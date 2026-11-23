using UnityEngine;
using HoraExtra.Characters;
using HoraExtra.Network.Models;
using HoraExtra.UI;
using HoraExtra.UI.Hud.Panels;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Computador do Financeiro com a tela de e-mail (Tarefa 7 — "Enviar arquivos/e-mails").
    ///
    /// [E] abre o <see cref="EmailUploadPanel"/> com o cursor solto e o personagem parado.
    /// Rede, no mesmo par já usado pela cafeteira:
    ///   - ao abrir, se a task estiver <c>pending</c> → <c>task_start_interaction</c>;
    ///   - barra cheia → <c>task_complete_attempt { success: true }</c>.
    /// O painel só mostra "Enviado!" quando o servidor devolve <c>completed</c>. Sem
    /// confirmação em <see cref="_segundosTimeout"/>, volta ao início para tentar de novo.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EmailComputer : MonoBehaviour
    {
        [SerializeField] private string _taskType = TaskPresentation.TYPE_SEND_EMAIL_UPLOAD;
        [SerializeField, Min(0.5f)] private float _segundosEnvio = 6f;
        [SerializeField, Min(1f)] private float _segundosTimeout = 5f;
        [SerializeField] private string _promptMessage = "Aperte [E] para usar o computador";
        [SerializeField] private InteractionPrompt _prompt;

        [Header("Som (opcional)")]
        [Tooltip("Tocado ao clicar em \"Enviar email\" (SFX_Click).")]
        [SerializeField] private AudioClip _somClique;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        /// <summary>Tipo da task. Lido pelo marcador de objetivo da HUD.</summary>
        public string TaskType => _taskType;

        private bool _jogadorPerto;
        private bool _painelAberto;
        private string _taskId;
        private float _timeoutEm = -1f;

        private void Awake()
        {
            if (System.Array.Find(GetComponents<Collider>(), c => c.isTrigger) == null)
                Debug.LogWarning($"[GAMEPLAY] EmailComputer '{name}' sem Collider com isTrigger — o jogador não vai conseguir interagir.");

            if (_prompt == null)
                _prompt = FindFirstObjectByType<InteractionPrompt>();
        }

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskUpdated += AoAtualizarTask;
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskUpdated -= AoAtualizarTask;
            if (_painelAberto) EmailUploadPanel.CloseSilently();
            _painelAberto = false;
            _timeoutEm = -1f;
            PlayerActionLock.Release(this);
            _jogadorPerto = false;
            _prompt?.Hide(this);
        }

        private void Update()
        {
            if (_painelAberto)
            {
                if (_timeoutEm > 0f && Time.unscaledTime >= _timeoutEm)
                {
                    _timeoutEm = -1f;
                    Debug.LogWarning("[GAMEPLAY] EmailComputer — servidor não confirmou o envio; voltando ao início.");
                    EmailUploadPanel.MostrarFalha("O servidor não confirmou o envio. Tente de novo.");
                }
                return;
            }

            if (!_jogadorPerto) return;

            var task = TaskAtiva();
            if (task == null)
            {
                _prompt?.Hide(this);
                return;
            }

            _prompt?.Show(this, _promptMessage);
            if (InteractionInput.InteractPressed()) Abrir(task);
        }

        private void Abrir(AssignedTask task)
        {
            if (!PlayerActionLock.Acquire(this, lockMovement: true, freeCursor: true)) return;

            _taskId = task.Id;
            _painelAberto = true;
            _prompt?.Hide(this);

            if (task.Status == TaskPresentation.STATUS_PENDING)
                TaskSystemBridge.Instance.SendStartInteraction(task.Id);

            EmailUploadPanel.Open(_segundosEnvio, AoTerminarEnvio, AoFecharPainel, _somClique, _volume);
            Debug.Log($"[GAMEPLAY] EmailComputer — tela aberta (taskId={task.Id}, status={task.Status}).");
        }

        private void AoTerminarEnvio()
        {
            var task = TaskAtiva();
            if (task == null || task.Id != _taskId)
            {
                EmailUploadPanel.MostrarFalha("Não há e-mail pendente para enviar.");
                return;
            }

            // Ainda não chegou o in_progress do start (rede lenta): o servidor recusaria.
            if (task.Status != TaskPresentation.STATUS_IN_PROGRESS)
            {
                TaskSystemBridge.Instance.SendStartInteraction(task.Id);
                EmailUploadPanel.MostrarFalha("Conectando ao servidor de e-mail... tente de novo.");
                return;
            }

            _timeoutEm = Time.unscaledTime + _segundosTimeout;
            TaskSystemBridge.Instance.SendCompleteAttempt(task.Id, true);
        }

        private void AoAtualizarTask(string playerId, AssignedTask task)
        {
            if (!_painelAberto || task == null || task.Id != _taskId) return;
            if (!EhJogadorLocal(playerId)) return;
            if (task.Status != TaskPresentation.STATUS_COMPLETED) return;

            _timeoutEm = -1f;
            EmailUploadPanel.MostrarConcluido();
            Debug.Log($"[GAMEPLAY] EmailComputer — envio confirmado pelo servidor (taskId={task.Id}).");
        }

        private void AoFecharPainel()
        {
            _painelAberto = false;
            _timeoutEm = -1f;
            PlayerActionLock.Release(this);
        }

        private static bool EhJogadorLocal(string playerId)
        {
            var localId = SocketManager.Instance?.LocalPlayerId;
            if (string.IsNullOrEmpty(localId) || string.IsNullOrEmpty(playerId)) return true;
            return localId == playerId;
        }

        private AssignedTask TaskAtiva()
        {
            var bridge = TaskSystemBridge.Instance;
            if (bridge == null) return null;
            return bridge.FindMyTask(_taskType, TaskPresentation.STATUS_PENDING, TaskPresentation.STATUS_IN_PROGRESS);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) _jogadorPerto = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _jogadorPerto = false;
            if (!_painelAberto) _prompt?.Hide(this);
        }
    }
}
