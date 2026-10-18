using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Interactions;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Slot "na mão" (baixo-direita): o que o jogador carrega e quanto ("Papéis 3/6").
    /// Âmbar quando a pilha enche; ferramentas aparecem como "Ferramenta". Vazio = "Mãos livres".
    /// Dados: <see cref="PlayerCarrier"/> (evento OnCarryChanged + leitura do estado).
    /// </summary>
    public class CarrySlotWidget : HudWidget
    {
        private const float RETRY_SECONDS = 1f;

        private CanvasGroup _group;
        private Image _icon;
        private TextMeshProUGUI _iconLetter;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _count;

        private PlayerCarrier _carrier;
        private float _nextFindAt;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(1f, 0f), new Vector2(-Theme.ScreenMargin, Theme.ScreenMargin + 40f), new Vector2(300f, 84f));
            _group = Root.gameObject.AddComponent<CanvasGroup>();

            var panel = HudFactory.Panel(Root, "Panel", Theme.Panel, Theme);
            HudFactory.Stretch(panel.rectTransform);
            HudFactory.Row(panel.gameObject, 14f, TextAnchor.MiddleLeft, new RectOffset(14, 18, 12, 12));

            _icon = HudFactory.Panel(panel.transform, "Icon", Theme.PanelRaised, Theme);
            _icon.pixelsPerUnitMultiplier = Theme.CornerSharpness * 1.6f;
            HudFactory.Fixed(_icon.gameObject, 58f, 58f);
            _iconLetter = HudFactory.Label(_icon.transform, "Letter", Theme, 30f, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            HudFactory.Stretch(_iconLetter.rectTransform);

            var texts = HudFactory.Rect("Texts", panel.transform);
            HudFactory.Column(texts.gameObject, 0f).childAlignment = TextAnchor.MiddleLeft;
            HudFactory.Fixed(texts.gameObject, -1f, -1f).flexibleWidth = 1f;

            _caption = HudFactory.Label(texts, "Caption", Theme, Theme.SmallSize * 0.85f, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Bold);
            _caption.characterSpacing = 6f;
            _name = HudFactory.Label(texts, "Name", Theme, Theme.TitleSize * 0.85f, Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);

            _count = HudFactory.Label(panel.transform, "Count", Theme, Theme.TitleSize, Theme.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            HudFactory.Fixed(_count.gameObject, 70f, 58f);
        }

        private void OnEnable()
        {
            PlayerCarrier.OnCarryChanged += HandleCarryChanged;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerCarrier.OnCarryChanged -= HandleCarryChanged;
        }

        private void Update()
        {
            // O Player pode nascer depois da HUD; procura até achar.
            if (_carrier == null && Time.unscaledTime >= _nextFindAt)
            {
                _nextFindAt = Time.unscaledTime + RETRY_SECONDS;
                _carrier = FindFirstObjectByType<PlayerCarrier>();
                if (_carrier != null) Refresh();
            }
        }

        private void HandleCarryChanged(CarryableItem top)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_carrier == null || !_carrier.IsCarrying)
            {
                _group.alpha = 0.55f;
                _icon.color = Theme.PanelRaised;
                _iconLetter.text = string.Empty;
                _caption.text = "NA MÃO";
                _caption.color = Theme.TextMuted;
                _name.text = "Mãos livres";
                _name.color = Theme.TextMuted;
                _count.text = string.Empty;
                return;
            }

            _group.alpha = 1f;
            string kind = _carrier.CarriedKind;
            int n = _carrier.CarriedCount;
            bool ferramenta = _carrier.Carried != null && _carrier.Carried.IsTool;

            string nome = TaskPresentation.GetItemLabel(kind, n);
            _iconLetter.text = string.IsNullOrEmpty(nome) ? "?" : nome.Substring(0, 1);
            _icon.color = ferramenta ? Theme.CategoryClean : Theme.Info;
            _name.text = nome;
            _name.color = Theme.Text;

            if (ferramenta)
            {
                _caption.text = "FERRAMENTA";
                _caption.color = Theme.CategoryClean;
                _count.text = string.Empty;
                return;
            }

            _caption.text = _carrier.IsFull ? "MÃO CHEIA" : "NA MÃO";
            _caption.color = _carrier.IsFull ? Theme.Accent : Theme.TextMuted;
            _count.text = $"{n}<size=60%><color={HudFactory.Hex(Theme.TextMuted)}>/{_carrier.MaxStack}</color></size>";
            _count.color = _carrier.IsFull ? Theme.Accent : Theme.Text;
        }
    }
}
