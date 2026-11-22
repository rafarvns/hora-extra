using System;
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    /// <summary>
    /// Sala na lista do menu (GET /api/rooms).
    /// Shape: { id, nome, jogadores, max, emAndamento }
    /// </summary>
    [Serializable]
    public class RoomData
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("nome")]        public string Nome;
        [JsonProperty("jogadores")]   public int    Jogadores;
        [JsonProperty("max")]         public int    Max;
        [JsonProperty("emAndamento")] public bool   EmAndamento;

        public bool Cheia => Jogadores >= Max;
    }

    /// <summary>Corpo de POST /api/rooms e de POST /api/session: { nome }.</summary>
    [Serializable]
    public class NomeRequest
    {
        [JsonProperty("nome")] public string Nome;
    }

    /// <summary>Resposta de POST /api/session: { token, playerId, nome }.</summary>
    [Serializable]
    public class SessionData
    {
        [JsonProperty("token")]    public string Token;
        [JsonProperty("playerId")] public string PlayerId;
        [JsonProperty("nome")]     public string Nome;
    }
}
