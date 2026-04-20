using UnityEngine;
using HoraExtra.Network;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Classe especializada para chefes de fase.
    /// Herda de Enemy, possuindo atributos de inimigos e específicos de Boss.
    /// </summary>
    [RequireComponent(typeof(NetworkEntity))]
    public class Boss : Enemy
    {
        [Header("Configurações do Chefe")]
        [SerializeField, Tooltip("Threshold de vida para troca de fases (em percentual).")]
        private float _nextPhaseThresholdPercent = 0.5f;

        [SerializeField, Tooltip("Dano de habilidade especial.")]
        private int _ultimateDamage = 50;

        private int _currentPhase = 1;

        protected override void Initialize()
        {
            base.Initialize();
            _currentPhase = 1;
            Debug.Log($"CHEFE {_characterName} entrou na arena. Fase {_currentPhase}.");
        }

        public override void TakeDamage(int amount)
        {
            base.TakeDamage(amount);

            // Verificação simples de troca de fase.
            if (_currentPhase == 1 && _currentHealth <= _maxHealth * _nextPhaseThresholdPercent)
            {
                AdvancePhase();
            }
        }

        private void AdvancePhase()
        {
            _currentPhase++;
            Debug.Log($"O CHEFE {_characterName} avançou para a Fase {_currentPhase}!");
            // Futuramente aplicar efeitos visuais e buffs.
        }

        protected override void Die()
        {
            base.Die();
            Debug.Log($"CHEFE {_characterName} derrotado. Vitória conquistada!");
        }
    }
}
