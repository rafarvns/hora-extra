using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using HoraExtra.Characters;
using HoraExtra.Network.Models;

namespace HoraExtra.Network
{
    /// <summary>
    /// Instancia e gerencia os jogadores remotos (o colega da sala).
    ///
    ///   - Os remotos são filhos deste objeto (DontDestroyOnLoad): sobrevivem à troca de andar.
    ///   - Só aparecem quando estão no MESMO andar que o jogador local (campo <c>f</c> do
    ///     player_move); senão ficam escondidos.
    ///   - Quem entra no meio da partida recebe os colegas pelo <c>room_state</c>, já na
    ///     posição e andar reais.
    ///   - Nome em cima da cabeça (<see cref="NetworkPlayer"/>).
    ///
    /// Por padrão usa uma cápsula colorida como visual (TCC). Para usar um modelo, atribua
    /// _remotePlayerPrefab.
    /// </summary>
    public class RemotePlayerSpawner : MonoBehaviour
    {
        public static RemotePlayerSpawner Instance { get; private set; }

        [Header("Configuracao de Spawn")]
        [SerializeField, Tooltip("Prefab opcional para jogadores remotos. Se null, usa Capsule placeholder.")]
        private GameObject _remotePlayerPrefab;

        private readonly Dictionary<string, NetworkPlayer> _remotePlayers = new();

        // Cores rotativas para distinguir os colegas visualmente
        private static readonly Color[] _palette = new[]
        {
            new Color(0.3f, 0.6f, 0.9f), // azul
            new Color(0.9f, 0.3f, 0.3f), // vermelho
            new Color(0.4f, 0.9f, 0.4f), // verde
            new Color(0.9f, 0.7f, 0.2f), // amarelo
        };
        private int _colorIdx = 0;
        private SocketManager _socket;

        /// <summary>
        /// Garante que existe uma instância do RemotePlayerSpawner (DontDestroyOnLoad).
        /// Chamado pelo MatchJoinFlow antes de conectar.
        /// </summary>
        public static RemotePlayerSpawner EnsureExists()
        {
            if (Instance == null)
            {
                Debug.Log("[NETWORK] RemotePlayerSpawner nao encontrado — criando runtime.");
                var go = new GameObject("RemotePlayerSpawner (auto-created)");
                go.AddComponent<RemotePlayerSpawner>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // O SocketManager é garantido antes deste objeto (MatchJoinFlow).
            _socket = SocketManager.EnsureExists();
            _socket.On(NetworkEvents.CONNECTION_SUCCESS, OnConnected);
            _socket.On(NetworkEvents.ROOM_STATE, OnRoomState);
            _socket.On(NetworkEvents.ROOM_ROSTER, OnRoster);
            _socket.On(NetworkEvents.PLAYER_JOINED, OnPlayerJoined);
            _socket.On(NetworkEvents.PLAYER_MOVE, OnPlayerMove);
            _socket.On(NetworkEvents.PLAYER_DISCONNECTED, OnPlayerDisconnected);
            FloorTracker.OnFloorChanged += OnLocalFloorChanged;
        }

        private void OnDisable()
        {
            if (_socket != null)
            {
                _socket.Off(NetworkEvents.CONNECTION_SUCCESS, OnConnected);
                _socket.Off(NetworkEvents.ROOM_STATE, OnRoomState);
                _socket.Off(NetworkEvents.ROOM_ROSTER, OnRoster);
                _socket.Off(NetworkEvents.PLAYER_JOINED, OnPlayerJoined);
                _socket.Off(NetworkEvents.PLAYER_MOVE, OnPlayerMove);
                _socket.Off(NetworkEvents.PLAYER_DISCONNECTED, OnPlayerDisconnected);
            }
            FloorTracker.OnFloorChanged -= OnLocalFloorChanged;
        }

        // ---- Handlers de evento ----

        /// <summary>Nova conexão (nova sala): ninguém da sessão anterior fica.</summary>
        private void OnConnected(JToken _) => ClearAll();

        private void OnRoomState(JToken data)
        {
            var estado = data?.ToObject<RoomStatePayload>();
            if (estado?.Players == null) return;

            foreach (var p in estado.Players)
            {
                if (string.IsNullOrEmpty(p.Id) || IsLocal(p.Id)) continue;
                var remote = SpawnIfNeeded(p.Id, p.Name);
                remote.Floor = p.Floor;
                if (p.Position != null && p.Position.Length == 3)
                    remote.UpdateState(p.Position, p.RotationY, snap: true);
                RefreshVisibility(remote);
            }
            Debug.Log($"[NETWORK] room_state — {estado.Players.Count} colega(s) já na sala.");
        }

        /// <summary>Roster é a lista oficial: atualiza nomes e remove quem não está mais.</summary>
        private void OnRoster(JToken data)
        {
            var roster = data?.ToObject<RoomRosterPayload>();
            if (roster?.Players == null) return;

            var presentes = new HashSet<string>();
            foreach (var entry in roster.Players)
            {
                if (string.IsNullOrEmpty(entry.Id) || IsLocal(entry.Id)) continue;
                presentes.Add(entry.Id);
                SpawnIfNeeded(entry.Id, entry.Name).SetName(entry.Name);
            }

            foreach (var id in new List<string>(_remotePlayers.Keys))
                if (!presentes.Contains(id)) Remove(id);
        }

        private void OnPlayerJoined(JToken data)
        {
            string id = data["id"]?.ToString();
            if (string.IsNullOrEmpty(id) || IsLocal(id)) return;

            // Quem acabou de entrar começa no 1º andar e ainda não andou: fica escondido até
            // o primeiro player_move.
            var remote = SpawnIfNeeded(id, data["name"]?.ToString());
            remote.Floor = 1;
            RefreshVisibility(remote);
        }

        private void OnPlayerMove(JToken data)
        {
            string id = data["id"]?.ToString();
            if (string.IsNullOrEmpty(id) || IsLocal(id)) return;

            var remote = SpawnIfNeeded(id, null);

            int andarAntes = remote.Floor;
            var f = data["f"];
            if (f != null && f.Type == JTokenType.Integer) remote.Floor = (int)f;

            var pos = data["p"];
            var rot = data["r"];
            if (pos != null && pos.HasValues && rot != null)
            {
                float[] p = { (float)pos[0], (float)pos[1], (float)pos[2] };
                // Trocou de andar: teleporta (a posição antiga é de outra cena).
                remote.UpdateState(p, (float)rot, snap: andarAntes != remote.Floor);
            }
            RefreshVisibility(remote);
        }

        private void OnPlayerDisconnected(JToken data)
        {
            string id = data["id"]?.ToString();
            if (!string.IsNullOrEmpty(id)) Remove(id);
        }

        private void OnLocalFloorChanged(int andar)
        {
            // Voltou ao menu: a partida acabou para este cliente.
            if (andar == 0)
            {
                ClearAll();
                return;
            }
            foreach (var remote in _remotePlayers.Values)
                if (remote != null) RefreshVisibility(remote);
        }

        // ---- Helpers ----

        private void RefreshVisibility(NetworkPlayer remote)
        {
            bool visivel = remote.HasPosition && remote.Floor == FloorTracker.CurrentFloor && FloorTracker.CurrentFloor != 0;
            if (remote.gameObject.activeSelf == visivel) return;
            if (visivel) remote.Snap();
            remote.gameObject.SetActive(visivel);
        }

        private static bool IsLocal(string id) => id == SocketManager.Instance?.LocalPlayerId;

        private void Remove(string id)
        {
            if (_remotePlayers.TryGetValue(id, out var np) && np != null)
            {
                Debug.Log($"[NETWORK] Colega saiu: {np.PlayerName} ({id})");
                Destroy(np.gameObject);
            }
            _remotePlayers.Remove(id);
        }

        private void ClearAll()
        {
            foreach (var id in new List<string>(_remotePlayers.Keys)) Remove(id);
        }

        private NetworkPlayer SpawnIfNeeded(string id, string name)
        {
            if (_remotePlayers.TryGetValue(id, out var existing) && existing != null)
                return existing;

            GameObject go;
            if (_remotePlayerPrefab != null)
            {
                go = Instantiate(_remotePlayerPrefab);
            }
            else
            {
                // Placeholder: Capsule colorida — funciona out-of-the-box sem prefab
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);

                var col = _palette[_colorIdx % _palette.Length];
                _colorIdx++;

                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.color = col;

                // Remove collider para nao interferir no movimento local
                var capsuleCol = go.GetComponent<Collider>();
                if (capsuleCol != null) Destroy(capsuleCol);
            }

            // Filho deste objeto persistente: sobrevive à troca de andar.
            go.transform.SetParent(transform, false);
            go.SetActive(false);

            var np = go.GetComponent<NetworkPlayer>();
            if (np == null) np = go.AddComponent<NetworkPlayer>();
            np.Initialize(id, name);

            _remotePlayers[id] = np;
            Debug.Log($"[NETWORK] NetworkPlayer remoto instanciado: id={id}, name={np.PlayerName}");
            return np;
        }
    }
}
