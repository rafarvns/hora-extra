using UnityEngine;
using HoraExtra.Audio;
using HoraExtra.Characters;
using HoraExtra.Network;
using HoraExtra.Network.Models;
using HoraExtra.UI;
using HoraExtra.UI.Hud.Panels;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Cofre do Financeiro (Tarefa 6 — "Desbloquear os cofres e recolher o dinheiro").
    ///
    /// Fluxo: [E] no cofre trancado abre o <see cref="SafeKeypadPanel"/>; a senha certa abre
    /// a porta (<see cref="SafeDoor"/>). [E] no cofre aberto deixa o personagem ocupado por
    /// ~10 s recolhendo as notas (sem andar, sem outras ações — <see cref="PlayerActionLock"/>)
    /// e no fim envia <c>task_progress</c> com o <c>itemId</c> deste cofre.
    ///
    /// A senha é conferida no cliente (decisão de escopo: o servidor só conta o dinheiro
    /// recolhido, validando item e duplicidade). O dinheiro só some quando o servidor
    /// confirma — mesmo padrão otimista do <see cref="DirtSpot"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SafeInteraction : MonoBehaviour
    {
        private enum Estado { Trancado, DigitandoSenha, Aberto, Recolhendo, AguardandoServidor, Vazio }

        [Header("Identidade")]
        [Tooltip("Id deste cofre. Precisa bater com 'items' no catálogo (cofre-direito / cofre-esquerdo).")]
        [SerializeField] private string _itemId = "cofre-direito";

        [Tooltip("Senha do cofre (o post-it perto dele). Direita = 1512, esquerda = 2803.")]
        [SerializeField] private string _codigo = "1512";

        [Tooltip("Tipo da task que este cofre atende.")]
        [SerializeField] private string _taskType = TaskPresentation.TYPE_OPEN_SAFE_COLLECT_MONEY;

        [Tooltip("Enviado como slotId; só para log, não é validado pelo servidor.")]
        [SerializeField] private string _slotId = "cofre";

        [Header("Cena")]
        [SerializeField] private SafeDoor _porta;

        [Tooltip("Notas/moedas dentro do cofre. Somem quando o servidor confirma o recolhimento.")]
        [SerializeField] private GameObject[] _dinheiroVisual = new GameObject[0];

        [Header("Interação")]
        [SerializeField, Min(0.5f)] private float _segundosRecolhendo = 10f;
        [SerializeField] private string _promptTrancado = "Aperte [E] para digitar a senha do cofre";
        [SerializeField] private string _promptAberto = "Aperte [E] para recolher o dinheiro";
        [SerializeField] private InteractionPrompt _prompt;

        [Header("Som (opcional)")]
        [SerializeField] private AudioClip _somRecolher;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        /// <summary>Tipo da task. Lido pelo marcador de objetivo da HUD.</summary>
        public string TaskType => _taskType;

        /// <summary>True enquanto ainda há o que fazer neste cofre (marcador de objetivo).</summary>
        public bool Pendente => _estado != Estado.Vazio;

        private Estado _estado = Estado.Trancado;
        private bool _jogadorPerto;
        private float _progresso;
        private string _taskIdEnviada;
        private int _progressoAntesDoEnvio;

        private void Awake()
        {
            if (System.Array.Find(GetComponents<Collider>(), c => c.isTrigger) == null)
                Debug.LogWarning($"[GAMEPLAY] SafeInteraction '{name}' sem Collider com isTrigger — o jogador não vai conseguir interagir.");

            if (string.IsNullOrWhiteSpace(_itemId))
                Debug.LogWarning($"[GAMEPLAY] SafeInteraction '{name}' sem _itemId — o servidor vai recusar com MISSING_ITEM.");

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

            if (_estado == Estado.DigitandoSenha)
            {
                SafeKeypadPanel.CloseSilently();
                _estado = Estado.Trancado;
            }
            if (_estado == Estado.Recolhendo) _estado = Estado.Aberto;

            PlayerActionLock.Release(this);
            _jogadorPerto = false;
            _progresso = 0f;
            _prompt?.Hide(this);
        }

        private void Update()
        {
            switch (_estado)
            {
                case Estado.Recolhendo:
                    AtualizarRecolhimento();
                    return;
                case Estado.DigitandoSenha:
                case Estado.AguardandoServidor:
                case Estado.Vazio:
                    return;
            }

            if (!_jogadorPerto) return;

            var task = TaskAtiva();
            if (task == null)
            {
                _prompt?.Hide(this);
                return;
            }

            _prompt?.Show(this, _estado == Estado.Trancado ? _promptTrancado : _promptAberto);

            if (!InteractionInput.InteractPressed()) return;

            if (_estado == Estado.Trancado) AbrirTeclado();
            else IniciarRecolhimento();
        }

        // === Senha ===

        private void AbrirTeclado()
        {
            if (!PlayerActionLock.Acquire(this, lockMovement: true, freeCursor: false)) return;

            _estado = Estado.DigitandoSenha;
            _prompt?.Hide(this);

            SafeKeypadPanel.Open("COFRE", Mathf.Max(1, _codigo.Length),
                validar: digitado => digitado == _codigo,
                aoAbrir: AoAcertarSenha,
                aoCancelar: AoCancelarTeclado);
        }

        private void AoAcertarSenha()
        {
            PlayerActionLock.Release(this);
            _estado = Estado.Aberto;
            if (_porta != null) _porta.Abrir();
            Debug.Log($"[GAMEPLAY] Cofre '{_itemId}' destravado.");
        }

        private void AoCancelarTeclado()
        {
            PlayerActionLock.Release(this);
            _estado = Estado.Trancado;
        }

        // === Recolhimento ===

        private void IniciarRecolhimento()
        {
            if (!PlayerActionLock.Acquire(this, lockMovement: true, freeCursor: false)) return;

            _estado = Estado.Recolhendo;
            _progresso = 0f;
            InteractionAudio.PlayOneShot(_somRecolher, _volume);
            Debug.Log($"[GAMEPLAY] Cofre '{_itemId}' — recolhendo o dinheiro ({_segundosRecolhendo:0.#} s).");
        }

        private void AtualizarRecolhimento()
        {
            // A task pode ter sumido no meio (reset da sala, por ex.): solta o jogador.
            var task = TaskAtiva();
            if (task == null)
            {
                _estado = Estado.Aberto;
                _progresso = 0f;
                PlayerActionLock.Release(this);
                _prompt?.Hide(this);
                return;
            }

            _progresso += Time.deltaTime;
            _prompt?.ShowProgress(this, "Recolhendo o dinheiro...", _progresso / _segundosRecolhendo);

            if (_progresso >= _segundosRecolhendo)
                Enviar(task);
        }

        private void Enviar(AssignedTask task)
        {
            _estado = Estado.AguardandoServidor;
            _taskIdEnviada = task.Id;
            _progressoAntesDoEnvio = task.CurrentProgress;
            _progresso = 0f;

            TaskSystemBridge.Instance.SendProgress(task.Id, _itemId, _slotId);
        }

        /// <summary>Confirmação do servidor: só agora o dinheiro some e o jogador é liberado.</summary>
        private void AoAtualizarTask(string playerId, AssignedTask task)
        {
            if (_estado != Estado.AguardandoServidor) return;
            if (task == null || task.Id != _taskIdEnviada) return;
            if (!EhJogadorLocal(playerId)) return;
            if (task.CurrentProgress <= _progressoAntesDoEnvio) return;

            _estado = Estado.Vazio;
            PlayerActionLock.Release(this);
            _prompt?.Hide(this);

            foreach (var d in _dinheiroVisual)
                if (d != null) d.SetActive(false);

            Debug.Log($"[GAMEPLAY] Cofre '{_itemId}' — recolhimento confirmado pelo servidor ({task.CurrentProgress}/{task.TargetCount}).");
        }

        private void AoRecusar(TaskRejectedPayload payload)
        {
            if (_estado != Estado.AguardandoServidor) return;
            if (payload == null || payload.TaskId != _taskIdEnviada) return;

            // Já contado = outro envio deste cofre chegou antes; trata como vazio.
            bool jaContado = payload.Code == "ALREADY_COUNTED";
            _estado = jaContado ? Estado.Vazio : Estado.Aberto;
            PlayerActionLock.Release(this);

            var msg = string.IsNullOrWhiteSpace(payload.Message) ? "Não dá para recolher agora." : payload.Message;
            _prompt?.ShowTemporary(this, msg, 3f);
            Debug.Log($"[GAMEPLAY] Cofre '{_itemId}' — recolhimento recusado ({payload.Code}): {msg}");
        }

        // === Auxiliares ===

        private AssignedTask TaskAtiva()
        {
            var bridge = TaskSystemBridge.Instance;
            if (bridge == null) return null;
            return bridge.FindMyTask(_taskType, TaskPresentation.STATUS_PENDING, TaskPresentation.STATUS_IN_PROGRESS);
        }

        private static bool EhJogadorLocal(string playerId)
        {
            var localId = SocketManager.Instance?.LocalPlayerId;
            if (string.IsNullOrEmpty(localId) || string.IsNullOrEmpty(playerId)) return true;
            return localId == playerId;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) _jogadorPerto = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _jogadorPerto = false;
            if (_estado == Estado.Trancado || _estado == Estado.Aberto) _prompt?.Hide(this);
        }
    }
}
