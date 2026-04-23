using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace HoraExtra.Network.Rest
{
    /**
     * Cliente REST genérico para Unity.
     * Utiliza UnityWebRequest e Newtonsoft.Json para facilitar a comunicação com o backend Node.js.
     */
    public static class ApiClient
    {
        // Altere para a URL correta conforme o ambiente
        private const string BaseUrl = "http://127.0.0.1:5000/api";

        /// <summary>
        /// Realiza uma requisição GET assíncrona.
        /// </summary>
        public static async Task<ApiResponse<T>> Get<T>(string endpoint)
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{BaseUrl}{endpoint}"))
            {
                return await SendRequest<T>(request);
            }
        }

        /// <summary>
        /// Realiza uma requisição POST assíncrona.
        /// </summary>
        public static async Task<ApiResponse<T>> Post<T>(string endpoint, object body = null)
        {
            string jsonBody = body != null ? JsonConvert.SerializeObject(body) : "";
            using (UnityWebRequest request = new UnityWebRequest($"{BaseUrl}{endpoint}", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                return await SendRequest<T>(request);
            }
        }

        private static async Task<ApiResponse<T>> SendRequest<T>(UnityWebRequest request)
        {
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try {
                    return JsonConvert.DeserializeObject<ApiResponse<T>>(request.downloadHandler.text);
                } catch (Exception ex) {
                    Debug.LogError($"[ApiClient] Erro ao deserializar resposta: {ex.Message}");
                    return CreateErrorResponse<T>("Erro de serialização interna", 0);
                }
            }
            else
            {
                Debug.LogWarning($"[ApiClient] Erro HTTP {request.responseCode}: {request.error}");
                
                // Tenta extrair o erro do JSON retornado pelo backend
                try {
                    return JsonConvert.DeserializeObject<ApiResponse<T>>(request.downloadHandler.text);
                } catch {
                    return CreateErrorResponse<T>(request.error, (int)request.responseCode);
                }
            }
        }

        private static ApiResponse<T> CreateErrorResponse<T>(string message, int code)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = code,
                Error = new ApiErrorDetail { Message = message, StatusCode = code }
            };
        }
    }
}
