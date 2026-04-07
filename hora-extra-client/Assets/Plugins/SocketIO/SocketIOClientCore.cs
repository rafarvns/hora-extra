using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SocketIOClient
{
    public class SocketIOOptions
    {
        public Dictionary<string, string> Query { get; set; }
        public Transport.TransportProtocol Transport { get; set; }
        public bool AutoConnect { get; set; } = true;
        public int ConnectionTimeout { get; set; } = 20000;
        public Dictionary<string, string> ExtraHeaders { get; set; }
        public bool Reconnection { get; set; } = true;
    }

    namespace Transport
    {
        public enum TransportProtocol
        {
            Polling,
            WebSocket
        }
    }

    public interface IJsonSerializer
    {
        string Serialize(object[] obj);
        T Deserialize<T>(string json);
    }

    namespace Newtonsoft.Json
    {
        public class NewtonsoftJsonSerializer : IJsonSerializer
        {
            private readonly JsonSerializerSettings _settings;

            public NewtonsoftJsonSerializer()
            {
                _settings = new JsonSerializerSettings();
            }

            public NewtonsoftJsonSerializer(JsonSerializerSettings settings)
            {
                _settings = settings;
            }

            public string Serialize(object[] obj)
            {
                return JsonConvert.SerializeObject(obj, _settings);
            }

            public T Deserialize<T>(string json)
            {
                return JsonConvert.DeserializeObject<T>(json, _settings);
            }
        }
    }
}

namespace SocketIOClient
{
    // Minimal implementation of SocketIO data wrapper to fix CS0246
    public class SocketIOResponse
    {
        private readonly JArray _array;
        public SocketIOResponse(JArray array) => _array = array;
        public T GetValue<T>(int index = 0) => _array[index].ToObject<T>();
        public T GetValue<T>(string key) => _array[0][key].ToObject<T>();
        public override string ToString() => _array.ToString();
    }
}
