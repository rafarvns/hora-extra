using TMPro;
using UnityEngine;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Relógio do expediente (topo-centro): hora de jogo grande, quanto falta para o fim e
    /// uma barra fina do dia. Fica âmbar perto do fim e vermelho quando o expediente acaba.
    /// Dados: <see cref="ShiftClock"/>.
    /// </summary>
    public class ShiftClockWidget : HudWidget
    {
        private TextMeshProUGUI _time;
        private TextMeshProUGUI _caption;
        private HudBar _dayBar;
        private int _lastMinuteShown = int.MinValue;
        private ShiftClock.ShiftState _lastState = (ShiftClock.ShiftState)(-1);

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(0.5f, 1f), new Vector2(0f, -Theme.ScreenMargin), new Vector2(260f, 112f));

            var panel = HudFactory.Panel(Root, "Panel", Theme.Panel, Theme);
            HudFactory.Stretch(panel.rectTransform);
            HudFactory.Column(panel.gameObject, 2f, new RectOffset(18, 18, 8, 12));

            _time = HudFactory.Label(panel.transform, "Time", Theme, Theme.ClockSize, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            _time.characterSpacing = 4f;
            HudFactory.Fixed(_time.gameObject, -1f, Theme.ClockSize * 1.1f);

            _caption = HudFactory.Label(panel.transform, "Caption", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Center);
            HudFactory.Fixed(_caption.gameObject, -1f, Theme.SmallSize * 1.4f);

            _dayBar = HudFactory.Bar(panel.transform, "DayBar", Theme, Theme.Accent, 6f);
            _dayBar.SetValue(0f);
        }

        private void Update()
        {
            var clock = ShiftClock.Instance;
            var state = clock != null ? clock.State : ShiftClock.ShiftState.Idle;
            int minute = clock != null ? Mathf.FloorToInt(clock.CurrentMinute) : 0;

            if (state == _lastState && minute == _lastMinuteShown) return;
            _lastState = state;
            _lastMinuteShown = minute;

            switch (state)
            {
                case ShiftClock.ShiftState.Running:
                    float restante = clock.EndMinute - clock.CurrentMinute;
                    bool reta = restante <= Theme.ClockWarningMinutes;
                    _time.text = ShiftClock.Format(minute);
                    _time.color = reta ? Theme.Accent : Theme.Text;
                    _caption.text = $"faltam {FormatDuration(restante)}";
                    _caption.color = reta ? Theme.Accent : Theme.TextMuted;
                    _dayBar.SetColor(reta ? Theme.Accent : Theme.Info);
                    _dayBar.SetValue(clock.Progress01);
                    break;

                case ShiftClock.ShiftState.Ended:
                    _time.text = ShiftClock.Format(clock.EndMinute);
                    _time.color = Theme.Danger;
                    _caption.text = "<b>FIM DO EXPEDIENTE</b>";
                    _caption.color = Theme.Danger;
                    _dayBar.SetColor(Theme.Danger);
                    _dayBar.SetValue(1f);
                    break;

                default:
                    _time.text = "--:--";
                    _time.color = Theme.TextMuted;
                    _caption.text = "expediente não iniciado";
                    _caption.color = Theme.TextMuted;
                    _dayBar.SetValue(0f);
                    break;
            }
        }

        /// <summary>Minutos de jogo como "2h 15min" / "45min".</summary>
        private static string FormatDuration(float minutes)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(minutes));
            int h = total / 60, m = total % 60;
            if (h == 0) return $"{m}min";
            return m == 0 ? $"{h}h" : $"{h}h {m:00}min";
        }
    }
}
