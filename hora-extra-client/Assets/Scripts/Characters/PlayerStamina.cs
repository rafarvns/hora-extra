using UnityEngine;
using UnityEngine.UI;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Gerencia a stamina do jogador, consumo durante a corrida e recuperação.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        [Header("Configurações de Stamina")]
        [SerializeField] private float _maxStamina = 100f;
        [SerializeField] private float _currentStamina = 100f;
        [SerializeField] private float _consumptionRate = 20f; // Por segundo
        [SerializeField] private float _recoveryRate = 15f;    // Por segundo
        [SerializeField] private float _recoveryDelay = 1.5f; // Tempo após correr para começar a recuperar

        [Header("UI (Opcional)")]
        [SerializeField] private Slider _staminaSlider;
        [SerializeField] private Image _staminaImage;

        private float _lastConsumptionTime;

        public float CurrentStamina => _currentStamina;
        public float MaxStamina => _maxStamina;
        public bool CanSprint => _currentStamina > 0;

        /// <summary>
        /// Retorna true se já houver alguma referência de UI vinculada.
        /// </summary>
        public bool HasUIReference => _staminaSlider != null || _staminaImage != null;

        private void Start()
        {
            _currentStamina = _maxStamina;
            
            // Forçar configuração correta da imagem se existir
            if (_staminaImage != null)
            {
                _staminaImage.type = Image.Type.Filled;
                _staminaImage.fillMethod = Image.FillMethod.Horizontal;
                _staminaImage.fillOrigin = (int)Image.OriginHorizontal.Left; // Preenche da esquerda, então reduz da direita
            }

            UpdateUI();
        }

        private void Update()
        {
            // Recuperação se não houve consumo recente
            if (Time.time > _lastConsumptionTime + _recoveryDelay)
            {
                if (_currentStamina < _maxStamina)
                {
                    _currentStamina += _recoveryRate * Time.deltaTime;
                    _currentStamina = Mathf.Min(_currentStamina, _maxStamina);
                    UpdateUI();
                }
            }
        }

        /// <summary>
        /// Consome stamina durante o frame. Chamada pelo PlayerController.
        /// </summary>
        public void ConsumeStamina()
        {
            if (_currentStamina > 0)
            {
                float previousValue = _currentStamina;
                _currentStamina -= _consumptionRate * Time.deltaTime;
                _currentStamina = Mathf.Max(_currentStamina, 0);
                _lastConsumptionTime = Time.time;
                UpdateUI();

                // Log esporádico para não inundar o console
                if (Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[STAMINA] Consumindo: {previousValue:F1} -> {_currentStamina:F1}");
                }
            }
            else
            {
                if (Time.frameCount % 60 == 0) Debug.LogWarning("[STAMINA] Stamina ESGOTADA!");
            }
        }

        /// <summary>
        /// Atualiza os elementos de UI se estiverem atribuídos.
        /// </summary>
        private void UpdateUI()
        {
            float fillRatio = _currentStamina / _maxStamina;

            if (_staminaSlider != null)
            {
                _staminaSlider.maxValue = _maxStamina;
                _staminaSlider.value = _currentStamina;
            }

            if (_staminaImage != null)
            {
                _staminaImage.fillAmount = fillRatio;
            }
        }

        /// <summary>
        /// Define o componente de UI programaticamente.
        /// </summary>
        public void SetUI(Selectable uiElement)
        {
            if (uiElement is Slider slider) _staminaSlider = slider;
            UpdateUI();
        }

        public void SetImage(Image image)
        {
            _staminaImage = image;
            UpdateUI();
        }
    }
}
