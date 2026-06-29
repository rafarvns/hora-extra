using System;
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    /// <summary>
    /// Payload retornado pelo endpoint POST /api/auth/guest.
    /// Encapsulado em ApiResponse<GuestData> pelo backend (sendCreated).
    /// </summary>
    [Serializable]
    public class GuestData
    {
        /// <summary>JWT temporário (1h) para autenticação UDP.</summary>
        [JsonProperty("token")]
        public string Token;

        /// <summary>Identificador anônimo gerado pelo servidor (prefixo "guest-").</summary>
        [JsonProperty("guestId")]
        public string GuestId;

        /// <summary>Sala compartilhada automática — sempre "guest-room".</summary>
        [JsonProperty("roomId")]
        public string RoomId;
    }
}
