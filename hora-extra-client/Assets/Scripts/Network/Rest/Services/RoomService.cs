using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using HoraExtra.Network.Models;

namespace HoraExtra.Network.Rest.Services
{
    /**
     * Serviço responsável pela comunicação com os endpoints de Salas (/rooms).
     */
    public class RoomService
    {
        private const string RoomsEndpoint = "/rooms";

        /// <summary>
        /// Obtém a lista de salas abertas do servidor.
        /// </summary>
        public async Task<ApiResponse<List<RoomData>>> GetRooms()
        {
            try
            {
                Debug.Log("[RoomService] Buscando lista de salas...");
                var response = await ApiClient.Get<List<RoomData>>(RoomsEndpoint);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomService] Erro ao buscar salas: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Solicita a criação de uma nova sala.
        /// </summary>
        public async Task<ApiResponse<RoomData>> CreateRoom(string roomName, string hostId)
        {
            try
            {
                var requestBody = new CreateRoomRequest
                {
                    Nome = roomName,
                    HostId = hostId
                };

                Debug.Log($"[RoomService] Criando sala: {roomName}...");
                var response = await ApiClient.Post<RoomData>(RoomsEndpoint, requestBody);
                return response;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomService] Erro ao criar sala: {ex.Message}");
                return null;
            }
        }
    }
}
