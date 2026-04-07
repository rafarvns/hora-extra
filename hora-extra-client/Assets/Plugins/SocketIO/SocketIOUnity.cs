using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using SocketIOClient;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

public class SocketIOUnity : IDisposable
{
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private readonly Uri _uri;
    private readonly SocketIOOptions _options;
    private readonly Dictionary<string, List<Action<SocketIOResponse>>> _handlers = new Dictionary<string, List<Action<SocketIOResponse>>>();

    public event EventHandler OnConnected;
    public event EventHandler<string> OnDisconnected;
    public bool Connected => _ws?.State == WebSocketState.Open;
    public IJsonSerializer JsonSerializer { get; set; }

    public SocketIOUnity(Uri uri, SocketIOOptions options)
    {
        _uri = uri;
        _options = options;
    }

    public async void Connect()
    {
        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            // Socket.IO v4 WebSocket support
            var wsUrl = _uri.ToString().Replace("http", "ws") + "/socket.io/?EIO=4&transport=websocket";
            if (_options.Query != null)
            {
                foreach (var q in _options.Query) wsUrl += $"&{q.Key}={q.Value}";
            }

            Debug.Log($"[SocketIO] Connecting to {wsUrl}");
            await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
            OnConnected?.Invoke(this, EventArgs.Empty);
            
            _ = ReceiveLoop();
            _ = PingLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SocketIO] Connection error: {ex.Message}");
            OnDisconnected?.Invoke(this, ex.Message);
        }
    }

    public void OnUnityThread(string eventName, Action<SocketIOResponse> handler)
    {
        if (!_handlers.ContainsKey(eventName)) _handlers[eventName] = new List<Action<SocketIOResponse>>();
        _handlers[eventName].Add(data => UnityThread.Execute(() => handler(data)));
    }

    public async void Emit(string eventName, object data)
    {
        if (!Connected) return;

        // Socket.IO v4: 42["event", data]
        var payload = $"42[\"{eventName}\",{JsonConvert.SerializeObject(data)}]";
        var bytes = Encoding.UTF8.GetBytes(payload);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 8];
        while (Connected)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            ParseMessage(message);
        }
    }

    private void ParseMessage(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 2) return;

        // Simple v4 parser: 42 is Message, others: 0 (Open), 2 (Ping), 3 (Pong)
        if (raw.StartsWith("42"))
        {
            var json = raw.Substring(2);
            var array = JArray.Parse(json);
            var eventName = array[0].ToString();
            var data = new SocketIOResponse(array); // The actual data is usually at index 1 or we wrap the whole thing

            if (_handlers.TryGetValue(eventName, out var handlers))
            {
                foreach (var h in handlers) h.Invoke(data);
            }
        }
        else if (raw.StartsWith("2"))
        {
            _ = SendPong();
        }
    }

    private async Task SendPong()
    {
        if (!Connected) return;
        var bytes = Encoding.UTF8.GetBytes("3"); // Pong
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
    }

    private async Task PingLoop()
    {
        while (Connected)
        {
            await Task.Delay(25000); // v4 default interval
            if (Connected)
            {
                var bytes = Encoding.UTF8.GetBytes("2"); // Ping
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
            }
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
    }

    public void Dispose() => Disconnect();
}
