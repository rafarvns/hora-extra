using UnityEngine;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Arremessa o item no topo da mão, se ele for um <see cref="ThrowablePaper"/>.
    /// Mira é sempre a direção da câmera — "quanto mais distante/mal direcionado, mais
    /// difícil acertar" sai de graça da física real do arremesso, sem precisar modelar
    /// probabilidade artificial de acerto.
    ///
    /// Anexado ao prefab Player, junto do <see cref="PlayerCarrier"/> já existente.
    /// </summary>
    [RequireComponent(typeof(PlayerCarrier))]
    public class PaperThrower : MonoBehaviour
    {
        [Tooltip("Câmera usada como origem/direção da mira. Se vazia, procura nos filhos.")]
        [SerializeField] private Transform _aimOrigin;

        [Tooltip("Velocidade do arremesso na direção da câmera.")]
        [SerializeField] private float _throwForce = 12f;

        [Tooltip("Componente vertical extra, para dar um arco em vez de uma linha reta.")]
        [SerializeField] private float _throwUpwardArc = 2.5f;

        [Tooltip("Mensagem mostrada enquanto há algo arremessável na mão.")]
        [SerializeField] private string _throwMessage = "Clique para arremessar";

        [Tooltip("Prompt de mundo compartilhado. Se vazio, procura um InteractionPrompt na cena.")]
        [SerializeField] private InteractionPrompt _prompt;

        private PlayerCarrier _carrier;

        private void Awake()
        {
            _carrier = GetComponent<PlayerCarrier>();
            if (_aimOrigin == null) _aimOrigin = GetComponentInChildren<Camera>()?.transform;

            if (_aimOrigin == null)
                Debug.LogWarning($"[GAMEPLAY] PaperThrower em '{name}' sem câmera para mirar.");

            if (_prompt == null)
                _prompt = FindFirstObjectByType<InteractionPrompt>();
        }

        private void OnDisable()
        {
            _prompt?.Hide(this);
        }

        private void Update()
        {
            if (_aimOrigin == null) return;

            var throwable = _carrier.Carried?.GetComponent<ThrowablePaper>();

            // Indicador: só enquanto há algo arremessável na mão — "na mão tem uma
            // ferramenta" ou "mão vazia" não mostram a dica de clique.
            if (throwable != null && !ThrowablePaper.AnyBusy)
                _prompt?.Show(this, _throwMessage);
            else
                _prompt?.Hide(this);

            if (ThrowablePaper.AnyBusy) return; // um pacote em voo por vez (task_updated não ecoa itemId)
            if (throwable == null) return;
            if (!InteractionInput.ThrowPressed()) return;

            var top = _carrier.Carried;
            _carrier.TakeForThrow(top);
            _prompt?.Hide(this);

            Vector3 velocity = _aimOrigin.forward * _throwForce + Vector3.up * _throwUpwardArc;
            throwable.Launch(velocity);

            Debug.Log($"[GAMEPLAY] PaperThrower — arremessou '{top.ItemId}'.");
        }
    }
}
