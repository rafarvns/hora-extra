using UnityEngine;
using HoraExtra.Network.Models;
using HoraExtra.UI;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Marcador flutuante exibido sobre um objeto do mundo (cafeteira, documento) enquanto
    /// houver uma tarefa ativa do tipo correspondente atribuída ao jogador local.
    ///
    /// Indica ONDE realizar a tarefa. Visível enquanto a task está 'pending' ou
    /// 'in_progress'; some quando 'completed'/'failed' ou quando não existe task do tipo.
    ///
    /// Reavalia a visibilidade de forma reativa (eventos do TaskSystemBridge) e também no
    /// OnEnable, cobrindo o caso de a task já ter sido atribuída antes do objeto habilitar.
    ///
    /// Setup:
    ///   - _taskType: "coffee_maker" ou "collect" (ver TaskPresentation).
    ///   - _markerVisual: GameObject filho com o ícone (SpriteRenderer ou World-Space Canvas).
    ///   - _billboard: se true, o ícone gira para encarar a câmera no LateUpdate.
    /// </summary>
    public class WorldTaskMarker : MonoBehaviour
    {
        [Header("Tarefa")]
        [Tooltip("Tipo da tarefa que ativa este marcador (ex: 'coffee_maker' ou 'collect').")]
        [SerializeField] private string _taskType = TaskPresentation.TYPE_COFFEE_MAKER;

        [Header("Visual")]
        [Tooltip("GameObject filho com o ícone do marcador. É ligado/desligado conforme a task.")]
        [SerializeField] private GameObject _markerVisual;

        [Tooltip("Se marcado, o ícone gira para encarar a câmera a cada frame.")]
        [SerializeField] private bool _billboard = true;

        private Camera _camera;

        // === Lifecycle ===

        private void Awake()
        {
            _camera = Camera.main;

            if (_markerVisual != null)
                _markerVisual.SetActive(false);
        }

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskAssigned += HandleTasksAssigned;
            TaskSystemBridge.OnTaskUpdated  += HandleTaskUpdated;

            // Reflete o estado atual caso a task já tenha sido atribuída.
            Reevaluate();
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskAssigned -= HandleTasksAssigned;
            TaskSystemBridge.OnTaskUpdated  -= HandleTaskUpdated;
        }

        private void LateUpdate()
        {
            if (!_billboard || _markerVisual == null || !_markerVisual.activeSelf) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // Encara a câmera (billboard simples).
            _markerVisual.transform.rotation = Quaternion.LookRotation(
                _markerVisual.transform.position - _camera.transform.position);
        }

        // === Handlers ===

        private void HandleTasksAssigned(string playerId, System.Collections.Generic.List<AssignedTask> tasks)
        {
            Reevaluate();
        }

        private void HandleTaskUpdated(string playerId, AssignedTask task)
        {
            Reevaluate();
        }

        // === Lógica ===

        /// <summary>
        /// Liga o marcador quando existe uma task do tipo configurado ainda ativa
        /// (pending ou in_progress) para o jogador local.
        /// </summary>
        private void Reevaluate()
        {
            if (_markerVisual == null || TaskSystemBridge.Instance == null) return;

            AssignedTask task = TaskSystemBridge.Instance.FindMyTask(
                _taskType, TaskPresentation.STATUS_PENDING, TaskPresentation.STATUS_IN_PROGRESS);

            bool shouldShow = (task != null);
            if (_markerVisual.activeSelf != shouldShow)
                _markerVisual.SetActive(shouldShow);
        }
    }
}
