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
    /// Fila de avisos (lado direito). Cada aviso tem uma faixa colorida pelo tipo, entra com
    /// fade/slide e sai sozinho. No máximo <see cref="MAX_VISIBLE"/> ao mesmo tempo: o mais
    /// antigo sai para dar lugar ao novo.
    ///
    /// Fontes: tarefas do jogador local (nova / concluída / falhou / todas concluídas),
    /// colegas entrando e saindo (RoomRoster), marcos do expediente (ShiftClock) e queda de
    /// conexão. Outros scripts podem publicar via <see cref="HudRoot.Toast"/>.
    /// </summary>
    public class ToastWidget : HudWidget
    {
        public enum Kind { Info, Success, Warning, Danger }

        private const int MAX_VISIBLE = 3;
        private const float LIFETIME = 4.5f;
        private const float FADE = 0.25f;
        private const float WIDTH = 360f;

        private readonly List<GameObject> _active = new List<GameObject>();
        private bool _wasConnected;
        private bool _connectionKnown;
        private bool _reminderShown;
        private bool _allDoneShown;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(1f, 0.62f), new Vector2(-Theme.ScreenMargin, 0f), new Vector2(WIDTH, 300f));
            Root.pivot = new Vector2(1f, 1f);
            var col = HudFactory.Column(Root.gameObject, 10f);
            col.childAlignment = TextAnchor.UpperRight;
        }

        private void OnEnable()
        {
            TaskSystemBridge.OnTaskAssigned += HandleAssigned;
            TaskSystemBridge.OnTaskUpdated += HandleUpdated;
            if (RoomRoster.Instance != null)
            {
                RoomRoster.Instance.OnPlayerJoined += HandleJoined;
                RoomRoster.Instance.OnPlayerLeft += HandleLeft;
            }
            if (ShiftClock.Instance != null)
            {
                ShiftClock.Instance.OnMinuteTick += HandleMinute;
                ShiftClock.Instance.OnStateChanged += HandleShiftState;
            }
        }

        private void OnDisable()
        {
            TaskSystemBridge.OnTaskAssigned -= HandleAssigned;
            TaskSystemBridge.OnTaskUpdated -= HandleUpdated;
            if (RoomRoster.Instance != null)
            {
                RoomRoster.Instance.OnPlayerJoined -= HandleJoined;
                RoomRoster.Instance.OnPlayerLeft -= HandleLeft;
            }
            if (ShiftClock.Instance != null)
            {
                ShiftClock.Instance.OnMinuteTick -= HandleMinute;
                ShiftClock.Instance.OnStateChanged -= HandleShiftState;
            }
        }

        private void Update()
        {
            // Conexão: avisa na transição, não a cada frame.
            var socket = SocketManager.Instance;
            bool conectado = socket != null && socket.IsConnected;
            if (_connectionKnown && conectado != _wasConnected)
            {
                if (conectado) Show(Kind.Success, "Conexão restabelecida");
                else Show(Kind.Danger, "Conexão perdida", "Tentando reconectar ao servidor…");
            }
            _wasConnected = conectado;
            _connectionKnown |= conectado;
        }

        // === API ===

        public void Show(Kind kind, string title, string body = null)
        {
            if (_active.Count >= MAX_VISIBLE)
            {
                Destroy(_active[0]);
                _active.RemoveAt(0);
            }

            var toast = BuildToast(kind, title, body);
            _active.Add(toast);
            StartCoroutine(Lifecycle(toast));
            Debug.Log($"[UI] Toast ({kind}): {title}{(body != null ? " — " + body : "")}");
        }

        // === Fontes ===

        private void HandleAssigned(string playerId, List<AssignedTask> tasks)
        {
            if (!IsLocalPlayer(playerId) || tasks == null) return;
            foreach (var t in tasks)
                if (!TaskPresentation.IsFinished(t))
                    Show(Kind.Info, "Nova tarefa", TaskPresentation.GetSummary(t));
        }

        private void HandleUpdated(string playerId, AssignedTask task)
        {
            if (!IsLocalPlayer(playerId) || task == null) return;

            if (task.Status == TaskPresentation.STATUS_COMPLETED)
                Show(Kind.Success, "Tarefa concluída", TaskPresentation.GetSummary(task));
            else if (task.Status == TaskPresentation.STATUS_FAILED)
                Show(Kind.Danger, "Tarefa falhou", TaskPresentation.GetSummary(task));
            else
                return;

            var bridge = TaskSystemBridge.Instance;
            if (bridge == null || _allDoneShown) return;
            var minhas = bridge.GetMyTasks();
            int terminadas = 0;
            foreach (var t in minhas)
                if (TaskPresentation.IsFinished(t)) terminadas++;
            if (bridge.MyTotalTasks > 0 && terminadas >= bridge.MyTotalTasks)
            {
                _allDoneShown = true;
                Show(Kind.Success, "Todas as tarefas concluídas!", "Bom trabalho — o escritório está em ordem.");
            }
        }

        private void HandleJoined(RosterEntry p)
        {
            if (p == null || IsLocalPlayer(p.Id)) return;
            Show(Kind.Info, $"{p.Name} entrou no expediente");
        }

        private void HandleLeft(RosterEntry p)
        {
            if (p == null || IsLocalPlayer(p.Id)) return;
            Show(Kind.Warning, $"{p.Name} saiu");
        }

        private void HandleMinute(int minute)
        {
            var clock = ShiftClock.Instance;
            if (clock == null || _reminderShown) return;
            float restante = clock.EndMinute - minute;
            if (restante <= Theme.ClockReminderMinutes && restante > 0f)
            {
                _reminderShown = true;
                Show(Kind.Warning, $"Faltam {Mathf.CeilToInt(restante)} min para o fim do expediente");
            }
        }

        private void HandleShiftState(ShiftClock.ShiftState state)
        {
            if (state == ShiftClock.ShiftState.Running)
                Show(Kind.Info, "Expediente iniciado", $"Das {ShiftClock.Format(ShiftClock.Instance.StartMinute)} às {ShiftClock.Format(ShiftClock.Instance.EndMinute)}");
            else if (state == ShiftClock.ShiftState.Ended)
                Show(Kind.Danger, "Fim do expediente", "Agora é hora extra…");
        }

        // === Visual ===

        private Color KindColor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Success: return Theme.Success;
                case Kind.Warning: return Theme.Accent;
                case Kind.Danger:  return Theme.Danger;
                default:           return Theme.Info;
            }
        }

        private GameObject BuildToast(Kind kind, string title, string body)
        {
            var panel = HudFactory.Panel(Root, "Toast", Theme.Panel, Theme);
            panel.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            HudFactory.Row(panel.gameObject, 14f, TextAnchor.MiddleLeft, new RectOffset(12, 18, 12, 12));
            HudFactory.Fixed(panel.gameObject, WIDTH, -1f);

            var stripe = HudFactory.Panel(panel.transform, "Stripe", KindColor(kind), Theme);
            stripe.pixelsPerUnitMultiplier = 10f;
            var stripeLayout = HudFactory.Fixed(stripe.gameObject, 5f, -1f);
            stripeLayout.minHeight = 36f;
            stripeLayout.flexibleHeight = 1f;

            var texts = HudFactory.Rect("Texts", panel.transform);
            HudFactory.Column(texts.gameObject, 2f);
            HudFactory.Fixed(texts.gameObject, -1f, -1f).flexibleWidth = 1f;

            var t = HudFactory.Label(texts, "Title", Theme, Theme.BodySize, Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold, wrap: true);
            t.text = title;

            if (!string.IsNullOrEmpty(body))
            {
                var b = HudFactory.Label(texts, "Body", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal, wrap: true);
                b.text = body;
            }

            // A Row com childForceExpandHeight=false não estica a faixa; o layout do pai cuida.
            panel.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            return panel.gameObject;
        }

        private IEnumerator Lifecycle(GameObject toast)
        {
            var group = toast.GetComponent<CanvasGroup>();

            for (float t = 0f; t < FADE && toast != null; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / FADE;
                yield return null;
            }
            if (toast == null) yield break;
            group.alpha = 1f;

            yield return new WaitForSecondsRealtime(LIFETIME);
            if (toast == null) yield break;

            for (float t = 0f; t < FADE && toast != null; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / FADE;
                yield return null;
            }
            if (toast == null) yield break;

            _active.Remove(toast);
            Destroy(toast);
        }
    }
}
