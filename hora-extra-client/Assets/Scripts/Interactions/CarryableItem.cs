using UnityEngine;
using HoraExtra.Audio;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Objeto do cenário que o jogador pode pegar e carregar na "mão".
    ///
    /// Trabalha em par com <see cref="PlayerCarrier"/> (quem segura) e
    /// <see cref="TaskDepositPoint"/> (onde entregar). Este componente só cuida de
    /// "estou ao alcance e o jogador apertou [E]" — o que acontece depois é do carrier.
    ///
    /// Requer um Collider com isTrigger marcado. Os assets da entrega já vêm assim;
    /// o Awake avisa no Console quando não vier, porque um trigger faltando produz um
    /// item que simplesmente nunca responde e é chato de diagnosticar na cena.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CarryableItem : MonoBehaviour
    {
        [Header("Identidade")]
        [Tooltip("Id único do item. Vai no task_progress e PRECISA bater exatamente com o " +
                 "que está declarado em items/pairs no catálogo do TaskSystemBridge.")]
        [SerializeField] private string _itemId;

        [Tooltip("Categoria do item ('papel', 'caneta', 'rodo'...). O TaskDepositPoint " +
                 "filtra por esta categoria, não pelo itemId.")]
        [SerializeField] private string _kind;

        [Tooltip("Ferramenta: NÃO é consumida ao ser entregue, continua na mão do jogador " +
                 "(rodo, vassoura). Usado a partir do plano 0006.")]
        [SerializeField] private bool _isTool = false;

        [Header("Interação")]
        [Tooltip("Mensagem do prompt ao chegar perto (ex: 'Aperte [E] para pegar o papel').")]
        [SerializeField] private string _pickupMessage = "Aperte [E] para pegar";

        [Tooltip("Prompt de mundo compartilhado. Se vazio, procura um InteractionPrompt na cena.")]
        [SerializeField] private InteractionPrompt _prompt;

        [Header("Som (opcional)")]
        [Tooltip("Tocado uma vez ao pegar o item. Pode ficar vazio.")]
        [SerializeField] private AudioClip _pickupSound;

        [Tooltip("Volume do som de pegar.")]
        [SerializeField, Range(0f, 1f)] private float _pickupVolume = 1f;

        // === Estado ===
        private PlayerCarrier _carrierInRange;
        private Collider _collider;
        private Collider[] _colliders;

        // === Propriedades públicas ===
        public string ItemId => _itemId;
        public string Kind => _kind;
        public bool IsTool => _isTool;

        /// <summary>Escala que o item tinha na cena, restaurada ao ser largado.</summary>
        public Vector3 OriginalScale { get; private set; }

        /// <summary>Collider de gatilho do item (o que detecta o jogador).</summary>
        public Collider Collider => _collider;

        /// <summary>
        /// Liga/desliga TODOS os colliders do item.
        ///
        /// São todos, e não só o gatilho, porque vários assets trazem um collider sólido
        /// próprio (a caneta tem CapsuleCollider): deixá-lo ligado faria o item empurrar
        /// o jogador enquanto está na mão.
        /// </summary>
        public void SetCollidersEnabled(bool enabled)
        {
            if (_colliders == null) return;
            foreach (var c in _colliders)
                if (c != null) c.enabled = enabled;
        }

        private void Awake()
        {
            OriginalScale = transform.localScale;

            // Prefere o collider marcado como trigger: o asset pode ter um collider sólido
            // de cenário além do gatilho que adicionamos para a interação.
            _colliders = GetComponents<Collider>();
            _collider = System.Array.Find(_colliders, c => c.isTrigger) ?? GetComponent<Collider>();

            if (_collider == null || !_collider.isTrigger)
            {
                Debug.LogWarning($"[GAMEPLAY] CarryableItem '{name}' não tem nenhum Collider com isTrigger — " +
                                 "o jogador nunca vai conseguir pegá-lo.");
            }

            if (string.IsNullOrWhiteSpace(_itemId))
            {
                Debug.LogWarning($"[GAMEPLAY] CarryableItem '{name}' sem _itemId — o servidor vai " +
                                 "recusar a entrega com MISSING_ITEM se a task declarar items/pairs.");
            }

            if (_prompt == null)
                _prompt = FindFirstObjectByType<InteractionPrompt>();
        }

        private void Update()
        {
            // Sem jogador por perto, ou com as mãos já ocupadas, não há nada a fazer.
            if (_carrierInRange == null) return;

            // A mão pode estar cheia, ou já carregando outra categoria (não se mistura
            // papel com caneta). Nos dois casos o prompt some, para não prometer uma
            // ação que seria recusada.
            if (!_carrierInRange.CanAccept(this))
            {
                _prompt?.Hide(this);
                return;
            }

            _prompt?.Show(this, _pickupMessage);

            // Só toca se o pickup foi de fato aceito — som de ação recusada confunde.
            if (InteractionInput.InteractPressed() && _carrierInRange.TryPickup(this))
            {
                InteractionAudio.PlayOneShot(_pickupSound, _pickupVolume);

                // Some do alcance na hora. O item agora se move junto com o jogador, então
                // o OnTriggerExit pode nunca disparar e o prompt ficaria aceso oferecendo
                // pegar o que já está na mão.
                _carrierInRange = null;
                _prompt?.Hide(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            var carrier = other.GetComponentInParent<PlayerCarrier>();
            if (carrier == null) return;

            _carrierInRange = carrier;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            var carrier = other.GetComponentInParent<PlayerCarrier>();
            if (carrier == null || carrier != _carrierInRange) return;

            _carrierInRange = null;
            _prompt?.Hide(this);
        }

        private void OnDisable()
        {
            // Ao ser pego ou consumido, o objeto sai de cena sem disparar OnTriggerExit.
            // Sem isto, o prompt fica aceso apontando para um item que não está mais lá.
            _carrierInRange = null;
            _prompt?.Hide(this);
        }
    }
}
