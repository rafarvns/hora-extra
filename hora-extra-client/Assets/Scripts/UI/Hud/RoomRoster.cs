using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using HoraExtra.Network.Models;

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Lista de jogadores da sala, mantida a partir do room_roster do servidor.
    ///
    /// O servidor sempre manda a lista COMPLETA; aqui ela é substituída e o diff com a
    /// anterior vira os eventos de entrada/saída (usados pelos toasts).
    /// </summary>
    public class RoomRoster : MonoBehaviour
    {
        public static RoomRoster Instance { get; private set; }

        /// <summary>Disparado a cada roster recebido (lista nova já aplicada).</summary>
        public event Action OnRosterChanged;

        /// <summary>Jogador entrou na sala (não dispara para o primeiro roster recebido).</summary>
        public event Action<RosterEntry> OnPlayerJoined;

        /// <summary>Jogador saiu da sala (timeout de sessão no servidor).</summary>
        public event Action<RosterEntry> OnPlayerLeft;

        private readonly List<RosterEntry> _players = new List<RosterEntry>();
        private bool _receivedOnce;

        public IReadOnlyList<RosterEntry> Players => _players;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            SocketManager.Instance?.On(NetworkEvents.ROOM_ROSTER, OnRosterReceived);
        }

        private void OnDisable()
        {
            SocketManager.Instance?.Off(NetworkEvents.ROOM_ROSTER, OnRosterReceived);
            if (Instance == this) Instance = null;
        }

        /// <summary>Nome de exibição de um jogador, ou null se não está no roster.</summary>
        public string GetName(string playerId)
        {
            foreach (var p in _players)
                if (p.Id == playerId) return p.Name;
            return null;
        }

        private void OnRosterReceived(JToken data)
        {
            RoomRosterPayload payload;
            try
            {
                payload = data.ToObject<RoomRosterPayload>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NETWORK] Falha ao parsear room_roster: {ex.Message} | raw: {data}");
                return;
            }
            if (payload?.Players == null) return;

            var anterior = new List<RosterEntry>(_players);
            _players.Clear();
            _players.AddRange(payload.Players);

            Debug.Log($"[UI] RoomRoster — {_players.Count} jogador(es) na sala.");

            if (_receivedOnce)
            {
                foreach (var p in _players)
                    if (!Contains(anterior, p.Id)) OnPlayerJoined?.Invoke(p);
                foreach (var p in anterior)
                    if (!Contains(_players, p.Id)) OnPlayerLeft?.Invoke(p);
            }
            _receivedOnce = true;

            OnRosterChanged?.Invoke();
        }

        private static bool Contains(List<RosterEntry> list, string id)
        {
            foreach (var p in list)
                if (p.Id == id) return true;
            return false;
        }
    }
}
