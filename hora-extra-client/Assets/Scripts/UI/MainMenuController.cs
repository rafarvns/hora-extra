using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using HoraExtra.Network;

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
        [SerializeField] private Button btnGuestPlay;

        [Header("Scene Names")]
        [SerializeField] private string registerSceneName = "CreateAccountScene";
        [SerializeField] private string loginSceneName = "LoginScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";

        [Tooltip("Cena de gameplay carregada direto ao entrar como convidado. O guest já é " +
                 "auto-joinado na 'guest-room' pelo servidor, então não passa pelo lobby de salas.")]
        [SerializeField] private string guestGameSceneName = "SCN_FirstFloor";

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
            
            if (btnGuestPlay != null)
                btnGuestPlay.onClick.AddListener(OnGuestPlayClicked);
        }

        private void OnEnable()
        {
            Debug.Log("[MainMenuController] OnEnable chamado.");
            // Tenta atualizar imediatamente
            UpdateUIState();

            // Se inscreve para atualizações futuras
            if (HoraExtra.Network.SessionManager.Instance != null)
            {
                Debug.Log("[MainMenuController] Se inscrevendo no evento OnSessionUpdated.");
                HoraExtra.Network.SessionManager.Instance.OnSessionUpdated += UpdateUIState;
            }
            else
            {
                Debug.LogWarning("[MainMenuController] SessionManager.Instance está NULL no OnEnable!");
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

            Debug.Log($"[MainMenuController] UpdateUIState chamado. Logado: {isLoggedIn}");

            if (isLoggedIn)
            {
                if (userNameText != null)
                {
                    userNameText.text = session.CurrentPlayer.Nome;
                    Debug.Log($"[MainMenuController] Nome do usuário setado para: {session.CurrentPlayer.Nome}");
                }
                
                // Desativa os botões de login e cadastro individualmente
                if (btnGoToLogin != null) btnGoToLogin.gameObject.SetActive(false);
                if (btnGoToRegister != null) btnGoToRegister.gameObject.SetActive(false);
                Debug.Log("[MainMenuController] Botões de Login/Registro desativados.");
            }
            else
            {
                if (userNameText != null)
                    userNameText.text = ""; 
                
                if (btnGoToLogin != null) btnGoToLogin.gameObject.SetActive(true);
                if (btnGoToRegister != null) btnGoToRegister.gameObject.SetActive(true);
                Debug.Log("[MainMenuController] Botões de Login/Registro ativados (Modo Deslogado).");
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
        
        private async void OnGuestPlayClicked()
        {
            Debug.Log("[UI] Botão Guest Play clicado.");
            if (btnGuestPlay != null) btnGuestPlay.interactable = false;

            // Mesmo fluxo do menu novo (MainMenuRoot); em sucesso a cena já foi trocada.
            var result = await GuestPlayFlow.Start(guestGameSceneName);
            if (!result.Success && btnGuestPlay != null)
                btnGuestPlay.interactable = true;
        }
    }
}
