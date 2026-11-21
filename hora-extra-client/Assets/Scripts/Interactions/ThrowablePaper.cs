using System.Collections;
using UnityEngine;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Torna um <see cref="CarryableItem"/> arremessável: sai da mão como projétil físico
    /// e, se não for capturado por uma <see cref="TrashBin"/> em voo, pousa no chão e volta
    /// a ficar pegável normalmente (fluxo "errou, recolhe, tenta de novo").
    ///
    /// O collider de pickup (trigger) do CarryableItem só detecta o jogador — não gera
    /// resposta física contra o chão. Por isso este componente cria um segundo collider,
    /// sólido, usado só durante o voo.
    /// </summary>
    [RequireComponent(typeof(CarryableItem))]
    public class ThrowablePaper : MonoBehaviour
    {
        [Tooltip("Velocidade abaixo da qual o papel é considerado parado (pousou).")]
        [SerializeField] private float _restVelocityThreshold = 0.05f;

        [Tooltip("Tempo mínimo em voo antes de começar a checar se já pousou — evita " +
                 "detectar 'parado' no primeiro frame, antes da força ser aplicada.")]
        [SerializeField] private float _restCheckDelay = 0.3f;

        /// <summary>True entre o arremesso e o papel ser capturado/pousar — é o que a TrashBin confere.</summary>
        public bool InFlight { get; private set; }

        /// <summary>
        /// True entre Launch() e a resolução (pousou ou foi consumido). Única salvaguarda
        /// contra dois arremessos em voo ao mesmo tempo: o protocolo task_updated não ecoa
        /// itemId, então duas confirmações em voo seriam ambíguas.
        /// </summary>
        public static bool AnyBusy { get; private set; }

        private Rigidbody _rigidbody;
        private Collider _pickupTrigger;
        private BoxCollider _flightCollider;

        // Separado de InFlight porque MarkCaught() já zera InFlight antes de o papel ser
        // consumido (SetActive(false)) — sem este campo, AnyBusy nunca seria liberado após
        // um acerto confirmado, travando todo arremesso futuro.
        private bool _isBusy;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null) _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.isKinematic = true;

            _pickupTrigger = System.Array.Find(GetComponents<Collider>(), c => c.isTrigger);
            if (_pickupTrigger == null)
                Debug.LogWarning($"[GAMEPLAY] ThrowablePaper '{name}' sem Collider trigger de pickup.");

            _flightCollider = gameObject.AddComponent<BoxCollider>();
            _flightCollider.isTrigger = false;
            _flightCollider.enabled = false;
            if (_pickupTrigger is BoxCollider pickupBox)
            {
                _flightCollider.center = pickupBox.center;
                _flightCollider.size = pickupBox.size;
            }
        }

        private void OnDisable()
        {
            if (_isBusy)
            {
                _isBusy = false;
                AnyBusy = false;
            }
            InFlight = false;
        }

        /// <summary>Solta o papel da mão e aplica a velocidade do arremesso (mira).</summary>
        public void Launch(Vector3 velocity)
        {
            StopAllCoroutines();
            transform.SetParent(null, true);

            if (_pickupTrigger != null) _pickupTrigger.enabled = false;
            _flightCollider.enabled = true;
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = velocity;

            InFlight = true;
            _isBusy = true;
            AnyBusy = true;

            StartCoroutine(WaitForLanding());
        }

        /// <summary>A TrashBin detectou o papel em voo — congela no lugar até o servidor confirmar.</summary>
        public void MarkCaught()
        {
            StopAllCoroutines();
            InFlight = false;
            _rigidbody.isKinematic = true;
        }

        /// <summary>
        /// Pousou (errou a lixeira) ou o servidor recusou a entrega — volta a ficar pegável
        /// exatamente onde está.
        /// </summary>
        public void ReturnToFloor()
        {
            StopAllCoroutines();
            InFlight = false;
            if (_isBusy)
            {
                _isBusy = false;
                AnyBusy = false;
            }
            _rigidbody.isKinematic = true;
            _flightCollider.enabled = false;
            if (_pickupTrigger != null) _pickupTrigger.enabled = true;
        }

        private IEnumerator WaitForLanding()
        {
            yield return new WaitForSeconds(_restCheckDelay);
            while (_rigidbody.linearVelocity.magnitude > _restVelocityThreshold)
                yield return null;
            ReturnToFloor();
        }
    }
}
