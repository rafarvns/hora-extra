using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Network.Models;

namespace HoraExtra.UI
{
    /// <summary>
    /// View de uma única linha do painel de missões (<see cref="MissionListHud"/>).
    ///
    /// Preenche os textos de título, "como fazer" e progresso a partir de um
    /// <see cref="AssignedTask"/>, usando <see cref="TaskPresentation"/> como fonte dos
    /// textos. Aplica feedback visual de status: concluída em destaque, falhada esmaecida.
    ///
    /// Setup no prefab PFB_MissionRow: um GameObject com os três Text e (opcional) uma
    /// Image de fundo, todos referenciados via Inspector.
    /// </summary>
    public class MissionRowView : MonoBehaviour
    {
        [Header("Textos")]
        [Tooltip("Título da tarefa (descrição do catálogo).")]
        [SerializeField] private Text _titleText;

        [Tooltip("Instrução de como executar a tarefa.")]
        [SerializeField] private Text _howToText;

        [Tooltip("Rótulo de progresso, ex: '2/4'.")]
        [SerializeField] private Text _progressText;

        [Header("Feedback de status (opcional)")]
        [Tooltip("Cor aplicada ao título quando a task está concluída.")]
        [SerializeField] private Color _completedColor = new Color(0.4f, 0.8f, 0.4f);

        [Tooltip("Cor aplicada quando a task falhou.")]
        [SerializeField] private Color _failedColor = new Color(0.7f, 0.7f, 0.7f);

        private Color _defaultTitleColor;
        private bool _defaultCaptured = false;

        /// <summary>
        /// Id da task que esta linha representa. Usado pelo MissionListHud para localizar
        /// a linha em atualizações incrementais (task_updated).
        /// </summary>
        public string TaskId { get; private set; }

        /// <summary>
        /// Preenche a linha com os dados da task. Pode ser chamado tanto na criação
        /// quanto em atualizações de progresso/status.
        /// </summary>
        public void Bind(AssignedTask task)
        {
            if (task == null) return;

            if (!_defaultCaptured && _titleText != null)
            {
                _defaultTitleColor = _titleText.color;
                _defaultCaptured = true;
            }

            TaskId = task.Id;

            if (_titleText != null)    _titleText.text    = TaskPresentation.GetTitle(task);
            if (_howToText != null)    _howToText.text    = TaskPresentation.GetHowTo(task);
            if (_progressText != null) _progressText.text = $"{TaskPresentation.GetProgressLabel(task)} — {TaskPresentation.GetStatusLabel(task)}";

            ApplyStatusColor(task.Status);
        }

        private void ApplyStatusColor(string status)
        {
            if (_titleText == null) return;

            switch (status)
            {
                case TaskPresentation.STATUS_COMPLETED:
                    _titleText.color = _completedColor;
                    break;
                case TaskPresentation.STATUS_FAILED:
                    _titleText.color = _failedColor;
                    break;
                default:
                    _titleText.color = _defaultCaptured ? _defaultTitleColor : _titleText.color;
                    break;
            }
        }
    }
}
