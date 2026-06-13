using UnityEngine;
using UnityEngine.UI;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Minigame de timing Quick Time Event (QTE) para a tarefa da cafeteira.
    ///
    /// Mecânica:
    ///   - Recebe targetCount (número de rounds) de CoffeeMakerInteraction.
    ///   - Cada round exibe um indicador visual (barra de progresso) que se preenche durante
    ///     uma janela de tempo (_windowDuration segundos).
    ///   - O jogador deve pressionar a tecla de ação (_qteKey) dentro da janela verde
    ///     (_hitZoneStart a _hitZoneEnd, normalizados 0-1) para acertar o round.
    ///   - Acerto: avança pro próximo round; log "[GAMEPLAY] CoffeeQTE — round N/total acertado".
    ///   - Erro (fora da janela ou tempo expirado): chama TaskSystemBridge.SendCompleteAttempt(false);
    ///     log "[GAMEPLAY] CoffeeQTE — round N falhou". QTE é encerrado imediatamente.
    ///   - Após todos os rounds: chama TaskSystemBridge.SendCompleteAttempt(true).
    ///
    /// Setup no Prefab PFB_Interactable_CoffeeMaker:
    ///   - _qteCanvas: canvas filho com o painel do QTE (ativado/desativado pelo QTE).
    ///   - _progressBar: Image com ImageType=Filled para indicar o progresso dentro da janela.
    ///   - _roundText: TextMesh Pro ou UI.Text para exibir "Round N/Total".
    ///   - _hitZoneIndicator: RectTransform do indicador de zona de acerto (opcional, posicionado
    ///     via código baseado em _hitZoneStart/_hitZoneEnd).
    ///
    /// Todos os campos são opcionais no Inspector — o minigame funciona sem UI, só sem feedback visual.
    /// </summary>
    public class CoffeeQTE : MonoBehaviour
    {
        // === Constantes ===
        private const float DEFAULT_WINDOW_DURATION = 2f;
        private const float DEFAULT_HIT_ZONE_START  = 0.55f;
        private const float DEFAULT_HIT_ZONE_END    = 0.85f;

        // === Inspector ===
        [Header("UI References")]
        [Tooltip("Canvas filho que contém o painel do QTE. Ativado ao iniciar, desativado ao encerrar.")]
        [SerializeField] private GameObject _qteCanvas;

        [Tooltip("Image com ImageType=Filled; fillAmount de 0 a 1 acompanha o progresso da janela.")]
        [SerializeField] private Image _progressBar;

        [Tooltip("Texto que exibe 'Round N/Total' durante o QTE (pode ser null).")]
        [SerializeField] private Text _roundText;

        [Tooltip("RectTransform do indicador de zona verde de acerto (pode ser null).")]
        [SerializeField] private RectTransform _hitZoneIndicator;

        [Header("QTE Settings")]
        [Tooltip("Duração em segundos de cada janela de timing.")]
        [SerializeField] private float _windowDuration = DEFAULT_WINDOW_DURATION;

        [Tooltip("Início normalizado (0-1) da zona de acerto dentro da janela.")]
        [SerializeField] private float _hitZoneStart = DEFAULT_HIT_ZONE_START;

        [Tooltip("Fim normalizado (0-1) da zona de acerto dentro da janela.")]
        [SerializeField] private float _hitZoneEnd = DEFAULT_HIT_ZONE_END;

        [Header("Input")]
        [Tooltip("Tecla pressionada durante o QTE para tentar acertar.")]
        [SerializeField] private KeyCode _qteKey = KeyCode.E;

        // === Estado privado ===
        private bool   _qteRunning  = false;
        private int    _currentRound;
        private int    _totalRounds;
        private string _taskId;
        private float  _roundTimer;
        private bool   _inputHandledThisRound;

        // === Lifecycle ===

        private void Awake()
        {
            // Garantir que o canvas do QTE começa oculto.
            if (_qteCanvas != null)
                _qteCanvas.SetActive(false);
        }

        private void Update()
        {
            if (!_qteRunning) return;

            _roundTimer += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(_roundTimer / _windowDuration);

            UpdateProgressBar(normalizedTime);

            // Verificar input do jogador.
            if (!_inputHandledThisRound && Input.GetKeyDown(_qteKey))
            {
                _inputHandledThisRound = true;
                HandleInput(normalizedTime);
                return;
            }

            // Janela expirou sem input.
            if (_roundTimer >= _windowDuration && !_inputHandledThisRound)
            {
                _inputHandledThisRound = true;
                Debug.Log($"[GAMEPLAY] CoffeeQTE — round {_currentRound}/{_totalRounds} falhou (tempo expirado).");
                FailQTE();
            }
        }

        // === Métodos públicos ===

        /// <summary>
        /// Inicia o QTE com o taskId e o número de rounds fornecidos.
        /// Chamado por CoffeeMakerInteraction após task_start_interaction.
        /// </summary>
        public void StartQTE(string taskId, int rounds)
        {
            if (_qteRunning)
            {
                Debug.LogWarning("[GAMEPLAY] CoffeeQTE — StartQTE chamado enquanto QTE já estava ativo. Reiniciando.");
                StopAllCoroutines();
            }

            _taskId       = taskId;
            _totalRounds  = Mathf.Max(1, rounds);
            _currentRound = 0;
            _qteRunning   = true;

            if (_qteCanvas != null)
                _qteCanvas.SetActive(true);

            Debug.Log($"[GAMEPLAY] CoffeeQTE — iniciando {_totalRounds} round(s) para taskId={_taskId}");

            BeginNextRound();
        }

        // === Métodos privados ===

        private void BeginNextRound()
        {
            _currentRound++;
            _roundTimer             = 0f;
            _inputHandledThisRound  = false;

            UpdateRoundText();

            Debug.Log($"[GAMEPLAY] CoffeeQTE — round {_currentRound}/{_totalRounds} iniciado.");
        }

        private void HandleInput(float normalizedTime)
        {
            bool isHit = (normalizedTime >= _hitZoneStart && normalizedTime <= _hitZoneEnd);

            if (isHit)
            {
                Debug.Log($"[GAMEPLAY] CoffeeQTE — round {_currentRound}/{_totalRounds} acertado.");

                if (_currentRound >= _totalRounds)
                {
                    // Todos os rounds concluídos com sucesso.
                    CompleteQTE();
                }
                else
                {
                    BeginNextRound();
                }
            }
            else
            {
                Debug.Log($"[GAMEPLAY] CoffeeQTE — round {_currentRound}/{_totalRounds} falhou (fora da zona de acerto).");
                FailQTE();
            }
        }

        private void CompleteQTE()
        {
            _qteRunning = false;
            HideQteUI();

            Debug.Log($"[GAMEPLAY] CoffeeQTE — todos os rounds concluídos! Enviando resultado true para taskId={_taskId}");
            TaskSystemBridge.Instance?.SendCompleteAttempt(_taskId, true);
        }

        private void FailQTE()
        {
            _qteRunning = false;
            HideQteUI();

            Debug.Log($"[GAMEPLAY] CoffeeQTE — QTE falhou. Enviando resultado false para taskId={_taskId}");
            TaskSystemBridge.Instance?.SendCompleteAttempt(_taskId, false);
        }

        private void HideQteUI()
        {
            if (_qteCanvas != null)
                _qteCanvas.SetActive(false);
        }

        private void UpdateProgressBar(float normalizedTime)
        {
            if (_progressBar != null)
                _progressBar.fillAmount = normalizedTime;
        }

        private void UpdateRoundText()
        {
            if (_roundText != null)
                _roundText.text = $"Round {_currentRound}/{_totalRounds}";
        }
    }
}
