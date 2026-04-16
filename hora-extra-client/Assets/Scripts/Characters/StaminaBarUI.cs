using UnityEngine;
using HoraExtra.Characters;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Gerencia o preenchimento visual da barra de stamina usando a largura de um RectTransform.
    /// Funciona de forma resiliente, buscando o jogador local se necessário.
    /// </summary>
    public class StaminaBarUI : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private PlayerController _playerController;
        [SerializeField, Tooltip("O objeto (Child) que representa a parte colorida preenchível da barra.")] 
        private RectTransform _staminaFill;

        private float _initialWidth;

        private void Start()
        {
            if (_staminaFill == null)
            {
                Debug.LogError($"[UI] StaminaBarUI em {gameObject.name}: Stamina Fill não atribuído no Inspector!");
                return;
            }

            _initialWidth = _staminaFill.sizeDelta.x;
            
            if (_playerController == null)
            {
                FindLocalPlayer();
            }
        }

        private void FindLocalPlayer()
        {
            _playerController = FindObjectOfType<PlayerController>();
            if (_playerController != null)
            {
                Debug.Log("[UI] StaminaBarUI: PlayerController encontrado e vinculado.");
            }
        }

        private void Update()
        {
            if (_staminaFill == null) return;

            // Busca resiliente caso o player tenha sido instanciado depois ou em outro frame
            if (_playerController == null)
            {
                FindLocalPlayer();
                return; 
            }

            UpdateBar();
        }

        private void UpdateBar()
        {
            float max = _playerController.MaxStamina;
            float current = _playerController.CurrentStamina;

            if (max <= 0) return;

            float staminaPercent = Mathf.Clamp01(current / max);

            Vector2 newSize = _staminaFill.sizeDelta;
            newSize.x = _initialWidth * staminaPercent;
            _staminaFill.sizeDelta = newSize;
        }
    }
}