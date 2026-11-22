using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HoraExtra.Network.Models;

namespace HoraExtra.Network.Rest.Services
{
    /// <summary>
    /// Endpoints de entrada no jogo: sessão só com o nome (/session) e salas (/rooms).
    /// Não há cadastro nem login.
    /// </summary>
    public static class RoomService
    {
        private const string ROOMS = "/rooms";
        private const string SESSION = "/session";

        /// <summary>Lista as salas abertas (mais novas primeiro).</summary>
        public static Task<ApiResponse<List<RoomData>>> GetRooms()
        {
            Debug.Log("[NETWORK] RoomService — buscando salas...");
            return ApiClient.Get<List<RoomData>>(ROOMS);
        }

        /// <summary>Cria uma sala vazia; quem cria entra em seguida pelo CONN.</summary>
        public static Task<ApiResponse<RoomData>> CreateRoom(string nome)
        {
            Debug.Log($"[NETWORK] RoomService — criando sala '{nome}'...");
            return ApiClient.Post<RoomData>(ROOMS, new NomeRequest { Nome = nome });
        }

        /// <summary>Abre a sessão do jogador com o nome digitado e devolve o token.</summary>
        public static Task<ApiResponse<SessionData>> CreateSession(string nome)
        {
            Debug.Log($"[NETWORK] RoomService — abrindo sessão para '{nome}'...");
            return ApiClient.Post<SessionData>(SESSION, new NomeRequest { Nome = nome });
        }
    }
}
