using TMPro;
using UnityEngine;
using HoraExtra.Characters;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Barra de fôlego (centro-baixo). Some quando está cheia e o jogador não está correndo;
    /// fica âmbar abaixo de 50% e vermelha abaixo de 25%.
    /// Dados: <see cref="PlayerController.CurrentStamina"/> / <see cref="PlayerController.MaxStamina"/>.
    /// </summary>
    public class StaminaWidget : HudWidget
    {
        private const float RETRY_SECONDS = 1f;
        private const float HIDE_DELAY = 1.2f;
        private const float FADE_SPEED = 4f;

        private CanvasGroup _group;
        private HudBar _bar;
        private PlayerController _player;
        private float _nextFindAt;
        private float _fullSince;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(0.5f, 0f), new Vector2(0f, Theme.ScreenMargin + 64f), new Vector2(360f, 34f));
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var caption = HudFactory.Label(Root, "Caption", Theme, Theme.SmallSize * 0.85f, Theme.TextMuted, TextAlignmentOptions.Center, FontStyles.Bold);
            caption.characterSpacing = 8f;
            caption.text = "FÔLEGO";
            HudFactory.Place(caption.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(360f, 18f));

            _bar = HudFactory.Bar(Root, "Bar", Theme, Theme.Success, 10f);
            HudFactory.Place(_bar.Track.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(360f, 10f));
        }

        private void Update()
        {
            if (_player == null)
            {
                if (Time.unscaledTime >= _nextFindAt)
                {
                    _nextFindAt = Time.unscaledTime + RETRY_SECONDS;
                    _player = FindFirstObjectByType<PlayerController>();
                }
                _group.alpha = 0f;
                return;
            }

            float max = _player.MaxStamina;
            float pct = max > 0f ? Mathf.Clamp01(_player.CurrentStamina / max) : 1f;

            _bar.SetValue(pct);
            _bar.SetColor(pct < 0.25f ? Theme.Danger : pct < 0.5f ? Theme.Accent : Theme.Success);

            bool cheia = pct >= 0.999f;
            if (!cheia) _fullSince = Time.unscaledTime;
            bool mostrar = !cheia || Time.unscaledTime - _fullSince < HIDE_DELAY;

            _group.alpha = Mathf.MoveTowards(_group.alpha, mostrar ? 1f : 0f, FADE_SPEED * Time.unscaledDeltaTime);
        }
    }
}
