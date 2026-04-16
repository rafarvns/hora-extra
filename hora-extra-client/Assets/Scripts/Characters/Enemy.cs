using UnityEngine;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Classe genérica para inimigos hostis.
    /// Futuramente conterá agressividade e loot.
    /// </summary>
    public class Enemy : CharacterBase
    {
        [Header("Configurações do Inimigo")]
        [SerializeField, Tooltip("Dano ao tocar no jogador.")]
        protected int _contactDamage = 10;

        [SerializeField, Tooltip("Alcance de visão ou perseguição.")]
        protected float _detectionRange = 10f;

        protected override void Initialize()
        {
            base.Initialize();
            Debug.Log($"Inimigo {_characterName} pronto para atacar.");
        }

        protected override void Die()
        {
            base.Die();
            Debug.Log($"Inimigo {_characterName} eliminado.");
        }
    }
}
