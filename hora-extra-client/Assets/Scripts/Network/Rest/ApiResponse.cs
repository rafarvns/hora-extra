using System;
using Newtonsoft.Json;

namespace HoraExtra.Network.Rest
{
    [Serializable]
    public class ApiResponse<T>
    {
        [JsonProperty("success")]
        public bool Success;

        [JsonProperty("message")]
        public string Message;

        [JsonProperty("data")]
        public T Data;

        [JsonProperty("statusCode")]
        public int StatusCode;

        [JsonProperty("error")]
        public ApiErrorDetail Error;

        public bool HasError => !Success || Error != null;
    }

    [Serializable]
    public class ApiErrorDetail
    {
        [JsonProperty("message")]
        public string Message;

        [JsonProperty("statusCode")]
        public int StatusCode;
    }
}
