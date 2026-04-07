using UnityEngine;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Classe específica para o jogador.
    /// Futuramente conterá lógica de input, inventário e experiências.
    /// </summary>
    public class Player : CharacterBase
    {
        [Header("Componentes de Controle")]
        [SerializeField] private PlayerController _playerController;

        [Header("Configurações do Jogador")]
        [SerializeField, Tooltip("Duração da invencibilidade após sofrer dano.")]
        private float _invincibilityDuration = 1.0f;

        protected override void Awake()
        {
            base.Awake();
            if (_playerController == null)
            {
                _playerController = GetComponent<PlayerController>();
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
            Debug.Log($"Jogador {_characterName} pronto para a ação.");
        }

        public override void TakeDamage(int amount)
        {
            // O jogador pode ter propriedades específicas de redução de dano.
            base.TakeDamage(amount);
            Debug.Log($"Jogador {_characterName} recebeu {amount} de dano.");
        }

        protected override void Die()
        {
            base.Die();
            if (_playerController != null)
            {
                _playerController.ToggleControl(false);
            }
            Debug.Log($"Jogador {_characterName} foi derrotado. Iniciando respawn ou Game Over.");
        }
    }
}
