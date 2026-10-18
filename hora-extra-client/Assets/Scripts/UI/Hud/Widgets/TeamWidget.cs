using System.Collections.Generic;
using TMPro;
using UnityEngine;
using HoraExtra.Characters;
using HoraExtra.Network.Models;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Painel da equipe (topo-direita): uma linha por jogador da sala com nome, tarefa atual
    /// e quantas já terminou. O jogador local aparece destacado como "(você)".
    ///
    /// Dados: <see cref="RoomRoster"/> (quem está na sala) + TaskSystemBridge.GetTasksOf (o que
    /// cada um está fazendo, vindo dos broadcasts task_assigned/task_updated).
    /// Some quando ainda não há roster (servidor sem room_roster).
    /// </summary>
    public class TeamWidget : HudWidget
    {
        private const float WIDTH = 330f;

        private RectTransform _rows;
        private bool _dirty = true;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(1f, 1f), new Vector2(-Theme.ScreenMargin, -Theme.ScreenMargin - 44f), new Vector2(WIDTH, 80f));

            var panel = Root.gameObject.AddComponent<UnityEngine.UI.Image>();
            panel.sprite = HudSprites.Rounded;
            panel.type = UnityEngine.UI.Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = Theme.CornerSharpness;
            panel.color = Theme.Panel;
            panel.raycastTarget = false;

            HudFactory.Column(Root.gameObject, 8f, new RectOffset(18, 18, 14, 16));
            HudFactory.FitContent(Root.gameObject, false, true);

            var header = HudFactory.Label(Root, "Header", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Bold);
            header.characterSpacing = 6f;
            header.text = "EQUIPE";

            _rows = HudFactory.Rect("Rows", Root);
            HudFactory.Column(_rows.gameObject, 10f);
        }

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskAssigned += HandleAssigned;
            TaskSystemBridge.OnTaskUpdated += HandleUpdated;
            if (RoomRoster.Instance != null) RoomRoster.Instance.OnRosterChanged += MarkDirty;
            _dirty = true;
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskAssigned -= HandleAssigned;
            TaskSystemBridge.OnTaskUpdated -= HandleUpdated;
            if (RoomRoster.Instance != null) RoomRoster.Instance.OnRosterChanged -= MarkDirty;
        }

        private void HandleAssigned(string playerId, List<AssignedTask> tasks) => _dirty = true;
        private void HandleUpdated(string playerId, AssignedTask task) => _dirty = true;
        private void MarkDirty() => _dirty = true;

        // Reconstrói no máximo uma vez por frame, mesmo com vários eventos seguidos.
        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        private void Rebuild()
        {
            for (int i = _rows.childCount - 1; i >= 0; i--)
                Destroy(_rows.GetChild(i).gameObject);

            var roster = RoomRoster.Instance;
            bool temRoster = roster != null && roster.Players.Count > 0;
            Root.gameObject.GetComponent<UnityEngine.UI.Image>().enabled = temRoster;
            foreach (Transform child in Root) child.gameObject.SetActive(temRoster);
            if (!temRoster) return;

            // Jogador local primeiro, depois os colegas na ordem do servidor.
            var ordenados = new List<RosterEntry>(roster.Players);
            ordenados.Sort((a, b) => (IsLocalPlayer(b.Id) ? 1 : 0) - (IsLocalPlayer(a.Id) ? 1 : 0));

            foreach (var p in ordenados)
                BuildRow(p);
        }

        private void BuildRow(RosterEntry player)
        {
            var bridge = TaskSystemBridge.Instance;
            var tasks = bridge != null ? bridge.GetTasksOf(player.Id) : new List<AssignedTask>();
            int total = bridge != null ? bridge.GetTotalTasksOf(player.Id) : 0;
            int terminadas = 0;
            foreach (var t in tasks)
                if (TaskPresentation.IsFinished(t)) terminadas++;
            var ativa = TaskPresentation.FindActive(tasks);
            bool local = IsLocalPlayer(player.Id);
            bool tudoFeito = total > 0 && terminadas >= total;

            var row = HudFactory.Rect("Row", _rows);
            HudFactory.Row(row.gameObject, 12f, TextAnchor.MiddleLeft);

            Color status = tudoFeito ? Theme.Success : ativa != null ? Theme.Accent : Theme.TextMuted;
            HudFactory.Dot(row, "Status", status, 12f);

            var texts = HudFactory.Rect("Texts", row);
            HudFactory.Column(texts.gameObject, 0f);
            HudFactory.Fixed(texts.gameObject, -1f, -1f).flexibleWidth = 1f;

            var nome = HudFactory.Label(texts, "Name", Theme, Theme.BodySize, local ? Theme.Accent : Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            nome.text = local ? $"{player.Name} <size=75%><color={HudFactory.Hex(Theme.TextMuted)}>(você)</color></size>" : player.Name;
            nome.overflowMode = TextOverflowModes.Ellipsis;

            var tarefa = HudFactory.Label(texts, "Task", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Left);
            tarefa.overflowMode = TextOverflowModes.Ellipsis;
            tarefa.text = tudoFeito ? "Tudo concluído"
                        : ativa != null ? $"{TaskPresentation.GetSummary(ativa)} · {TaskPresentation.GetProgressLabel(ativa)}"
                        : "Sem tarefa no momento";

            var placar = HudFactory.Label(row, "Score", Theme, Theme.BodySize, tudoFeito ? Theme.Success : Theme.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            placar.text = total > 0 ? $"{terminadas}<size=70%><color={HudFactory.Hex(Theme.TextMuted)}>/{total}</color></size>" : "–";
            HudFactory.Fixed(placar.gameObject, 56f, -1f);
        }
    }
}
