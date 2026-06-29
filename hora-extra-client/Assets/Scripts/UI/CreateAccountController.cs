using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using HoraExtra.Network.Rest.Services;

namespace HoraExtra.UI
{
    /**
     * Controlador responsável pela lógica da tela de criação de conta.
     * Vincula os campos de input e o botão ao AuthService.
     */
    public class CreateAccountController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button btnCriar;

        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        private AuthService _authService;

        private void Awake()
        {
            _authService = new AuthService();
            
            if (btnCriar != null)
            {
                btnCriar.onClick.AddListener(OnCreateAccountClicked);
            }
        }

        private async void OnCreateAccountClicked()
        {
            // Desabilita o botão para evitar cliques duplos
            btnCriar.interactable = false;

            string email = emailInput.text;
            string nome = nameInput.text;
            string password = passwordInput.text;

            // Validação simples
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[CreateAccountController] Todos os campos são obrigatórios.");
                btnCriar.interactable = true;
                return;
            }

            var response = await _authService.Register(nome, email, password);

            if (response != null && response.Success)
            {
                Debug.Log("[CreateAccountController] Cadastro bem-sucedido! Redirecionando...");
                // Aqui poderíamos salvar o token no PlayerPrefs ou em um SessionManager
                PlayerPrefs.SetString("AuthToken", response.Data.Token);
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                // Reabilita o botão em caso de erro para tentar novamente
                btnCriar.interactable = true;
                
                string errorMsg = response?.Error?.Message ?? "Erro desconhecido ao cadastrar.";
                Debug.LogError($"[CreateAccountController] Falha: {errorMsg}");
            }
        }
    }
}
