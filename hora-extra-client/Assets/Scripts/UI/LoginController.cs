using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using HoraExtra.Network.Rest.Services;
using HoraExtra.Network;

namespace HoraExtra.UI
{
    /**
     * Controlador responsável pela lógica da tela de Login.
     */
    public class LoginController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button btnLogin;

        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        private AuthService _authService;

        private void Awake()
        {
            _authService = new AuthService();
            
            if (btnLogin != null)
            {
                btnLogin.onClick.AddListener(OnLoginClicked);
            }
        }

        private async void OnLoginClicked()
        {
            btnLogin.interactable = false;

            string email = emailInput.text;
            string password = passwordInput.text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[LoginController] Email e Senha são obrigatórios.");
                btnLogin.interactable = true;
                return;
            }

            var response = await _authService.Login(email, password);

            if (response != null && response.Success)
            {
                Debug.Log("[LoginController] Login bem-sucedido!");
                
                // Salva na sessão global
                if (SessionManager.Instance != null)
                {
                    SessionManager.Instance.SetSession(response.Data);
                }

                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                btnLogin.interactable = true;
                string errorMsg = response?.Error?.Message ?? "E-mail ou senha inválidos.";
                Debug.LogError($"[LoginController] Falha: {errorMsg}");
            }
        }
    }
}
