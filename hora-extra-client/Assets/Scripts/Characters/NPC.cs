using UnityEngine;
using HoraExtra.Network;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Classe para personagens não-jogáveis amigáveis.
    /// Futuramente poderá gerenciar falas e missões.
    /// </summary>
    [RequireComponent(typeof(NetworkEntity))]
    public class NPC : CharacterBase
    {
        [Header("Configurações do NPC")]
        [SerializeField, Tooltip("O NPC pode ser ferido por acidente?")]
        private bool _canBeHurt = false;

        protected override void Initialize()
        {
            base.Initialize();
            Debug.Log($"NPC {_characterName} pronto para interagir.");
        }

        public override void TakeDamage(int amount)
        {
            if (!_canBeHurt) return;

            base.TakeDamage(amount);
            Debug.Log($"NPC {_characterName} tomou dano incidental.");
        }

        protected override void Die()
        {
            base.Die();
            Debug.Log($"NPC {_characterName} foi neutralizado (por erro ou tragédia).");
        }
    }
}
