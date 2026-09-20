using UnityEngine;
using HoraExtra.Audio;
using HoraExtra.Characters;
using HoraExtra.Network;
using HoraExtra.Network.Models;
using HoraExtra.UI;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Sujeira no chão limpa com uma ferramenta, segurando [E].
    ///
    /// Diferente do <see cref="TaskDepositPoint"/>, aqui nada sai da mão: a ferramenta
    /// (rodo, vassoura) tem <c>_isTool = true</c> e permanece com o jogador para limpar a
    /// próxima. O que é consumido é a sujeira.
    ///
    /// Vale a mesma regra autoritativa do resto: o [E] só ENVIA o task_progress; a mancha
    /// some quando o servidor confirma. Soltar o [E] no meio zera o progresso.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DirtSpot : MonoBehaviour
    {
        [Header("Identidade")]
        [Tooltip("Id único desta sujeira. Precisa bater com 'items' no catálogo.")]
        [SerializeField] private string _itemId;

        [Tooltip("Categoria da ferramenta que limpa esta sujeira (ex: 'rodo', 'vassoura').")]
        [SerializeField] private string _requiredKind = "rodo";

        [Tooltip("Tipo da task que esta sujeira atende.")]
        [SerializeField] private string _taskType;

        [Tooltip("Enviado como slotId; usado para log/telemetria, não é validado pelo servidor.")]
        [SerializeField] private string _slotId = "chao";

        [Header("Interação")]
        [Tooltip("Segundos de [E] segurado para limpar.")]
        [SerializeField, Min(0.1f)] private float _segundosParaLimpar = 1.8f;

        [Tooltip("Mensagem ao chegar perto com a ferramenta certa.")]
        [SerializeField] private string _promptMessage = "Segure [E] para limpar";

        [Tooltip("Mensagem quando falta a ferramenta.")]
        [SerializeField] private string _promptSemFerramenta = "Você precisa do rodo para limpar isso";

        [SerializeField] private InteractionPrompt _prompt;

        [Header("Som (opcional)")]
        [SerializeField] private AudioClip _somLimpeza;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        // === Estado ===
        private PlayerCarrier _carrierInRange;
        private float _progresso;
        private bool _aguardandoServidor;
        private string _taskIdEnviada;
        private int _progressoAntesDoEnvio;

        private void Awake()
        {
            if (System.Array.Find(GetComponents<Collider>(), c => c.isTrigger) == null)
                Debug.LogWarning($"[GAMEPLAY] DirtSpot '{name}' sem Collider com isTrigger — não será possível limpar.");

            if (string.IsNullOrWhiteSpace(_itemId))
                Debug.LogWarning($"[GAMEPLAY] DirtSpot '{name}' sem _itemId — o servidor vai recusar com MISSING_ITEM.");

            if (_prompt == null)
                _prompt = FindFirstObjectByType<InteractionPrompt>();
        }

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskUpdated += AoAtualizarTask;
            TaskSystemBridge.OnTaskRejected += AoRecusar;
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskUpdated -= AoAtualizarTask;
            TaskSystemBridge.OnTaskRejected -= AoRecusar;
            _carrierInRange = null;
            _progresso = 0f;
            _aguardandoServidor = false;
            _prompt?.Hide(this);
        }

        private void Update()
        {
            if (_carrierInRange == null || _aguardandoServidor) return;

            var task = TaskAtiva();
            if (task == null)
            {
                _prompt?.Hide(this);
                return;
            }

            // Sem a ferramenta certa, diz o que falta em vez de ficar mudo.
            if (_carrierInRange.CarriedKind != _requiredKind)
            {
                _progresso = 0f;
                _prompt?.Show(this, _promptSemFerramenta);
                return;
            }

            if (InteractionInput.InteractHeld())
            {
                _progresso += Time.deltaTime;
                int pct = Mathf.Clamp(Mathf.RoundToInt(_progresso / _segundosParaLimpar * 100f), 0, 100);
                _prompt?.Show(this, $"Limpando... {pct}%");

                if (_progresso >= _segundosParaLimpar)
                    Enviar(task);
            }
            else
            {
                // Soltou: recomeça do zero. Limpar exige um gesto contínuo, não toquinhos.
                if (_progresso > 0f) _progresso = 0f;
                _prompt?.Show(this, _promptMessage);
            }
        }

        private AssignedTask TaskAtiva()
        {
            var bridge = TaskSystemBridge.Instance;
            if (bridge == null) return null;
            return bridge.FindMyTask(_taskType, TaskPresentation.STATUS_PENDING, TaskPresentation.STATUS_IN_PROGRESS);
        }

        private void Enviar(AssignedTask task)
        {
            _aguardandoServidor = true;
            _taskIdEnviada = task.Id;
            _progressoAntesDoEnvio = task.CurrentProgress;
            _progresso = 0f;

            TaskSystemBridge.Instance.SendProgress(task.Id, _itemId, _slotId);
        }

        /// <summary>Confirmação do servidor: só agora a mancha some.</summary>
        private void AoAtualizarTask(string playerId, AssignedTask task)
        {
            if (!_aguardandoServidor) return;
            if (task == null || task.Id != _taskIdEnviada) return;
            if (!EhJogadorLocal(playerId)) return;
            if (task.CurrentProgress <= _progressoAntesDoEnvio) return;

            _aguardandoServidor = false;
            _prompt?.Hide(this);
            InteractionAudio.PlayOneShot(_somLimpeza, _volume);

            Debug.Log($"[GAMEPLAY] DirtSpot '{_itemId}' — limpeza confirmada pelo servidor.");
            gameObject.SetActive(false);
        }

        private void AoRecusar(TaskRejectedPayload payload)
        {
            if (!_aguardandoServidor) return;
            if (payload == null || payload.TaskId != _taskIdEnviada) return;

            _aguardandoServidor = false;
            var msg = string.IsNullOrWhiteSpace(payload.Message) ? "Não dá para limpar isso agora." : payload.Message;
            _prompt?.ShowTemporary(this, msg, 3f);
            Debug.Log($"[GAMEPLAY] DirtSpot '{_itemId}' — limpeza recusada ({payload.Code}): {msg}");
        }

        private static bool EhJogadorLocal(string playerId)
        {
            var localId = SocketManager.Instance?.LocalPlayerId;
            if (string.IsNullOrEmpty(localId) || string.IsNullOrEmpty(playerId)) return true;
            return localId == playerId;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            var carrier = other.GetComponentInParent<PlayerCarrier>();
            if (carrier != null) _carrierInRange = carrier;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            var carrier = other.GetComponentInParent<PlayerCarrier>();
            if (carrier == null || carrier != _carrierInRange) return;

            _carrierInRange = null;
            _progresso = 0f;
            _prompt?.Hide(this);
        }
    }
}
