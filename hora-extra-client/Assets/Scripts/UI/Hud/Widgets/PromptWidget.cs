using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Interactions;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Pílula de interação (centro-baixo) — desenha o que o <see cref="InteractionPrompt"/>
    /// emite. As teclas entre colchetes ("[E]") ganham destaque, e ações de segurar mostram
    /// um anel de progresso à esquerda.
    ///
    /// O InteractionPrompt continua sendo a fonte da verdade (dono, temporários, recusa do
    /// servidor); este widget só renderiza.
    /// </summary>
    public class PromptWidget : HudWidget
    {
        private const float FADE_SPEED = 12f;

        private CanvasGroup _group;
        private Image _ring;
        private Image _ringTrack;
        private TextMeshProUGUI _label;

        private InteractionPrompt _currentSender;
        private bool _visible;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(200f, 60f));
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            var panel = Root.gameObject.AddComponent<Image>();
            panel.sprite = HudSprites.Rounded;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = Theme.CornerSharpness * 0.8f;
            panel.color = Theme.Panel;
            panel.raycastTarget = false;

            HudFactory.Row(Root.gameObject, 14f, TextAnchor.MiddleCenter, new RectOffset(26, 26, 12, 12));
            HudFactory.FitContent(Root.gameObject, true, true);

            var ringHolder = HudFactory.Rect("Ring", Root);
            HudFactory.Fixed(ringHolder.gameObject, 34f, 34f);
            _ringTrack = ringHolder.gameObject.AddComponent<Image>();
            _ringTrack.sprite = HudSprites.Ring;
            _ringTrack.color = Theme.Track;
            _ringTrack.raycastTarget = false;

            _ring = HudFactory.Rect("Fill", ringHolder).gameObject.AddComponent<Image>();
            HudFactory.Stretch(_ring.rectTransform);
            _ring.sprite = HudSprites.Ring;
            _ring.type = Image.Type.Filled;
            _ring.fillMethod = Image.FillMethod.Radial360;
            _ring.fillOrigin = (int)Image.Origin360.Top;
            _ring.fillClockwise = true;
            _ring.color = Theme.Accent;
            _ring.raycastTarget = false;
            ringHolder.gameObject.SetActive(false);

            _label = HudFactory.Label(Root, "Text", Theme, Theme.BodySize * 1.1f, Theme.Text, TextAlignmentOptions.Center);
        }

        private void OnEnable()
        {
            InteractionPrompt.OnPromptChanged += HandlePrompt;
        }

        private void OnDisable()
        {
            InteractionPrompt.OnPromptChanged -= HandlePrompt;
        }

        private void HandlePrompt(InteractionPrompt sender, string message, bool visible, float progress)
        {
            if (!visible)
            {
                // Outro prompt (ex: o do elevador) escondendo não apaga o que está na tela.
                if (sender == _currentSender) _visible = false;
                return;
            }

            _currentSender = sender;
            _visible = true;
            _label.text = Highlight(message);

            bool temBarra = progress >= 0f;
            _ringTrack.gameObject.SetActive(temBarra);
            if (temBarra) _ring.fillAmount = progress;
        }

        private void Update()
        {
            float alvo = _visible ? 1f : 0f;
            if (Mathf.Approximately(_group.alpha, alvo)) return;
            _group.alpha = Mathf.MoveTowards(_group.alpha, alvo, FADE_SPEED * Time.unscaledDeltaTime);
        }

        /// <summary>"Aperte [E] para pegar" → "[E]" em âmbar e negrito.</summary>
        private string Highlight(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            string cor = HudFactory.Hex(Theme.Accent);
            return System.Text.RegularExpressions.Regex.Replace(message, @"\[[^\]]{1,8}\]", m => $"<color={cor}><b>{m.Value}</b></color>");
        }
    }
}
