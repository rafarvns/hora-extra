using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using HoraExtra.Network.Rest.Services;
using HoraExtra.Network.Models;
using HoraExtra.Network;
using UnityEngine.SceneManagement;

namespace HoraExtra.UI.Lobby
{
    /**
     * Controlador responsável pela lógica da cena de Lobby.
     */
    public class LobbyController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform roomsContainer; // Content do ScrollView
        [SerializeField] private GameObject roomItemPrefab;
        [SerializeField] private Button btnCreateRoom;
        [SerializeField] private Button btnRefresh;

        [Header("Settings")]
        [SerializeField] private string gameSceneName = "SampleScene";

        private RoomService _roomService;

        private void Awake()
        {
            _roomService = new RoomService();

            if (btnCreateRoom != null)
                btnCreateRoom.onClick.AddListener(OnCreateRoomClicked);

            if (btnRefresh != null)
                btnRefresh.onClick.AddListener(RefreshRoomList);
        }

        private void Start()
        {
            RefreshRoomList();
        }

        /**
         * Busca a lista de salas e atualiza a UI.
         */
        public async void RefreshRoomList()
        {
            // Limpa lista atual
            foreach (Transform child in roomsContainer)
            {
                Destroy(child.gameObject);
            }

            var response = await _roomService.GetRooms();

            if (response != null && response.Success)
            {
                foreach (var room in response.Data)
                {
                    CreateRoomItem(room);
                }
            }
        }

        /**
         * Cria visualmente um item de sala na lista.
         */
        private void CreateRoomItem(RoomData data)
        {
            GameObject itemObj = Instantiate(roomItemPrefab, roomsContainer);
            RoomItemUI itemUI = itemObj.GetComponent<RoomItemUI>();
            
            if (itemUI != null)
            {
                itemUI.Setup(data, JoinRoom);
            }
        }

        /**
         * Lógica para criar uma nova sala (O nome será baseado no jogador).
         */
        private async void OnCreateRoomClicked()
        {
            if (SessionManager.Instance == null || !SessionManager.Instance.IsLoggedIn)
            {
                Debug.LogError("[LobbyController] Você precisa estar logado para criar uma sala!");
                return;
            }

            btnCreateRoom.interactable = false;
            string roomName = $"Sala de {SessionManager.Instance.CurrentPlayer.Nome}";
            string hostId = SessionManager.Instance.CurrentPlayer.Id;

            var response = await _roomService.CreateRoom(roomName, hostId);

            if (response != null && response.Success)
            {
                Debug.Log($"[LobbyController] Sala criada com ID: {response.Data.Id}");
                JoinRoom(response.Data.Id);
            }
            else
            {
                btnCreateRoom.interactable = true;
                Debug.LogError("[LobbyController] Falha ao criar sala.");
            }
        }

        /**
         * Entra em uma sala específica.
         */
        private void JoinRoom(string roomId)
        {
            Debug.Log($"[LobbyController] Entrando na sala: {roomId}");
            
            // Aqui poderíamos salvar o roomId no SessionManager ou passar via static
            // Por enquanto, apenas carregamos a cena de jogo
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
