using UnityEngine;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

/**
 * SocketManager: Gerencia a conexão Socket.IO no Unity.
 * Certifique-se de ter a lib 'SocketIOUnity' instalada no projeto.
 */
public class SocketManager : MonoBehaviour
{
    public static SocketManager Instance { get; private set; }

    [Header("Network Settings")]
    public string ServerUrl = "http://localhost:3000";
    public bool AutoConnect = true;

    [Header("Development & Testing")]
    [Tooltip("Se ativado, utiliza o token de teste fixo em vez de pedir o login real.")]
    public bool UseTestToken = true;
    public string TestToken = "horaextra_dev_test_token_2026";

    private SocketIOUnity _socket;

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

    private void Start()
    {
        if (AutoConnect)
        {
            ConnectToServer();
        }
    }

    public void ConnectToServer()
    {
        Debug.Log($"[NETWORK] Conectando ao servidor: {ServerUrl}");

        string token = UseTestToken ? TestToken : HoraExtra.Network.NetworkSettings.AuthToken;

        var uri = new Uri(ServerUrl);
        _socket = new SocketIOUnity(uri, new SocketIOOptions
        {
            Query = new System.Collections.Generic.Dictionary<string, string>
            {
                {"token", token}
            },
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
        });

        // Configurar o serializador JSON (necessita de Newtonsoft.Json)
        _socket.JsonSerializer = new NewtonsoftJsonSerializer();

        // Listeners Padrão
        _socket.OnConnected += (sender, e) =>
        {
            Debug.Log("[NETWORK] Conexão Socket.IO estabelecida!");
        };

        _socket.OnDisconnected += (sender, e) =>
        {
            Debug.Log($"[NETWORK] Desconectado. Motivo: {e}");
        };

        // Escutando eventos customizados (Exemplos do COMMUNICATION.md)
        _socket.OnUnityThread("connection_success", (data) =>
        {
            Debug.Log($"[NETWORK] ID da Sessão: {data.GetValue<string>("id")}");
        });

        _socket.OnUnityThread("room_joined", (data) =>
        {
            Debug.Log($"[NETWORK] Sala entrada com sucesso: {data.GetValue<string>("roomId")}");
        });

        _socket.Connect();
    }

    /// <summary>
    /// Envia um evento para o servidor.
    /// </summary>
    public void Emit(string eventName, object data)
    {
        if (_socket != null && _socket.Connected)
        {
            _socket.Emit(eventName, data);
        }
    }

    private void OnDestroy()
    {
        if (_socket != null)
        {
            _socket.Disconnect();
            _socket.Dispose();
        }
    }
}
