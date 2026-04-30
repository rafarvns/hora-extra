using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using HoraExtra.Network.Models;

namespace HoraExtra.UI.Lobby
{
    /**
     * Componente anexado ao Prefab RoomItem para exibir dados da sala.
     */
    public class RoomItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button joinButton;

        private RoomData _roomData;
        private Action<string> _onJoinClicked;

        /**
         * Inicializa o item com os dados da sala.
         */
        public void Setup(RoomData data, Action<string> onJoin)
        {
            _roomData = data;
            _onJoinClicked = onJoin;

            if (roomNameText != null) roomNameText.text = data.Nome;
            if (playerCountText != null) playerCountText.text = $"{data.PlayerCount}/{data.MaxPlayers}";

            if (joinButton != null)
            {
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(() => _onJoinClicked?.Invoke(_roomData.Id));
            }
        }
    }
}
