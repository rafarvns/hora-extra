using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Indicador discreto de conexão (canto superior direito): ponto colorido + latência.
    /// Substitui o ping legado do NetworkUI (cujo console de log continua no [F3]).
    /// </summary>
    public class NetworkStatusWidget : HudWidget
    {
        private const float GOOD_MS = 80f;
        private const float FAIR_MS = 180f;
        private const float REFRESH_SECONDS = 0.5f;

        private Image _dot;
        private TextMeshProUGUI _label;
        private float _nextRefreshAt;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(1f, 1f), new Vector2(-Theme.ScreenMargin, -Theme.ScreenMargin), new Vector2(160f, 32f));
            HudFactory.Row(Root.gameObject, 8f, TextAnchor.MiddleRight, new RectOffset(12, 14, 4, 4));
            HudFactory.FitContent(Root.gameObject, true, false);

            var bg = Root.gameObject.AddComponent<Image>();
            bg.sprite = HudSprites.Rounded;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = Theme.CornerSharpness * 2f;
            bg.color = HudTheme.WithAlpha(Theme.Panel, Theme.Panel.a * 0.7f);
            bg.raycastTarget = false;

            _dot = HudFactory.Dot(Root, "Dot", Theme.TextMuted, 10f);
            _label = HudFactory.Label(Root, "Ping", Theme, Theme.SmallSize * 0.9f, Theme.TextMuted, TextAlignmentOptions.Right, FontStyles.Bold);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + REFRESH_SECONDS;

            var socket = SocketManager.Instance;
            if (socket == null || !socket.IsConnected)
            {
                _dot.color = Theme.Danger;
                _label.text = "OFFLINE";
                _label.color = Theme.Danger;
                return;
            }

            float ms = socket.Latency;
            _dot.color = ms < GOOD_MS ? Theme.Success : ms < FAIR_MS ? Theme.Accent : Theme.Danger;
            _label.text = $"{Mathf.RoundToInt(ms)} ms";
            _label.color = Theme.TextMuted;
        }
    }
}
