using System;
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    [Serializable]
    public class RegisterRequest
    {
        [JsonProperty("nome")]
        public string Nome;

        [JsonProperty("email")]
        public string Email;

        [JsonProperty("senha")]
        public string Senha;
    }

    [Serializable]
    public class LoginRequest
    {
        [JsonProperty("email")]
        public string Email;

        [JsonProperty("senha")]
        public string Senha;
    }

    [Serializable]
    public class AuthData
    {
        [JsonProperty("jogador")]
        public PlayerData Jogador;

        [JsonProperty("token")]
        public string Token;
    }

    [Serializable]
    public class PlayerData
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("nome")]
        public string Nome;

        [JsonProperty("email")]
        public string Email;

        [JsonProperty("nivel")]
        public int Nivel;

        [JsonProperty("xp")]
        public int Xp;
    }
}
