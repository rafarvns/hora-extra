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
