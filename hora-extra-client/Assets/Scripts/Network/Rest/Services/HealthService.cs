using System;
using System.Threading.Tasks;
using UnityEngine;

namespace HoraExtra.Network.Rest.Services
{
    [Serializable]
    public class HealthData
    {
        public string status;
        public string timestamp;
        public float uptime;
        public string service;
    }

    /**
     * Exemplo de Serviço para verificar o status do servidor.
     */
    public class HealthService
    {
        public async Task<bool> CheckServerHealth()
        {
            try
            {
                var response = await ApiClient.Get<HealthData>("/health");

                if (response.Success)
                {
                    Debug.Log($"[HealthService] Servidor Online! Uptime: {response.Data.uptime}s");
                    return true;
                }
                else
                {
                    Debug.LogError($"[HealthService] Erro ao conectar: {response.Error.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
    }
}
