using System.Collections.Generic;
using UnityEngine;
using HoraExtra.Audio;
using HoraExtra.Characters;
using HoraExtra.Network.Models;
using HoraExtra.UI;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Lixeira: detecta um <see cref="ThrowablePaper"/> em voo que caiu dentro dela e reporta
    /// o acerto ao servidor via task_progress. Estrutura espelhada em
    /// <see cref="TaskDepositPoint"/> (mesma fila sequencial, mesmo motivo: o task_updated
    /// não ecoa qual itemId foi confirmado, então só dá para ter um pacote em voo por vez).
    ///
    /// Diferente do TaskDepositPoint, não há interação por [E] — a detecção é 100% física
    /// (OnTriggerEnter do papel em voo). Papel parado ou na mão (InFlight == false) nunca conta.
    ///
    /// Requer um segundo Collider marcado isTrigger além do sólido já existente no móvel
    /// (o sólido continua bloqueando o jogador; o trigger é só para detectar o papel).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TrashBin : MonoBehaviour
    {
        [Header("Destino")]
        [Tooltip("Id desta lixeira. Vai no task_progress como slotId (sem efeito de validação, " +
                 "pois a task não declara pairs — qualquer lixeira aceita qualquer papel).")]
        [SerializeField] private string _slotId;

        [Tooltip("Categoria de item aceita. Comparada com CarryableItem.Kind.")]
        [SerializeField] private string _acceptedKind = "papel_lixo";

        [Tooltip("Tipo (campo 'type' do catálogo) da task que esta lixeira atende.")]
        [SerializeField] private string _taskType = "throw_trash_papers";

        [Header("Som (opcional)")]
        [Tooltip("Tocado uma vez a cada papel CONFIRMADO pelo servidor. Pode ficar vazio.")]
        [SerializeField] private AudioClip _hitSound;

        [Tooltip("Volume do som de acerto.")]
        [SerializeField, Range(0f, 1f)] private float _hitVolume = 1f;

        /// <summary>Categoria aceita por esta lixeira. Lido pelo indicador de objetivo (HUD).</summary>
        public string AcceptedKind => _acceptedKind;

        /// <summary>Tipo de task que esta lixeira atende. Lido pelo indicador de objetivo (HUD).</summary>
        public string TaskType => _taskType;

        private readonly Queue<ThrowablePaper> _pending = new Queue<ThrowablePaper>();
        private string _pendingTaskId;
        private int _progressBeforeSend;

        private bool IsAwaitingServer => _pendingTaskId != null;

        private void Awake()
        {
            if (System.Array.Find(GetComponents<Collider>(), c => c.isTrigger) == null)
            {
                Debug.LogWarning($"[GAMEPLAY] TrashBin '{name}' não tem nenhum Collider com isTrigger — " +
                                 "nunca vai detectar um papel caindo dentro.");
            }
        }

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskUpdated += HandleTaskUpdated;
            TaskSystemBridge.OnTaskRejected += HandleTaskRejected;
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskUpdated -= HandleTaskUpdated;
            TaskSystemBridge.OnTaskRejected -= HandleTaskRejected;
            _pending.Clear();
            _pendingTaskId = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            var paper = other.GetComponentInParent<ThrowablePaper>();
            if (paper == null || !paper.InFlight) return;

            var carryable = paper.GetComponent<CarryableItem>();
            if (carryable == null || carryable.Kind != _acceptedKind) return;

            paper.MarkCaught();
            _pending.Enqueue(paper);
            Debug.Log($"[GAMEPLAY] TrashBin '{_slotId}' — papel '{carryable.ItemId}' capturado em voo.");
            DispatchNext();
        }

        private void DispatchNext()
        {
            if (IsAwaitingServer || _pending.Count == 0) return;

            var bridge = TaskSystemBridge.Instance;
            var task = bridge?.FindMyTask(_taskType, TaskPresentation.STATUS_PENDING, TaskPresentation.STATUS_IN_PROGRESS);
            if (task == null)
            {
                // Sem task ativa deste tipo: nada a reportar, mas o papel já foi capturado
                // fisicamente — devolve ao chão em vez de ficar suspenso para sempre.
                _pending.Dequeue().ReturnToFloor();
                DispatchNext();
                return;
            }

            var paper = _pending.Peek();
            var carryable = paper.GetComponent<CarryableItem>();

            _pendingTaskId = task.Id;
            _progressBeforeSend = task.CurrentProgress;

            bridge.SendProgress(task.Id, carryable.ItemId, _slotId);
        }

        private void HandleTaskUpdated(string playerId, AssignedTask task)
        {
            if (!IsAwaitingServer || _pending.Count == 0) return;
            if (task == null || task.Id != _pendingTaskId) return;
            if (task.CurrentProgress <= _progressBeforeSend) return;

            var paper = _pending.Dequeue();
            _pendingTaskId = null;

            // Toca antes de desativar: o próprio papel some no mesmo frame (SetActive(false)
            // cortaria um AudioSource nele) — InteractionAudio é compartilhado e sobrevive.
            InteractionAudio.PlayOneShot(_hitSound, _hitVolume);
            paper.gameObject.SetActive(false); // consumido, igual ao Consume() do PlayerCarrier

            Debug.Log($"[GAMEPLAY] TrashBin '{_slotId}' — entrega confirmada pelo servidor ({task.CurrentProgress}/{task.TargetCount}).");

            DispatchNext();
        }

        private void HandleTaskRejected(TaskRejectedPayload payload)
        {
            if (!IsAwaitingServer || _pending.Count == 0) return;
            if (payload == null || payload.TaskId != _pendingTaskId) return;

            var paper = _pending.Dequeue();
            _pendingTaskId = null;
            paper.ReturnToFloor();

            Debug.Log($"[GAMEPLAY] TrashBin '{_slotId}' — entrega recusada ({payload.Code}): {payload.Message}");

            DispatchNext();
        }
    }
}
