using System;
using System.Threading.Tasks;
using UnityEngine;
using HoraExtra.Network.Models;

namespace HoraExtra.Network.Rest.Services
{
    /// <summary>
    /// Serviço responsável pelo acesso guest ao servidor.
    /// Não requer cadastro nem login prévio.
    /// </summary>
    public static class GuestService
    {
        private const string GuestEndpoint = "/auth/guest";

        /// <summary>
        /// Solicita um token JWT guest ao backend e retorna os dados de sessão.
        /// O backend cria um guestId anônimo (prefixo "guest-") e associa automaticamente
        /// à sala compartilhada "guest-room".
        /// </summary>
        public static async Task<ApiResponse<GuestData>> JoinAsGuest()
        {
            try
            {
                Debug.Log("[NETWORK] Solicitando acesso como convidado...");
                var response = await ApiClient.Post<GuestData>(GuestEndpoint);

                if (response.Success)
                {
                    Debug.Log($"[NETWORK] Acesso guest concedido. ID: {response.Data.GuestId}, Sala: {response.Data.RoomId}");
                }
                else
                {
                    Debug.LogWarning($"[NETWORK] Falha no acesso guest: {response.Error?.Message}");
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NETWORK] Erro inesperado ao entrar como convidado: {ex.Message}");
                return null;
            }
        }
    }
}
