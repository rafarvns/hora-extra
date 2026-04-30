using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace HoraExtra.Network.Models
{
    [Serializable]
    public class RoomData
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("nome")]
        public string Nome;

        [JsonProperty("hostId")]
        public string HostId;

        [JsonProperty("playerCount")]
        public int PlayerCount;

        [JsonProperty("maxPlayers")]
        public int MaxPlayers;

        [JsonProperty("status")]
        public string Status;
    }

    [Serializable]
    public class CreateRoomRequest
    {
        [JsonProperty("nome")]
        public string Nome;

        [JsonProperty("hostId")]
        public string HostId;
    }
}
