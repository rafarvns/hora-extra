using UnityEngine;
using HoraExtra.Network.Models;

namespace HoraExtra.Network
{
    /**
     * Singleton responsável por manter o estado da sessão do jogador durante a execução do jogo.
     */
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Session Data")]
        public PlayerData CurrentPlayer;
        public string AuthToken;

        public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);

        public System.Action OnSessionUpdated;

        /// <summary>
        /// Garante a instância (criando um GameObject persistente se preciso). Chamado pelo menu
        /// ao abrir, para que o login salvo em PlayerPrefs seja restaurado já na primeira tela.
        /// </summary>
        public static SessionManager EnsureExists()
        {
            if (Instance == null)
                new GameObject("SessionManager").AddComponent<SessionManager>();
            return Instance;
        }

        private void Awake()
        {
            Debug.Log("[SessionManager] Awake chamado.");
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSession();
                Debug.Log("[SessionManager] Singleton inicializado e sessão carregada.");
            }
            else
            {
                Debug.LogWarning("[SessionManager] Instância duplicada detectada e destruída.");
                Destroy(gameObject);
            }
        }

        /**
         * Define os dados da sessão após um login ou cadastro bem-sucedido.
         */
        public void SetSession(AuthData authData)
        {
            CurrentPlayer = authData.Jogador;
            AuthToken = authData.Token;

            // Persistência básica do token e nome
            PlayerPrefs.SetString("AuthToken", AuthToken);
            PlayerPrefs.SetString("PlayerName", CurrentPlayer.Nome);
            PlayerPrefs.Save();
            
            Debug.Log($"[SessionManager] Sessão definida para o jogador: {CurrentPlayer.Nome}");
            OnSessionUpdated?.Invoke();
        }

        /**
         * Limpa os dados da sessão (Logout).
         */
        public void ClearSession()
        {
            CurrentPlayer = null;
            AuthToken = null;
            PlayerPrefs.DeleteKey("AuthToken");
            PlayerPrefs.DeleteKey("PlayerName");
            PlayerPrefs.Save();
            Debug.Log("[SessionManager] Sessão encerrada.");
            OnSessionUpdated?.Invoke();
        }

        /**
         * Tenta carregar dados básicos da sessão salvos no dispositivo.
         */
        private void LoadSession()
        {
            AuthToken = PlayerPrefs.GetString("AuthToken", "");
            string cachedName = PlayerPrefs.GetString("PlayerName", "");

            if (IsLoggedIn && CurrentPlayer == null)
            {
                CurrentPlayer = new PlayerData { Nome = cachedName };
                Debug.Log($"[SessionManager] Sessão recuperada: {cachedName}");
            }

            OnSessionUpdated?.Invoke();
        }
    }
}
