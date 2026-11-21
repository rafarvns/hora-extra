using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu
{
    /// <summary>
    /// Controles INTERATIVOS do menu (botão, slider, toggle, campo de texto, seletor), montados
    /// sobre os primitivos visuais do <see cref="HudFactory"/> e com o mesmo <see cref="HudTheme"/>.
    ///
    /// Diferente da HUD, aqui o raycast fica ligado nos alvos de clique.
    /// </summary>
    public static class MenuFactory
    {
        public static MenuButton Button(Transform parent, HudTheme theme, string label, Action onClick,
            bool primary = false, float height = 58f, string subLabel = null)
        {
            var bg = HudFactory.Panel(parent, $"Btn_{label}", Color.white, theme);
            bg.raycastTarget = true;
            HudFactory.Fixed(bg.gameObject, -1f, height);

            var bar = HudFactory.Rect("Bar", bg.transform);
            bar.anchorMin = new Vector2(0f, 0.2f);
            bar.anchorMax = new Vector2(0f, 0.8f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = new Vector2(8f, 0f);
            bar.sizeDelta = new Vector2(0f, 0f);
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.sprite = HudSprites.Rounded;
            barImg.type = Image.Type.Sliced;
            barImg.pixelsPerUnitMultiplier = 12f;
            barImg.color = primary ? theme.Panel : theme.Accent;
            barImg.raycastTarget = false;

            var text = HudFactory.Label(bg.transform, "Label", theme, height * 0.4f, theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            HudFactory.Stretch(text.rectTransform, 30f, 20f);
            text.characterSpacing = 3f;
            text.text = label;

            var sub = HudFactory.Label(bg.transform, "Sub", theme, theme.SmallSize * 0.9f, theme.TextMuted, TextAlignmentOptions.Right);
            HudFactory.Stretch(sub.rectTransform, 30f, 22f);
            sub.text = subLabel ?? string.Empty;

            var button = bg.gameObject.AddComponent<MenuButton>();
            button.Setup(theme, bg, bar, text, sub, primary, onClick);
            return button;
        }

        public static Slider Slider(Transform parent, HudTheme theme, float min, float max, float value, Action<float> onChanged)
        {
            var root = HudFactory.Rect("Slider", parent);
            HudFactory.Fixed(root.gameObject, -1f, 32f).flexibleWidth = 1f;

            var track = HudFactory.Panel(root, "Track", theme.Track, theme);
            track.pixelsPerUnitMultiplier = 8f;
            CenterBand(track.rectTransform, 8f, 10f);

            var fillArea = HudFactory.Rect("FillArea", root);
            CenterBand(fillArea, 8f, 10f);
            var fill = HudFactory.Panel(fillArea, "Fill", theme.Accent, theme);
            fill.pixelsPerUnitMultiplier = 8f;
            HudFactory.Stretch(fill.rectTransform);

            var handleArea = HudFactory.Rect("HandleArea", root);
            HudFactory.Stretch(handleArea, 10f, 10f);
            var handle = HudFactory.Rect("Handle", handleArea).gameObject.AddComponent<Image>();
            handle.sprite = HudSprites.Circle;
            handle.color = Color.white;
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(24f, -8f);

            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.colors = Tint(theme);
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return slider;
        }

        public static Toggle Toggle(Transform parent, HudTheme theme, bool value, Action<bool> onChanged)
        {
            var box = HudFactory.Panel(parent, "Toggle", theme.PanelRaised, theme);
            box.raycastTarget = true;
            box.pixelsPerUnitMultiplier = theme.CornerSharpness * 3f;
            HudFactory.Fixed(box.gameObject, 34f, 34f);

            var check = HudFactory.Panel(box.transform, "Check", theme.Accent, theme);
            check.pixelsPerUnitMultiplier = theme.CornerSharpness * 4f;
            HudFactory.Stretch(check.rectTransform, 7f, 7f, 7f, 7f);

            var toggle = box.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.colors = Tint(theme);
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return toggle;
        }

        /// <summary>Campo de texto de uma linha. <paramref name="onCommit"/> recebe o texto ao sair do campo.</summary>
        public static TMP_InputField Input(Transform parent, HudTheme theme, string value, string placeholder,
            int characterLimit, Action<string> onCommit)
        {
            var bg = HudFactory.Panel(parent, "Input", theme.PanelRaised, theme);
            bg.raycastTarget = true;
            HudFactory.Fixed(bg.gameObject, -1f, 48f).flexibleWidth = 1f;

            // Montado com o objeto inativo: o TMP_InputField precisa das referências antes do OnEnable.
            bg.gameObject.SetActive(false);

            var area = HudFactory.Rect("TextArea", bg.transform);
            HudFactory.Stretch(area, 16f, 16f, 8f, 8f);
            area.gameObject.AddComponent<RectMask2D>();

            var hint = HudFactory.Label(area, "Placeholder", theme, theme.BodySize, theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
            HudFactory.Stretch(hint.rectTransform);
            hint.text = placeholder;
            hint.alignment = TextAlignmentOptions.MidlineLeft;

            var text = HudFactory.Label(area, "Text", theme, theme.BodySize, theme.Text, TextAlignmentOptions.MidlineLeft);
            HudFactory.Stretch(text.rectTransform);
            text.richText = false;

            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = theme.ResolveFont();
            input.pointSize = theme.BodySize;
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.caretColor = theme.Accent;
            input.customCaretColor = true;
            input.selectionColor = HudTheme.WithAlpha(theme.Accent, 0.35f);
            input.targetGraphic = bg;
            input.colors = Tint(theme);
            input.SetTextWithoutNotify(value ?? string.Empty);
            input.onEndEdit.AddListener(v => onCommit?.Invoke(v));

            bg.gameObject.SetActive(true);
            return input;
        }

        /// <summary>
        /// Seletor "‹ valor ›" com setas — substitui dropdown (mais simples de navegar por
        /// teclado/gamepad e sem template de lista para montar).
        /// </summary>
        public static MenuCycler Cycler(Transform parent, HudTheme theme, IList<string> options, int index, Action<int> onChanged)
        {
            var root = HudFactory.Rect("Cycler", parent);
            HudFactory.Row(root.gameObject, 8f, TextAnchor.MiddleCenter);
            HudFactory.Fixed(root.gameObject, -1f, 44f).flexibleWidth = 1f;

            var cycler = root.gameObject.AddComponent<MenuCycler>();
            var prev = Button(root, theme, "<", () => cycler.Step(-1), height: 44f);
            HudFactory.Fixed(prev.gameObject, 52f, 44f);

            var value = HudFactory.Label(root, "Value", theme, theme.BodySize, theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            HudFactory.Fixed(value.gameObject, -1f, 44f).flexibleWidth = 1f;

            var next = Button(root, theme, ">", () => cycler.Step(1), height: 44f);
            HudFactory.Fixed(next.gameObject, 52f, 44f);

            foreach (var b in new[] { prev, next })
            {
                HudFactory.Stretch(b.LabelText.rectTransform);
                b.LabelText.alignment = TextAlignmentOptions.Center;
            }

            cycler.Setup(value, options, index, onChanged);
            return cycler;
        }

        /// <summary>Linha "rótulo .......... controle" usada nos painéis.</summary>
        public static RectTransform FieldRow(Transform parent, HudTheme theme, string label, float labelWidth = 260f)
        {
            var row = HudFactory.Rect($"Row_{label}", parent);
            HudFactory.Row(row.gameObject, 18f, TextAnchor.MiddleLeft);
            HudFactory.Fixed(row.gameObject, -1f, 48f);

            var text = HudFactory.Label(row, "Label", theme, theme.BodySize, theme.Text, TextAlignmentOptions.MidlineLeft);
            text.text = label;
            HudFactory.Fixed(text.gameObject, labelWidth, 48f);
            return row;
        }

        /// <summary>Título de seção em versalete ("ÁUDIO", "CONTROLES"...).</summary>
        public static TextMeshProUGUI SectionTitle(Transform parent, HudTheme theme, string title)
        {
            var t = HudFactory.Label(parent, "Section", theme, theme.SmallSize, theme.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
            t.characterSpacing = 8f;
            t.text = title;
            HudFactory.Fixed(t.gameObject, -1f, 34f);
            return t;
        }

        /// <summary>Faixa horizontal centralizada de altura fixa (trilho de slider).</summary>
        private static void CenterBand(RectTransform rt, float height, float horizontalPadding)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(horizontalPadding, -height / 2f);
            rt.offsetMax = new Vector2(-horizontalPadding, height / 2f);
        }

        private static ColorBlock Tint(HudTheme theme)
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = Color.Lerp(Color.white, theme.Accent, 0.55f);
            c.selectedColor = Color.Lerp(Color.white, theme.Accent, 0.55f);
            c.pressedColor = theme.Accent;
            c.disabledColor = HudTheme.WithAlpha(Color.white, 0.35f);
            c.fadeDuration = 0.08f;
            return c;
        }
    }

    /// <summary>Estado do seletor criado por <see cref="MenuFactory.Cycler"/>.</summary>
    public class MenuCycler : MonoBehaviour
    {
        private TextMeshProUGUI _value;
        private IList<string> _options;
        private Action<int> _onChanged;

        public int Index { get; private set; }

        internal void Setup(TextMeshProUGUI value, IList<string> options, int index, Action<int> onChanged)
        {
            _value = value;
            _options = options;
            _onChanged = onChanged;
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1));
            Refresh();
        }

        public void Step(int delta)
        {
            if (_options == null || _options.Count == 0) return;
            Index = (Index + delta + _options.Count) % _options.Count;
            Refresh();
            _onChanged?.Invoke(Index);
        }

        private void Refresh()
        {
            _value.text = _options != null && _options.Count > 0 ? _options[Index] : "-";
        }
    }
}
