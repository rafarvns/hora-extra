using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using HoraExtra.Characters;

namespace HoraExtra.Network
{
    /// <summary>
    /// Instancia e gerencia GameObjects visuais para jogadores remotos.
    /// Auto-criado via EnsureExists para tolerar cenas sem o GameObject pre-configurado.
    ///
    /// Assina PLAYER_JOINED, PLAYER_MOVE e PLAYER_DISCONNECTED do SocketManager.
    /// Ignora eventos do proprio LocalPlayerId.
    ///
    /// Por padrao usa Capsule primitiva colorida como placeholder visual (TCC).
    /// Para upgrade para prefab real, atribua _remotePlayerPrefab via Inspector
    /// OU adicione um prefab em Assets/Resources/PFB_NetworkPlayer e descomente
    /// o Resources.Load no Start.
    /// </summary>
    public class RemotePlayerSpawner : MonoBehaviour
    {
        public static RemotePlayerSpawner Instance { get; private set; }

        [Header("Configuracao de Spawn")]
        [SerializeField, Tooltip("Prefab opcional para jogadores remotos. Se null, usa Capsule placeholder.")]
        private GameObject _remotePlayerPrefab;

        [SerializeField, Tooltip("Altura inicial onde os players remotos serao spawnados.")]
        private float _spawnHeight = 1.1f;

        private readonly Dictionary<string, NetworkPlayer> _remotePlayers = new();

        // Cores rotativas para distinguir guests visualmente
        private static readonly Color[] _palette = new[]
        {
            new Color(0.9f, 0.3f, 0.3f), // vermelho
            new Color(0.3f, 0.6f, 0.9f), // azul
            new Color(0.4f, 0.9f, 0.4f), // verde
            new Color(0.9f, 0.7f, 0.2f), // amarelo
            new Color(0.7f, 0.4f, 0.9f), // roxo
        };
        private int _colorIdx = 0;

        /// <summary>
        /// Garante que existe uma instancia do RemotePlayerSpawner. Se nao existir
        /// na cena, cria um GameObject em runtime com DontDestroyOnLoad.
        /// Deve ser chamado no OnGuestPlayClicked apos SetAuthTokenAndReconnect.
        /// </summary>
        public static RemotePlayerSpawner EnsureExists()
        {
            if (Instance == null)
            {
                Debug.Log("[NETWORK] RemotePlayerSpawner nao encontrado — criando runtime.");
                var go = new GameObject("RemotePlayerSpawner (auto-created)");
                go.AddComponent<RemotePlayerSpawner>();
                // Awake roda imediatamente no AddComponent; Instance ja esta setado aqui.
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

        private void Start()
        {
            // Subscribe nos eventos de rede. Usa Start (nao OnEnable) para garantir
            // que o SocketManager ja existiu via Awake/AddComponent antes de assinar.
            var sm = SocketManager.EnsureExists();
            sm.On(NetworkEvents.PLAYER_JOINED, OnPlayerJoined);
            sm.On(NetworkEvents.PLAYER_MOVE, OnPlayerMove);
            sm.On(NetworkEvents.PLAYER_DISCONNECTED, OnPlayerDisconnected);
            Debug.Log("[NETWORK] RemotePlayerSpawner subscribed em PLAYER_JOINED / PLAYER_MOVE / PLAYER_DISCONNECTED");
        }

        // ---- Handlers de evento ----

        private void OnPlayerJoined(JToken data)
        {
            string id = data["id"]?.ToString();
            string name = data["name"]?.ToString() ?? "Player";
            if (string.IsNullOrEmpty(id)) return;
            if (id == SocketManager.Instance?.LocalPlayerId) return;

            SpawnIfNeeded(id, name);
        }

        private void OnPlayerMove(JToken data)
        {
            string id = data["id"]?.ToString();
            if (string.IsNullOrEmpty(id)) return;
            if (id == SocketManager.Instance?.LocalPlayerId) return;

            // Lazy spawn: se chegou movement sem PLAYER_JOINED previo (ex.: entramos depois)
            var remote = SpawnIfNeeded(id, "Remote");

            var pos = data["p"];
            var rot = data["r"];
            if (pos != null && pos.HasValues && rot != null)
            {
                float[] p = new[] { (float)pos[0], (float)pos[1], (float)pos[2] };
                float r = (float)rot;
                remote.UpdateState(p, r);
            }
        }

        private void OnPlayerDisconnected(JToken data)
        {
            string id = data["id"]?.ToString();
            if (string.IsNullOrEmpty(id)) return;

            if (_remotePlayers.TryGetValue(id, out var np) && np != null)
            {
                Debug.Log($"[NETWORK] Removendo NetworkPlayer remoto: {id}");
                Destroy(np.gameObject);
                _remotePlayers.Remove(id);
            }
        }

        // ---- Spawn helper ----

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

            go.transform.position = new Vector3(0f, _spawnHeight, 0f);

            var np = go.GetComponent<NetworkPlayer>();
            if (np == null) np = go.AddComponent<NetworkPlayer>();
            np.Initialize(id, name);

            _remotePlayers[id] = np;
            Debug.Log($"[NETWORK] NetworkPlayer remoto instanciado: id={id}, name={name}");
            return np;
        }
    }
}
