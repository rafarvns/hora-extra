using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/**
 * SocketManager (UDP Version): Gerencia a conexão de rede usando UDP Datagram nativo.
 * Substitui o Socket.IO para maior performance e simplicidade.
 */
public class SocketManager : MonoBehaviour
{
    public static SocketManager Instance { get; private set; }
    public string LocalPlayerId { get; private set; }

    [Header("Network Settings")]
    public string ServerIp = "127.0.0.1";
    public int ServerPort = 5001;
    public bool AutoConnect = true;

    [Header("Development & Testing")]
    public bool UseTestToken = true;
    public string TestToken = "horaextra_dev_test_token_2026";

    private UdpClient _udpClient;
    private IPEndPoint _serverEndPoint;
    private bool _isConnected = false;

    // Fila para processar eventos na Thread Principal do Unity
    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Inicializar o socket o mais cedo possível
            if (AutoConnect)
            {
                ConnectToServer();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Conexão agora é tratada no Awake para garantir prontidão para outros scripts no Start.
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
    }

    public void ConnectToServer()
    {
        try
        {
            Debug.Log($"[NETWORK] Iniciando Socket UDP para {ServerIp}:{ServerPort}");
            
            _udpClient = new UdpClient();
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort);
            
            // Iniciar loop de recebimento asíncrono
            ReceiveLoop();

            // Handshake (Envia Token de Conexão)
            SendHandshake();
        }
        catch (Exception e)
        {
            Debug.LogError($"[NETWORK] Erro ao iniciar UDP: {e.Message}");
        }
    }

    private async void SendHandshake()
    {
        string token = UseTestToken ? TestToken : HoraExtra.Network.NetworkSettings.AuthToken;
        
        // Protocolo: { "e": "CONN", "token": "...", "d": { "playerName": "..." } }
        var handshake = new {
            e = "CONN",
            token = token,
            d = new {
                playerName = "Player_" + UnityEngine.Random.Range(100, 999)
            }
        };

        Emit("CONN", handshake); // Emit manual do handshake
        Debug.Log("[NETWORK] Handshake de conexão enviado.");
    }

    private async void ReceiveLoop()
    {
        while (_udpClient != null)
        {
            try
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);
                
                // Agendar processamento na thread principal
                EnqueueAction(() => ProcessMessage(message));
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception e)
            {
                Debug.LogWarning($"[NETWORK] Erro no Receive UDP: {e.Message}");
            }
        }
    }

    private int _receivedMovePackets = 0;
    private void ProcessMessage(string json)
    {
        try
        {
            var packet = JsonConvert.DeserializeObject<JObject>(json);
            string eventName = packet["e"]?.ToString();
            JToken data = packet["d"];

            if (eventName == "CONN_SUCCESS")
            {
                _isConnected = true;
                LocalPlayerId = data["id"]?.ToString();
                Debug.Log($"[NETWORK] Conexão UDP Estabelecida! ID: {LocalPlayerId}");
                TriggerEvent("connection_success", data);
            }
            else if (eventName == "CONN_ERROR")
            {
                Debug.LogError($"[NETWORK] Erro na autenticação UDP: {data["message"]}");
            }
            else if (eventName == "player_move")
            {
                _receivedMovePackets++;
                if (_receivedMovePackets % 50 == 0) Debug.Log($"[NETWORK] Recebendo movimento de {data["id"]}... ({_receivedMovePackets} total)");
                TriggerEvent(eventName, data);
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

    private void TriggerEvent(string eventName, JToken data)
    {
        if (_eventHandlers.ContainsKey(eventName))
        {
            _eventHandlers[eventName]?.Invoke(data);
        }
    }

    // --- EMISSÃO ---
    private int _sentMovePackets = 0;
    public void Emit(string eventName, object payload)
    {
        if (_udpClient == null || _serverEndPoint == null)
        {
            Debug.LogWarning($"[NETWORK] Tentativa de enviar '{eventName}' antes do Socket estar pronto.");
            return;
        }

        try
        {
            // Se for o handshake (CONN), o payload já inclui o token. 
            // Para outros eventos, incluímos apenas 'e' e 'd'.
            object packet;
            if (eventName == "CONN") {
                packet = payload;
            } else {
                packet = new { e = eventName, d = payload };
            }

            if (eventName == "player_move") {
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

    private void OnDestroy()
    {
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
        }
    }
}
