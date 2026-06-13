using System;
using UnityEngine;
using HoraExtra.Network.Models;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Controla a interação do jogador com a cafeteira.
    ///
    /// Responsabilidades:
    ///   - Detectar proximidade do jogador via trigger Collider (OnTriggerEnter/Exit).
    ///   - Exibir/ocultar prompt "Pressione E" quando o jogador está próximo.
    ///   - Gate: prompt só aparece se o jogador local tiver uma task com type=="coffee_maker"
    ///     e status=="pending" ou "in_progress".
    ///   - Ao pressionar E: envia task_start_interaction via TaskSystemBridge e inicia CoffeeQTE.
    ///   - Reage a task_updated para fechar UI quando task completar ou falhar.
    ///
    /// Setup no Prefab PFB_Interactable_CoffeeMaker:
    ///   - Trigger Collider (IsTrigger = true) neste GameObject ou num filho.
    ///   - Tag "Player" no GameObject do jogador.
    ///   - _promptUI: referência ao GameObject do prompt "Pressione E" (Canvas filho).
    ///   - _coffeeQte: referência ao componente CoffeeQTE neste prefab.
    /// </summary>
    public class CoffeeMakerInteraction : MonoBehaviour
    {
        // === Constantes ===
        private const string TASK_TYPE = "coffee_maker";
        private const string STATUS_PENDING     = "pending";
        private const string STATUS_IN_PROGRESS = "in_progress";
        private const string STATUS_COMPLETED   = "completed";
        private const string STATUS_FAILED      = "failed";

        // === Inspector ===
        [Header("UI")]
        [Tooltip("GameObject do canvas filho que exibe o prompt 'Pressione E'.")]
        [SerializeField] private GameObject _promptUI;

        [Header("Components")]
        [Tooltip("Referência ao CoffeeQTE neste prefab.")]
        [SerializeField] private CoffeeQTE _coffeeQte;

        [Header("Input")]
        [Tooltip("Tecla para iniciar a interação quando o jogador está próximo.")]
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        // === Estado privado ===
        private bool _playerNearby  = false;
        private bool _qteActive     = false;
        private bool _promptVisible = false;
        private string _currentTaskId;

        // === Lifecycle ===

        private void Awake()
        {
            // Garantir que o prompt começa oculto.
            if (_promptUI != null)
                _promptUI.SetActive(false);

            // Cache: CoffeeQTE pode estar neste GameObject.
            if (_coffeeQte == null)
                _coffeeQte = GetComponent<CoffeeQTE>();

            if (_coffeeQte == null)
                Debug.LogWarning("[GAMEPLAY] CoffeeMakerInteraction — CoffeeQTE não encontrado. Atribua via Inspector.");
        }

        private void OnEnable()
        {
            // Reagir a atualização de task para fechar UI quando status muda para completed/failed.
            TaskSystemBridge.OnTaskUpdated += OnTaskStatusUpdated;
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskUpdated -= OnTaskStatusUpdated;
        }

        private void Update()
        {
            if (!_playerNearby || _qteActive) return;

            // Verificar gate de task antes de mostrar prompt.
            var task = GetEligibleTask();
            bool shouldShowPrompt = (task != null);

            if (_promptUI != null && _promptUI.activeSelf != shouldShowPrompt)
            {
                _promptUI.SetActive(shouldShowPrompt);
                if (shouldShowPrompt && !_promptVisible)
                    Debug.Log("[UI] CoffeeMakerInteraction — jogador próximo, exibindo prompt.");
                _promptVisible = shouldShowPrompt;
            }

            if (!shouldShowPrompt) return;

            if (Input.GetKeyDown(_interactKey))
            {
                StartInteraction(task);
            }
        }

        // === Trigger de proximidade ===

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerNearby = true;
            Debug.Log("[UI] CoffeeMakerInteraction — jogador próximo, avaliando gate de task.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            _playerNearby = false;
            HidePrompt();
            Debug.Log("[UI] CoffeeMakerInteraction — jogador saiu da área.");
        }

        // === Métodos privados ===

        /// <summary>
        /// Retorna a task elegível para interação (type=="coffee_maker" e status pending ou in_progress).
        /// Retorna null se não houver nenhuma ou se TaskSystemBridge não estiver disponível.
        /// </summary>
        private AssignedTask GetEligibleTask()
        {
            if (TaskSystemBridge.Instance == null) return null;
            return TaskSystemBridge.Instance.FindMyTask(TASK_TYPE, STATUS_PENDING, STATUS_IN_PROGRESS);
        }

        /// <summary>
        /// Inicia a interação: envia task_start_interaction e dispara o CoffeeQTE.
        /// </summary>
        private void StartInteraction(AssignedTask task)
        {
            _currentTaskId = task.Id;
            _qteActive = true;

            HidePrompt();

            // Notificar servidor: pending → in_progress.
            TaskSystemBridge.Instance.SendStartInteraction(_currentTaskId);

            // Iniciar minigame com número de rounds igual ao targetCount.
            if (_coffeeQte != null)
            {
                Debug.Log($"[GAMEPLAY] CoffeeMakerInteraction — iniciando CoffeeQTE para taskId={_currentTaskId} rounds={task.TargetCount}");
                _coffeeQte.StartQTE(_currentTaskId, task.TargetCount);
            }
            else
            {
                Debug.LogError("[GAMEPLAY] CoffeeMakerInteraction — CoffeeQTE não disponível, enviando resultado direto.");
                // Fallback: reportar sucesso imediato caso QTE não esteja configurado.
                TaskSystemBridge.Instance.SendCompleteAttempt(_currentTaskId, true);
                _qteActive = false;
            }
        }

        private void HidePrompt()
        {
            if (_promptUI != null)
                _promptUI.SetActive(false);
            _promptVisible = false;
        }

        /// <summary>
        /// Callback de OnTaskUpdated: reage quando o status muda (ex: in_progress confirmado,
        /// completed, ou failed). Reseta _qteActive quando a task encerra.
        /// </summary>
        private void OnTaskStatusUpdated(string playerId, AssignedTask task)
        {
            if (task.Id != _currentTaskId) return;

            if (task.Status == STATUS_IN_PROGRESS)
            {
                Debug.Log($"[GAMEPLAY] CoffeeMakerInteraction — task {task.Id} confirmada in_progress pelo servidor.");
            }
            else if (task.Status == STATUS_COMPLETED || task.Status == STATUS_FAILED)
            {
                _qteActive = false;
                HidePrompt();
                Debug.Log($"[GAMEPLAY] CoffeeMakerInteraction — task {task.Id} encerrada com status={task.Status}.");
            }
        }
    }
}
