using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HoraExtra.Network;

/**
 * SocketManager (UDP): conexão de rede com o servidor da partida.
 *
 * Ciclo: <see cref="Connect"/>(token, roomId) envia CONN (reenviado até 3× se não houver
 * resposta) → CONN_SUCCESS (<see cref="OnConnected"/>) ou CONN_ERROR
 * (<see cref="OnConnectionError"/>, ex: sala cheia). Conectado, manda ping a cada 2 s; sem
 * nenhum pacote do servidor por <see cref="CONNECTION_LOST_SECONDS"/> dispara
 * <see cref="OnConnectionLost"/>. <see cref="Disconnect"/> avisa o servidor (leave_room)
 * para a vaga ser liberada na hora.
 */
public class SocketManager : MonoBehaviour
{
    public static SocketManager Instance { get; private set; }
    public string LocalPlayerId { get; private set; }
    public string RoomId { get; private set; }
    public bool IsConnected => _isConnected;
    public float Latency { get; private set; }

    /// <summary>CONN aceito. Parâmetro: id do jogador local.</summary>
    public event Action<string> OnConnected;

    /// <summary>CONN recusado. Parâmetros: código (ROOM_FULL, ROOM_NOT_FOUND, INVALID_TOKEN, TIMEOUT) e mensagem.</summary>
    public event Action<string, string> OnConnectionError;

    /// <summary>Conexão caiu (servidor parou de responder).</summary>
    public event Action OnConnectionLost;

    public const float CONNECTION_LOST_SECONDS = 10f;
    private const float HANDSHAKE_RETRY_SECONDS = 1.5f;
    private const int HANDSHAKE_ATTEMPTS = 3;

    /// <summary>
    /// Garante que existe uma instância do SocketManager (DontDestroyOnLoad). Ela nasce
    /// desconectada: quem conecta é o <see cref="MatchJoinFlow"/>.
    /// </summary>
    public static SocketManager EnsureExists()
    {
        if (Instance == null)
        {
            Debug.Log("[NETWORK] SocketManager nao encontrado — criando runtime.");
            var go = new GameObject("SocketManager (auto-created)");
            go.AddComponent<SocketManager>();
        }
        return Instance;
    }

    /// <summary>Host do backend — proxy de leitura para <see cref="BackendConfig.Host"/>.</summary>
    public string ServerIp => BackendConfig.Host;

    /// <summary>Porta UDP do backend — proxy de leitura para <see cref="BackendConfig.UdpPort"/>.</summary>
    public int ServerPort => BackendConfig.UdpPort;

    private UdpClient _udpClient;
    private IPEndPoint _serverEndPoint;
    private bool _isConnected = false;

    private string _token;
    private bool _handshakePending;
    private int _handshakeAttempts;
    private float _handshakeTimer;

    private float _lastPingTime;
    private float _lastPacketTime;
    private const float HeartbeatInterval = 2f;
    private float _heartbeatTimer = 0f;

    // Fila para processar eventos na Thread Principal do Unity
    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Processar ações pendentes na thread principal
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
            {
                _mainThreadQueue.Dequeue().Invoke();
            }
        }

        if (_handshakePending) TickHandshake();

        if (_isConnected)
        {
            _heartbeatTimer += Time.unscaledDeltaTime;
            if (_heartbeatTimer >= HeartbeatInterval)
            {
                SendPing();
                _heartbeatTimer = 0f;
            }

            if (Time.realtimeSinceStartup - _lastPacketTime > CONNECTION_LOST_SECONDS)
            {
                Debug.LogWarning("[NETWORK] Servidor parou de responder — conexão perdida.");
                CloseSocket();
                OnConnectionLost?.Invoke();
            }
        }
    }

    private void SendPing()
    {
        _lastPingTime = Time.realtimeSinceStartup;
        Emit(NetworkEvents.PING, new { t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    }

    // === Conexão ===

    /// <summary>
    /// Conecta na sala <paramref name="roomId"/> com o token da sessão. Fecha qualquer
    /// conexão anterior. O resultado chega por <see cref="OnConnected"/> ou
    /// <see cref="OnConnectionError"/>.
    /// </summary>
    public void Connect(string token, string roomId)
    {
        CloseSocket();
        _token = token;
        RoomId = roomId;

        try
        {
            Debug.Log($"[NETWORK] Conectando em {ServerIp}:{ServerPort} (sala {roomId})");
            _udpClient = new UdpClient();
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort);
            ReceiveLoop(_udpClient);

            _handshakePending = true;
            _handshakeAttempts = 0;
            _handshakeTimer = HANDSHAKE_RETRY_SECONDS; // envia já no próximo Update
        }
        catch (Exception e)
        {
            Debug.LogError($"[NETWORK] Erro ao iniciar UDP: {e.Message}");
            _handshakePending = false;
            OnConnectionError?.Invoke("NETWORK", "Não foi possível abrir a conexão.");
        }
    }

    /// <summary>Sai da sala (leave_room) e fecha a conexão.</summary>
    public void Disconnect()
    {
        if (_isConnected) Emit(NetworkEvents.LEAVE_ROOM, new { });
        CloseSocket();
        LocalPlayerId = null;
        RoomId = null;
        Debug.Log("[NETWORK] Desconectado da sala.");
    }

    private void TickHandshake()
    {
        _handshakeTimer += Time.unscaledDeltaTime;
        if (_handshakeTimer < HANDSHAKE_RETRY_SECONDS) return;
        _handshakeTimer = 0f;

        if (_handshakeAttempts >= HANDSHAKE_ATTEMPTS)
        {
            _handshakePending = false;
            Debug.LogWarning("[NETWORK] Sem resposta ao CONN.");
            CloseSocket();
            OnConnectionError?.Invoke("TIMEOUT", "O servidor não respondeu.");
            return;
        }

        _handshakeAttempts++;
        Emit(NetworkEvents.CONN, new { e = NetworkEvents.CONN, token = _token, d = new { roomId = RoomId } });
        Debug.Log($"[NETWORK] CONN enviado (tentativa {_handshakeAttempts}/{HANDSHAKE_ATTEMPTS}).");
    }

    private void CloseSocket()
    {
        _handshakePending = false;
        _isConnected = false;
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
        }
    }

    private async void ReceiveLoop(UdpClient client)
    {
        while (client == _udpClient && client != null)
        {
            try
            {
                UdpReceiveResult result = await client.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);
                EnqueueAction(() => { if (client == _udpClient) ProcessMessage(message); });
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException e)
            {
                // ICMP "porta inalcançável" (servidor fora do ar) chega aqui no Windows.
                if (client != _udpClient) break;
                Debug.LogWarning($"[NETWORK] Erro no Receive UDP: {e.Message}");
            }
            catch (Exception e)
            {
                if (client != _udpClient) break;
                Debug.LogWarning($"[NETWORK] Erro no Receive UDP: {e.Message}");
            }
        }
    }

    private int _receivedMovePackets = 0;
    private void ProcessMessage(string json)
    {
        try
        {
            _lastPacketTime = Time.realtimeSinceStartup;

            var packet = JsonConvert.DeserializeObject<JObject>(json);
            string eventName = packet["e"]?.ToString();
            JToken data = packet["d"];

            if (eventName == NetworkEvents.CONN_SUCCESS)
            {
                if (_isConnected) return; // resposta duplicada de um CONN reenviado
                _handshakePending = false;
                _isConnected = true;
                LocalPlayerId = data["id"]?.ToString();
                Debug.Log($"[NETWORK] Conectado na sala {RoomId}. ID: {LocalPlayerId}");
                TriggerEvent(NetworkEvents.CONNECTION_SUCCESS, data);
                OnConnected?.Invoke(LocalPlayerId);
            }
            else if (eventName == NetworkEvents.CONN_ERROR)
            {
                string code = data?["code"]?.ToString() ?? "ERROR";
                string message = data?["message"]?.ToString() ?? "Conexão recusada.";
                Debug.LogWarning($"[NETWORK] CONN recusado ({code}): {message}");
                CloseSocket();
                OnConnectionError?.Invoke(code, message);
            }
            else if (eventName == NetworkEvents.PLAYER_MOVE)
            {
                _receivedMovePackets++;
                if (_receivedMovePackets % 50 == 0) Debug.Log($"[NETWORK] Recebendo movimento de {data["id"]}... ({_receivedMovePackets} total)");
                TriggerEvent(eventName, data);
            }
            else if (eventName == NetworkEvents.PONG)
            {
                Latency = (Time.realtimeSinceStartup - _lastPingTime) * 1000f;
            }
            else
            {
                TriggerEvent(eventName, data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NETWORK] Erro ao processar mensagem JSON: {e.Message} \n {json}");
        }
    }

    // --- SISTEMA DE EVENTOS ---
    private Dictionary<string, Action<JToken>> _eventHandlers = new Dictionary<string, Action<JToken>>();

    public void On(string eventName, Action<JToken> handler)
    {
        if (!_eventHandlers.ContainsKey(eventName))
            _eventHandlers[eventName] = null;

        _eventHandlers[eventName] += handler;
    }

    public void Off(string eventName, Action<JToken> handler)
    {
        if (_eventHandlers.ContainsKey(eventName))
        {
            _eventHandlers[eventName] -= handler;
        }
    }

    private void TriggerEvent(string eventName, JToken data)
    {
        if (!_eventHandlers.TryGetValue(eventName, out var handlers) || handlers == null) return;

        // Um handler com erro (ex: objeto destruído) não pode impedir os outros de rodar.
        foreach (Action<JToken> h in handlers.GetInvocationList())
        {
            try { h(data); }
            catch (Exception e) { Debug.LogError($"[NETWORK] Erro no handler de '{eventName}': {e}"); }
        }
    }

    // --- EMISSÃO ---
    private int _sentMovePackets = 0;
    public void Emit(string eventName, object payload)
    {
        if (_udpClient == null || _serverEndPoint == null)
        {
            return;
        }

        try
        {
            // O CONN já vem montado ({ e, token, d }); os demais são { e, d }.
            object packet = eventName == NetworkEvents.CONN ? payload : new { e = eventName, d = payload };

            if (eventName == NetworkEvents.PLAYER_MOVE) {
                _sentMovePackets++;
                if (_sentMovePackets % 50 == 0) Debug.Log($"[NETWORK] Enviando movimento... ({_sentMovePackets} total)");
            }

            string json = JsonConvert.SerializeObject(packet);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            _udpClient.Send(bytes, bytes.Length, _serverEndPoint);
        }
        catch (Exception e)
        {
            Debug.LogError($"[NETWORK] Falha ao enviar evento {eventName}: {e.Message}");
        }
    }

    private void EnqueueAction(Action action)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    private void OnApplicationQuit()
    {
        // Fechar o jogo libera a vaga na hora, sem esperar o timeout do servidor.
        Disconnect();
    }

    private void OnDestroy()
    {
        if (Instance == this) CloseSocket();
    }
}
