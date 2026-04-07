using UnityEngine;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Classe base abstrata para todos os personagens do jogo.
    /// Define propriedades e comportamentos básicos como Vida e Movimentação.
    /// </summary>
    public abstract class CharacterBase : MonoBehaviour
    {
        [Header("Configurações Básicas")]
        [SerializeField, Tooltip("Nome de exibição do personagem.")]
        protected string _characterName;

        [SerializeField, Tooltip("Velocidade de movimento do personagem.")]
        protected float _moveSpeed = 5f;

        [Header("Status de Vida")]
        [SerializeField, Tooltip("Vida máxima permitida.")]
        protected int _maxHealth = 100;

        [SerializeField, Tooltip("Vida atual do personagem.")]
        protected int _currentHealth;

        protected bool _isDead = false;

        public string CharacterName => _characterName;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// Inicializa os status do personagem. Pode ser sobrescrito por classes filhas.
        /// </summary>
        protected virtual void Initialize()
        {
            _currentHealth = _maxHealth;
            _isDead = false;
        }

        /// <summary>
        /// Aplica dano ao personagem.
        /// </summary>
        public virtual void TakeDamage(int amount)
        {
            if (_isDead) return;

            _currentHealth -= amount;
            _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);

            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Método chamado quando a vida chega a zero.
        /// </summary>
        protected virtual void Die()
        {
            _isDead = true;
            Debug.Log($"{gameObject.name} (CharacterBase) morreu.");
        }
    }
}
