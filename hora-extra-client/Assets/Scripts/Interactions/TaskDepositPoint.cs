using System.Collections.Generic;
using UnityEngine;
using HoraExtra.Audio;
using HoraExtra.Characters;
using HoraExtra.Network;
using HoraExtra.Network.Models;
using HoraExtra.UI;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Recipiente de entrega: caixa de papéis, porta-canetas, máquina de vendas, lixo,
    /// estante. Aceita um <see cref="CarryableItem"/> da categoria configurada e reporta
    /// a entrega ao servidor.
    ///
    /// O ponto central deste componente é o ida-e-volta autoritativo: apertar [E] apenas
    /// ENVIA o task_progress. O item só sai da mão quando chega o task_updated confirmando
    /// que o servidor contabilizou; se vier task_rejected, o item permanece com o jogador e
    /// a mensagem do servidor é exibida. É isso que faz "encomenda na estante errada"
    /// funcionar sem duplicar a regra de acerto no cliente.
    ///
    /// Requer um Collider com isTrigger marcado.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TaskDepositPoint : MonoBehaviour
    {
        [Header("Destino")]
        [Tooltip("Id deste destino. Vai no task_progress como slotId e, em tarefas de " +
                 "pareamento, PRECISA bater com o slotId declarado em pairs no catálogo.")]
        [SerializeField] private string _slotId;

        [Tooltip("Categoria de item que este destino aceita ('papel', 'caneta'...). " +
                 "Comparada com o _kind do CarryableItem, não com o itemId.")]
        [SerializeField] private string _acceptedKind;

        [Tooltip("Tipo (campo 'type' do catálogo) da task que este destino atende.")]
        [SerializeField] private string _taskType;

        [Header("Interação")]
        [Tooltip("Mensagem do prompt (ex: 'Aperte [E] para guardar o papel'). " +
                 "Se vazio, usa o texto de TaskPresentation para a task ativa.")]
        [SerializeField] private string _promptMessage;

        [Tooltip("Prompt de mundo compartilhado. Se vazio, procura um InteractionPrompt na cena.")]
        [SerializeField] private InteractionPrompt _prompt;

        [Tooltip("Por quantos segundos a mensagem de recusa do servidor fica na tela.")]
        [SerializeField] private float _rejectionMessageSeconds = 3f;

        [Header("Som (opcional)")]
        [Tooltip("Tocado uma vez a cada item CONFIRMADO pelo servidor. Pode ficar vazio.")]
        [SerializeField] private AudioClip _depositSound;

        [Tooltip("Volume do som de guardar.")]
        [SerializeField, Range(0f, 1f)] private float _depositVolume = 1f;

        // === Estado da entrega em voo ===
        private PlayerCarrier _carrierInRange;
        private CarryableItem _pendingItem;
        private string _pendingTaskId;
        private int _progressBeforeSend;

        /// <summary>
        /// Itens da pilha ainda não enviados. A entrega é SEQUENCIAL — um pacote por vez,
        /// esperando a confirmação antes do próximo.
        ///
        /// O motivo é do protocolo: `task_updated` não carrega `itemId` (o servidor não ecoa
        /// dado do cliente). Com N pacotes em voo, ao chegar uma confirmação não haveria como
        /// saber qual item ela confirmou. Em rede local cada ida-e-volta é de milissegundos,
        /// então a fila parece instantânea.
        /// </summary>
        private readonly Queue<CarryableItem> _dispatchQueue = new Queue<CarryableItem>();

        /// <summary>Instante até o qual o prompt normal fica suprimido (mensagem de recusa na tela).</summary>
        private float _suppressPromptUntil;

        /// <summary>True enquanto há um task_progress aguardando resposta do servidor.</summary>
        private bool IsAwaitingServer => _pendingTaskId != null;

        private void Awake()
        {
            // Basta UM collider de gatilho; o asset pode ter também um collider sólido
            // de cenário (a estante, o porta-canetas) que não deve ser confundido com ele.
            if (System.Array.Find(GetComponents<Collider>(), c => c.isTrigger) == null)
            {
                Debug.LogWarning($"[GAMEPLAY] TaskDepositPoint '{name}' não tem nenhum Collider com isTrigger — " +
                                 "o jogador nunca vai conseguir entregar aqui.");
            }

            if (string.IsNullOrWhiteSpace(_slotId))
                Debug.LogWarning($"[GAMEPLAY] TaskDepositPoint '{name}' sem _slotId — entregas de pareamento serão recusadas.");

            if (_prompt == null)
                _prompt = FindFirstObjectByType<InteractionPrompt>();
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

            ClearPending();
            _carrierInRange = null;
            _prompt?.Hide(this);
        }

        private void Update()
        {
            if (_carrierInRange == null) return;

            // Entrega em voo: não deixa reenviar nem trocar o prompt até o servidor responder.
            if (IsAwaitingServer) return;

            // Mensagem de recusa em exibição: não sobrescrever com o prompt normal. Sem isto
            // o Show() do frame seguinte apagaria o texto do servidor antes de ser lido.
            if (Time.time < _suppressPromptUntil) return;

            var task = FindActiveTask();
            if (!CanDeliver(task))
            {
                _prompt?.Hide(this);
                return;
            }

            _prompt?.Show(this, BuildPromptMessage(task));

            if (InteractionInput.InteractPressed())
                SendDelivery(task);
        }

        // === Gate de exibição / envio ===

        /// <summary>Task deste tipo atribuída ao jogador local e ainda aberta, ou null.</summary>
        private AssignedTask FindActiveTask()
        {
            var bridge = TaskSystemBridge.Instance;
            if (bridge == null) return null;
            return bridge.FindMyTask(_taskType, TaskPresentation.STATUS_PENDING, TaskPresentation.STATUS_IN_PROGRESS);
        }

        /// <summary>
        /// Três condições, todas necessárias: jogador carregando algo, da categoria certa,
        /// e com a task correspondente ativa.
        /// </summary>
        private bool CanDeliver(AssignedTask task)
        {
            if (task == null) return false;
            if (_carrierInRange == null || !_carrierInRange.IsCarrying) return false;
            return _carrierInRange.CarriedKind == _acceptedKind;
        }

        private string BuildPromptMessage(AssignedTask task)
        {
            string baseText = string.IsNullOrWhiteSpace(_promptMessage)
                ? TaskPresentation.GetDeliveryPrompt(task)
                : _promptMessage;

            // Com pilha, o jogador precisa saber quantos vai entregar de uma vez.
            int n = _carrierInRange != null ? _carrierInRange.CarriedCount : 0;
            return n > 1 ? $"{baseText} (x{n})" : baseText;
        }

        // === Envio e confirmação ===

        /// <summary>
        /// Envia o task_progress e guarda o estado necessário para reconciliar a resposta.
        /// NÃO tira o item da mão — quem faz isso é a confirmação do servidor.
        /// </summary>
        private void SendDelivery(AssignedTask task)
        {
            // Enfileira no MÁXIMO o que a task ainda precisa. Sem esse teto, entregar 4 papéis
            // numa task que só falta 1 mandaria 3 pacotes condenados: o servidor completa a
            // task no primeiro e responde INVALID_STATUS nos demais, deixando itens órfãos
            // na mão do jogador.
            int faltam = Mathf.Max(0, task.TargetCount - task.CurrentProgress);

            _dispatchQueue.Clear();
            foreach (var item in _carrierInRange.CarriedItems)
            {
                if (_dispatchQueue.Count >= faltam) break;
                if (item.Kind == _acceptedKind)
                    _dispatchQueue.Enqueue(item);
            }

            if (_dispatchQueue.Count == 0) return;

            int sobra = _carrierInRange.CarriedCount - _dispatchQueue.Count;
            if (sobra > 0)
                Debug.Log($"[GAMEPLAY] TaskDepositPoint '{_slotId}' — {sobra} item(ns) não cabe(m) nesta tarefa; use [Q] para largar.");

            Debug.Log($"[GAMEPLAY] TaskDepositPoint '{_slotId}' — entregando {_dispatchQueue.Count} item(ns) em sequência.");
            DispatchNext(task.Id, task.CurrentProgress);
        }

        /// <summary>Envia o próximo item da fila. Sem fila, encerra a entrega.</summary>
        private void DispatchNext(string taskId, int progressNow)
        {
            if (_dispatchQueue.Count == 0)
            {
                ClearPending();
                // Não esconde o prompt aqui: pode haver uma mensagem de recusa em exibição.
                // Se a entrega terminou limpa, o Update cuida de escondê-lo no próximo frame.
                return;
            }

            _pendingItem = _dispatchQueue.Dequeue();
            _pendingTaskId = taskId;
            _progressBeforeSend = progressNow;

            TaskSystemBridge.Instance.SendProgress(taskId, _pendingItem.ItemId, _slotId);
        }

        /// <summary>
        /// Confirmação autoritativa: só agora o item sai da mão. A comparação de progresso
        /// evita reagir a um task_updated de outro evento (ex: task_start_interaction) que
        /// não representa esta entrega.
        /// </summary>
        private void HandleTaskUpdated(string playerId, AssignedTask task)
        {
            if (!IsAwaitingServer) return;
            if (task == null || task.Id != _pendingTaskId) return;
            if (!IsLocalPlayer(playerId)) return;
            if (task.CurrentProgress <= _progressBeforeSend) return;

            var carrier = _carrierInRange;
            var item = _pendingItem;
            _pendingItem = null;

            if (carrier != null && item != null)
                carrier.Consume(item);

            // Toca só na confirmação, nunca no envio — som é feedback de sucesso, e no
            // envio ainda não se sabe se o servidor vai aceitar.
            InteractionAudio.PlayOneShot(_depositSound, _depositVolume);

            Debug.Log($"[GAMEPLAY] TaskDepositPoint '{_slotId}' — entrega de '{item?.ItemId}' confirmada pelo servidor.");

            // Segue para o próximo da pilha, se houver. A task já foi atualizada pelo
            // bridge antes deste callback, então CurrentProgress aqui é o valor novo.
            DispatchNext(task.Id, task.CurrentProgress);
        }

        /// <summary>
        /// Recusa de gameplay: o item volta (ou permanece) na mão e a mensagem do servidor
        /// é exibida. Nenhuma decisão de acerto acontece aqui — só apresentação.
        /// </summary>
        private void HandleTaskRejected(TaskRejectedPayload payload)
        {
            if (!IsAwaitingServer) return;
            if (payload == null || payload.TaskId != _pendingTaskId) return;

            var carrier = _carrierInRange;
            var item = _pendingItem;
            var taskId = _pendingTaskId;
            _pendingItem = null;

            if (carrier != null && item != null)
                carrier.ReturnToHand(item);

            var message = string.IsNullOrWhiteSpace(payload.Message)
                ? "Não dá para guardar isso aqui."
                : payload.Message;
            _prompt?.ShowTemporary(this, message, _rejectionMessageSeconds);
            _suppressPromptUntil = Time.time + _rejectionMessageSeconds;

            Debug.Log($"[GAMEPLAY] TaskDepositPoint '{_slotId}' — entrega recusada ({payload.Code}): {message}");

            // Um item recusado NÃO cancela o resto da pilha: se o jogador carrega 4 papéis e
            // um deles já tinha sido contado, os outros três continuam válidos.
            var task = FindActiveTask();
            if (task != null)
                DispatchNext(taskId, task.CurrentProgress);
            else
                ClearPending();
        }

        private void ClearPending()
        {
            _pendingItem = null;
            _pendingTaskId = null;
            _progressBeforeSend = 0;
            _dispatchQueue.Clear();
        }

        /// <summary>
        /// Trata playerId vazio como local: no bypass de desenvolvimento a sessão pode não
        /// expor o id, e recusar aí travaria toda entrega no Editor.
        /// </summary>
        private static bool IsLocalPlayer(string playerId)
        {
            var localId = SocketManager.Instance?.LocalPlayerId;
            if (string.IsNullOrEmpty(localId) || string.IsNullOrEmpty(playerId)) return true;
            return localId == playerId;
        }

        // === Trigger ===

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

            // Sair do trigger com um pacote em voo: a resposta ainda pode chegar, mas não
            // há mais a quem entregar. Descartar aqui evita consumir o item à distância.
            if (IsAwaitingServer)
            {
                Debug.Log($"[GAMEPLAY] TaskDepositPoint '{_slotId}' — jogador saiu antes da resposta do servidor; entrega descartada.");
                ClearPending();
            }
        }
    }
}
