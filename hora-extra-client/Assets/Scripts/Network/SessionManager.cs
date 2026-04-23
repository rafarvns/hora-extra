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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSession();
            }
            else
            {
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
