using System.Collections.Generic;
using UnityEngine;
using HoraExtra.Characters;
using HoraExtra.Network.Models;

namespace HoraExtra.UI
{
    /// <summary>
    /// Painel de lista de missões sempre visível no HUD.
    ///
    /// Mostra UMA linha por tarefa atribuída ao jogador local, com título, instrução de
    /// como executar e progresso/status — para que o jogador (e o avaliador) saiba de
    /// imediato o que fazer. Complementa o contador resumido de <see cref="MissionHud"/>.
    ///
    /// Espelha o padrão Observer de MissionHud: subscribe em OnTaskAssigned/OnTaskUpdated
    /// no OnEnable, unsubscribe no OnDisable, e reflete o estado atual já no enable.
    /// Não mantém estado próprio: a fonte é TaskSystemBridge.GetMyTasks().
    ///
    /// Setup no Canvas do HUD:
    ///   - _rowsContainer: um Transform com VerticalLayoutGroup onde as linhas são criadas.
    ///   - _rowPrefab: o prefab PFB_MissionRow (com MissionRowView).
    /// </summary>
    public class MissionListHud : MonoBehaviour
    {
        [Header("Referências")]
        [Tooltip("Container (com VerticalLayoutGroup) onde as linhas de missão são instanciadas.")]
        [SerializeField] private Transform _rowsContainer;

        [Tooltip("Prefab da linha de missão (PFB_MissionRow, com MissionRowView).")]
        [SerializeField] private MissionRowView _rowPrefab;

        [Header("Cabeçalho (opcional)")]
        [Tooltip("Se atribuído, o GameObject inteiro é ocultado quando não há nenhuma task.")]
        [SerializeField] private GameObject _panelRoot;

        // Linhas vivas, keyed por taskId, para atualização incremental.
        private readonly Dictionary<string, MissionRowView> _rows = new Dictionary<string, MissionRowView>();

        // === Lifecycle ===

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskAssigned += HandleTasksAssigned;
            TaskSystemBridge.OnTaskUpdated  += HandleTaskUpdated;

            // Caso as tasks já tenham sido atribuídas antes deste HUD habilitar.
            RebuildList();
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskAssigned -= HandleTasksAssigned;
            TaskSystemBridge.OnTaskUpdated  -= HandleTaskUpdated;
        }

        // === Handlers dos eventos do TaskSystemBridge ===

        private void HandleTasksAssigned(string playerId, List<AssignedTask> tasks)
        {
            if (!IsLocalPlayer(playerId)) return;
            RebuildList();
        }

        private void HandleTaskUpdated(string playerId, AssignedTask task)
        {
            if (!IsLocalPlayer(playerId) || task == null) return;

            // Atualização incremental: rebind apenas a linha afetada.
            if (_rows.TryGetValue(task.Id, out MissionRowView row) && row != null)
                row.Bind(task);
            else
                RebuildList(); // task ainda não tinha linha (ex: HUD habilitou depois) — reconstrói.
        }

        // === Construção da lista ===

        private void RebuildList()
        {
            if (_rowsContainer == null || _rowPrefab == null || TaskSystemBridge.Instance == null)
                return;

            ClearRows();

            List<AssignedTask> tasks = TaskSystemBridge.Instance.GetMyTasks();

            foreach (AssignedTask task in tasks)
            {
                MissionRowView row = Instantiate(_rowPrefab, _rowsContainer);
                row.Bind(task);
                _rows[task.Id] = row;
            }

            if (_panelRoot != null)
                _panelRoot.SetActive(tasks.Count > 0);

            Debug.Log($"[UI] MissionListHud — lista reconstruída com {tasks.Count} missão(ões).");
        }

        private void ClearRows()
        {
            foreach (KeyValuePair<string, MissionRowView> entry in _rows)
            {
                if (entry.Value != null)
                    Destroy(entry.Value.gameObject);
            }
            _rows.Clear();
        }

        // === Helpers ===

        private bool IsLocalPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            return SocketManager.Instance != null && SocketManager.Instance.LocalPlayerId == playerId;
        }
    }
}
