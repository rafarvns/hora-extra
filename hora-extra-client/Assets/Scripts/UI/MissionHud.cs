using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Characters;
using HoraExtra.Network.Models;

namespace HoraExtra.UI
{
    /// <summary>
    /// Atualiza o HUD de missões a partir dos eventos do TaskSystemBridge (Observer pattern):
    ///   - Contador "Missões: X/Y" (X = concluídas, Y = total atribuídas ao jogador local).
    ///   - Mensagem temporária de "tarefa concluída" quando uma task do jogador local
    ///     muda para o status 'completed'.
    ///
    /// O contador é derivado de TaskSystemBridge.GetMyTasks() — não há estado próprio aqui.
    /// Subscribe em OnEnable / unsubscribe em OnDisable (convenção do projeto).
    /// </summary>
    public class MissionHud : MonoBehaviour
    {
        private const string STATUS_COMPLETED = "completed";

        [Header("HUD References")]
        [Tooltip("Texto do contador de missões (HUD_Evans/Missoes). Formato final: '<prefixo>X/Y'.")]
        [SerializeField] private Text _missionCountText;

        [Tooltip("Texto temporário de conclusão. Começa oculto; aparece por alguns segundos ao completar uma task.")]
        [SerializeField] private Text _successMessageText;

        [Header("Settings")]
        [Tooltip("Prefixo do contador. O 'X/Y' é anexado ao final.")]
        [SerializeField] private string _counterPrefix = "Missões: ";

        [Tooltip("Mensagem exibida ao concluir uma tarefa.")]
        [SerializeField] private string _successMessage = "Tarefa concluída com sucesso!";

        [Tooltip("Duração (s) que a mensagem de sucesso fica visível.")]
        [SerializeField] private float _successMessageDuration = 2.5f;

        private Coroutine _hideRoutine;

        // === Lifecycle ===

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskAssigned += HandleTasksAssigned;
            TaskSystemBridge.OnTaskUpdated  += HandleTaskUpdated;

            if (_successMessageText != null)
                _successMessageText.gameObject.SetActive(false);

            // Caso as tasks já tenham sido atribuídas antes deste HUD habilitar, reflete o estado atual.
            RefreshCounter();
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskAssigned -= HandleTasksAssigned;
            TaskSystemBridge.OnTaskUpdated  -= HandleTaskUpdated;
        }

        // === Handlers dos eventos do TaskSystemBridge ===

        private void HandleTasksAssigned(string playerId, List<AssignedTask> tasks)
        {
            RefreshCounter();
        }

        private void HandleTaskUpdated(string playerId, AssignedTask task)
        {
            RefreshCounter();

            if (task != null && task.Status == STATUS_COMPLETED && IsLocalPlayer(playerId))
                ShowSuccessMessage();
        }

        // === Lógica de UI ===

        private void RefreshCounter()
        {
            if (_missionCountText == null || TaskSystemBridge.Instance == null) return;

            List<AssignedTask> tasks = TaskSystemBridge.Instance.GetMyTasks();
            int total = tasks.Count;
            int completed = 0;
            foreach (AssignedTask t in tasks)
                if (t.Status == STATUS_COMPLETED) completed++;

            _missionCountText.text = $"{_counterPrefix}{completed}/{total}";
        }

        private void ShowSuccessMessage()
        {
            if (_successMessageText == null) return;

            _successMessageText.text = _successMessage;
            _successMessageText.gameObject.SetActive(true);

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(_successMessageDuration);
            if (_successMessageText != null)
                _successMessageText.gameObject.SetActive(false);
            _hideRoutine = null;
        }

        // === Helpers ===

        private bool IsLocalPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            return SocketManager.Instance != null && SocketManager.Instance.LocalPlayerId == playerId;
        }
    }
}
