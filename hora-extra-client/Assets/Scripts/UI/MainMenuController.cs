using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace HoraExtra.UI
{
    /**
     * Controlador do Menu Principal.
     * Gerencia a navegação entre as telas de Login, Cadastro e o Lobby do jogo.
     */
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button btnGoToRegister;
        [SerializeField] private Button btnGoToLogin;
        [SerializeField] private Button btnGoToLobby;

        [Header("Scene Names")]
        [SerializeField] private string registerSceneName = "CreateAccountScene";
        [SerializeField] private string loginSceneName = "LoginScene";
        [SerializeField] private string lobbySceneName = "SampleScene"; // Ajuste se houver uma cena específica de Lobby

        [Header("User Info (Logged In State)")]
        [SerializeField] private TMPro.TextMeshProUGUI userNameText;

        private void Awake()
        {
            // Vincula as funções aos botões
            if (btnGoToRegister != null)
                btnGoToRegister.onClick.AddListener(() => LoadScene(registerSceneName));

            if (btnGoToLogin != null)
                btnGoToLogin.onClick.AddListener(() => LoadScene(loginSceneName));

            if (btnGoToLobby != null)
                btnGoToLobby.onClick.AddListener(() => LoadScene(lobbySceneName));
        }

        private void OnEnable()
        {
            // Tenta atualizar imediatamente
            UpdateUIState();

            // Se inscreve para atualizações futuras
            if (HoraExtra.Network.SessionManager.Instance != null)
            {
                HoraExtra.Network.SessionManager.Instance.OnSessionUpdated += UpdateUIState;
            }
        }

        private void OnDisable()
        {
            if (HoraExtra.Network.SessionManager.Instance != null)
            {
                HoraExtra.Network.SessionManager.Instance.OnSessionUpdated -= UpdateUIState;
            }
        }

        /**
         * Atualiza a visibilidade dos botões e o texto de boas-vindas com base na sessão.
         */
        private void UpdateUIState()
        {
            var session = HoraExtra.Network.SessionManager.Instance;
            bool isLoggedIn = session != null && session.IsLoggedIn;

            if (isLoggedIn)
            {
                if (userNameText != null)
                    userNameText.text = session.CurrentPlayer.Nome;
                
                // Desativa os botões de login e cadastro individualmente
                if (btnGoToLogin != null) btnGoToLogin.gameObject.SetActive(false);
                if (btnGoToRegister != null) btnGoToRegister.gameObject.SetActive(false);
            }
            else
            {
                if (userNameText != null)
                    userNameText.text = ""; // Limpa se não estiver logado
                
                if (btnGoToLogin != null) btnGoToLogin.gameObject.SetActive(true);
                if (btnGoToRegister != null) btnGoToRegister.gameObject.SetActive(true);
            }
        }

        /**
         * Carrega uma cena pelo nome.
         */
        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[MainMenuController] Nome da cena está vazio!");
                return;
            }

            Debug.Log($"[MainMenuController] Carregando cena: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        /**
         * Sai do aplicativo.
         */
        public void QuitGame()
        {
            Debug.Log("[MainMenuController] Saindo do jogo...");
            Application.Quit();
        }
    }
}
