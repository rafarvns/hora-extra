using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Characters;
using HoraExtra.Network.Models;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Card da tarefa atual (topo-esquerda). Substitui o checklist em texto do MissionHud.
    ///
    ///   - cabeçalho "TAREFA X DE N" + badge da categoria;
    ///   - título, barra de progresso autoritativa e o passo a passo;
    ///   - barra segmentada com todas as N tarefas da partida (verde = concluída,
    ///     vermelho = falhou, âmbar = atual, apagado = ainda na fila do servidor);
    ///   - segurar [Tab] mostra a lista de todas as tarefas já liberadas.
    ///
    /// Dados: TaskSystemBridge (GetMyTasks + MyTotalTasks). Nada é contado localmente.
    /// </summary>
    public class MissionCardWidget : HudWidget
    {
        private const float WIDTH = 450f;

        private Image _panel;
        private TextMeshProUGUI _header;
        private Image _badge;
        private TextMeshProUGUI _badgeLabel;
        private TextMeshProUGUI _title;
        private GameObject _progressRow;
        private HudBar _progressBar;
        private TextMeshProUGUI _progressLabel;
        private TextMeshProUGUI _howTo;
        private RectTransform _segments;
        private RectTransform _history;
        private TextMeshProUGUI _footer;

        private readonly List<Image> _segmentImages = new List<Image>();
        private bool _historyOpen;
        private Coroutine _pulse;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(0f, 1f), new Vector2(Theme.ScreenMargin, -Theme.ScreenMargin), new Vector2(WIDTH, 100f));

            _panel = Root.gameObject.AddComponent<Image>();
            _panel.sprite = HudSprites.Rounded;
            _panel.type = Image.Type.Sliced;
            _panel.pixelsPerUnitMultiplier = Theme.CornerSharpness;
            _panel.color = Theme.Panel;
            _panel.raycastTarget = false;

            HudFactory.Column(Root.gameObject, 10f, new RectOffset(22, 22, 18, 18));
            HudFactory.FitContent(Root.gameObject, false, true);

            // Cabeçalho: "TAREFA 2 DE 4" ............ [ORGANIZAÇÃO]
            var headerRow = HudFactory.Rect("HeaderRow", Root);
            HudFactory.Row(headerRow.gameObject, 10f);
            _header = HudFactory.Label(headerRow, "Header", Theme, Theme.SmallSize, Theme.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
            _header.characterSpacing = 6f;
            HudFactory.Fixed(_header.gameObject, -1f, 26f).flexibleWidth = 1f;

            _badge = HudFactory.Panel(headerRow, "Badge", Theme.CategoryOther, Theme);
            _badge.pixelsPerUnitMultiplier = Theme.CornerSharpness * 2.5f;
            HudFactory.Row(_badge.gameObject, 0f, TextAnchor.MiddleCenter, new RectOffset(10, 10, 3, 3));
            HudFactory.Fixed(_badge.gameObject, -1f, 24f);
            _badgeLabel = HudFactory.Label(_badge.transform, "Label", Theme, Theme.SmallSize * 0.8f, Theme.Panel, TextAlignmentOptions.Center, FontStyles.Bold);
            _badgeLabel.characterSpacing = 4f;

            _title = HudFactory.Label(Root, "Title", Theme, Theme.TitleSize, Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold, wrap: true);

            // Progresso: [██████░░░░] 2/4
            var progress = HudFactory.Rect("ProgressRow", Root);
            _progressRow = progress.gameObject;
            HudFactory.Row(_progressRow, 12f);
            _progressBar = HudFactory.Bar(progress, "Bar", Theme, Theme.Accent, 10f);
            _progressBar.Track.GetComponent<LayoutElement>().flexibleWidth = 1f;
            _progressLabel = HudFactory.Label(progress, "Count", Theme, Theme.BodySize, Theme.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            HudFactory.Fixed(_progressLabel.gameObject, 64f, 26f);

            _howTo = HudFactory.Label(Root, "HowTo", Theme, Theme.BodySize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal, wrap: true);
            _howTo.lineSpacing = 6f;

            _segments = HudFactory.Rect("Segments", Root);
            var segRow = HudFactory.Row(_segments.gameObject, 5f);
            segRow.childForceExpandWidth = true;
            HudFactory.Fixed(_segments.gameObject, -1f, 6f);

            _history = HudFactory.Rect("History", Root);
            HudFactory.Column(_history.gameObject, 6f, new RectOffset(0, 0, 4, 0));
            _history.gameObject.SetActive(false);

            _footer = HudFactory.Label(Root, "Footer", Theme, Theme.SmallSize * 0.9f, Theme.TextMuted, TextAlignmentOptions.Left);
            _footer.text = $"<color={HudFactory.Hex(Theme.Text)}><b>[Tab]</b></color> todas as tarefas";
        }

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskAssigned += HandleAssigned;
            TaskSystemBridge.OnTaskUpdated += HandleUpdated;
            Refresh();
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskAssigned -= HandleAssigned;
            TaskSystemBridge.OnTaskUpdated -= HandleUpdated;
        }

        private void Update()
        {
            bool open = HudInput.TaskListHeld();
            if (open == _historyOpen) return;
            _historyOpen = open;
            Refresh();
        }

        private void HandleAssigned(string playerId, List<AssignedTask> tasks)
        {
            if (IsLocalPlayer(playerId)) Refresh();
        }

        private void HandleUpdated(string playerId, AssignedTask task)
        {
            if (!IsLocalPlayer(playerId)) return;
            Refresh();

            if (task != null && task.Status == TaskPresentation.STATUS_COMPLETED)
                Pulse(Theme.Success);
            else if (task != null && task.Status == TaskPresentation.STATUS_FAILED)
                Pulse(Theme.Danger);
        }

        private void Refresh()
        {
            var bridge = TaskSystemBridge.Instance;
            List<AssignedTask> tasks = bridge != null ? bridge.GetMyTasks() : new List<AssignedTask>();
            int total = bridge != null ? bridge.MyTotalTasks : 0;

            int terminadas = 0;
            foreach (var t in tasks)
                if (TaskPresentation.IsFinished(t)) terminadas++;

            AssignedTask ativa = TaskPresentation.FindActive(tasks);
            int indiceAtiva = ativa != null ? tasks.IndexOf(ativa) + 1 : terminadas;

            RebuildSegments(tasks, total);
            RebuildHistory(tasks);

            if (tasks.Count == 0)
            {
                ShowMessage("AGUARDANDO", "Aguardando tarefas…", Theme.Text, "O supervisor está distribuindo o trabalho.");
                return;
            }

            if (ativa == null)
            {
                bool tudo = total == 0 || terminadas >= total;
                ShowMessage(tudo ? "EXPEDIENTE EM DIA" : "AGUARDANDO",
                    tudo ? "Todas as tarefas concluídas!" : "Próxima tarefa a caminho…",
                    tudo ? Theme.Success : Theme.Text,
                    tudo ? $"{terminadas} de {Mathf.Max(total, terminadas)} tarefas finalizadas." : null);
                return;
            }

            _header.text = total > 0 ? $"TAREFA {indiceAtiva} DE {total}" : "TAREFA ATUAL";
            _header.color = Theme.Accent;

            var categoria = TaskPresentation.GetCategory(ativa.Type);
            _badge.gameObject.SetActive(true);
            _badge.color = Theme.CategoryColor(categoria);
            _badgeLabel.text = TaskPresentation.GetCategoryLabel(categoria);

            _title.text = TaskPresentation.GetSummary(ativa);
            _title.color = Theme.Text;

            _progressRow.SetActive(true);
            float p = ativa.TargetCount > 0 ? (float)ativa.CurrentProgress / ativa.TargetCount : 0f;
            _progressBar.SetValue(p);
            _progressBar.SetColor(Theme.Accent);
            _progressLabel.text = TaskPresentation.GetProgressLabel(ativa);

            _howTo.text = TaskPresentation.GetHowTo(ativa);
            _howTo.gameObject.SetActive(!string.IsNullOrEmpty(_howTo.text));
        }

        private void ShowMessage(string header, string title, Color titleColor, string body)
        {
            _header.text = header;
            _header.color = titleColor == Theme.Success ? Theme.Success : Theme.TextMuted;
            _badge.gameObject.SetActive(false);
            _title.text = title;
            _title.color = titleColor;
            _progressRow.SetActive(false);
            _howTo.text = body ?? string.Empty;
            _howTo.gameObject.SetActive(!string.IsNullOrEmpty(body));
        }

        /// <summary>Uma fatia por tarefa da partida, incluindo as que ainda estão na fila do servidor.</summary>
        private void RebuildSegments(List<AssignedTask> tasks, int total)
        {
            int n = Mathf.Max(total, tasks.Count);
            _segments.gameObject.SetActive(n > 1);

            while (_segmentImages.Count < n)
            {
                var seg = HudFactory.Panel(_segments, $"Seg{_segmentImages.Count}", Theme.Track, Theme);
                seg.pixelsPerUnitMultiplier = 8f;
                HudFactory.Fixed(seg.gameObject, -1f, 6f).flexibleWidth = 1f;
                _segmentImages.Add(seg);
            }
            for (int i = 0; i < _segmentImages.Count; i++)
                _segmentImages[i].gameObject.SetActive(i < n);

            var ativa = TaskPresentation.FindActive(tasks);
            for (int i = 0; i < n; i++)
            {
                Color c = Theme.Track;
                if (i < tasks.Count)
                {
                    var t = tasks[i];
                    if (t.Status == TaskPresentation.STATUS_COMPLETED) c = Theme.Success;
                    else if (t.Status == TaskPresentation.STATUS_FAILED) c = Theme.Danger;
                    else if (t == ativa) c = Theme.Accent;
                }
                _segmentImages[i].color = c;
            }
        }

        /// <summary>Lista completa (só com [Tab] segurado): uma linha por tarefa já liberada.</summary>
        private void RebuildHistory(List<AssignedTask> tasks)
        {
            bool mostrar = _historyOpen && tasks.Count > 0;
            _history.gameObject.SetActive(mostrar);
            _footer.gameObject.SetActive(!_historyOpen && tasks.Count > 1);
            if (!mostrar) return;

            for (int i = _history.childCount - 1; i >= 0; i--)
                Destroy(_history.GetChild(i).gameObject);

            var titulo = HudFactory.Label(_history, "HistoryTitle", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Bold);
            titulo.characterSpacing = 6f;
            titulo.text = "TODAS AS TAREFAS";

            var ativa = TaskPresentation.FindActive(tasks);
            foreach (var t in tasks)
            {
                var row = HudFactory.Rect("Row", _history);
                HudFactory.Row(row.gameObject, 10f);

                Color cor = t.Status == TaskPresentation.STATUS_COMPLETED ? Theme.Success
                          : t.Status == TaskPresentation.STATUS_FAILED ? Theme.Danger
                          : t == ativa ? Theme.Accent : Theme.TextMuted;
                HudFactory.Dot(row, "Dot", cor, 10f);

                var nome = HudFactory.Label(row, "Name", Theme, Theme.BodySize, t == ativa ? Theme.Text : Theme.TextMuted);
                nome.text = TaskPresentation.GetSummary(t);
                if (TaskPresentation.IsFinished(t)) nome.fontStyle = FontStyles.Strikethrough;
                HudFactory.Fixed(nome.gameObject, -1f, 26f).flexibleWidth = 1f;

                var status = HudFactory.Label(row, "Status", Theme, Theme.SmallSize, cor, TextAlignmentOptions.Right);
                status.text = t.Status == TaskPresentation.STATUS_FAILED ? "falhou" : TaskPresentation.GetProgressLabel(t);
                HudFactory.Fixed(status.gameObject, 64f, 26f);
            }

            var bridge = TaskSystemBridge.Instance;
            int naFila = bridge != null ? bridge.MyTotalTasks - tasks.Count : 0;
            if (naFila > 0)
            {
                var fila = HudFactory.Label(_history, "Queue", Theme, Theme.SmallSize, Theme.TextMuted);
                fila.text = naFila == 1 ? "+ 1 tarefa ainda por liberar" : $"+ {naFila} tarefas ainda por liberar";
            }
        }

        private void Pulse(Color flash)
        {
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine(flash));
        }

        private IEnumerator PulseRoutine(Color flash)
        {
            const float duracao = 0.45f;
            float t = 0f;
            while (t < duracao)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / duracao) * Mathf.PI);
                Root.localScale = Vector3.one * (1f + 0.035f * k);
                _panel.color = Color.Lerp(Theme.Panel, HudTheme.WithAlpha(flash, Theme.Panel.a), k * 0.35f);
                yield return null;
            }
            Root.localScale = Vector3.one;
            _panel.color = Theme.Panel;
            _pulse = null;
        }
    }
}
