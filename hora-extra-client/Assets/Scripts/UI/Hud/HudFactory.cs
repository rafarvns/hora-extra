using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Blocos de construção da HUD (painel, texto, barra, keycap), sempre aplicando o
    /// <see cref="HudTheme"/>. Os widgets montam a UI só com estes helpers — assim o visual
    /// fica consistente e mudar o tema muda tudo.
    ///
    /// Tudo é criado com raycastTarget desligado: a HUD in-game não recebe clique e não
    /// pode roubar input do jogo.
    /// </summary>
    public static class HudFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Ancora e pivô no mesmo ponto (ex: (0,1) = canto superior esquerdo), com posição e tamanho.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        /// <summary>Preenche o pai com margens (esquerda, direita, cima, baixo).</summary>
        public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Painel de cantos arredondados (9-slice).</summary>
        public static Image Panel(Transform parent, string name, Color color, HudTheme theme)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = HudSprites.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = theme.CornerSharpness;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Círculo cheio.</summary>
        public static Image Dot(Transform parent, string name, Color color, float size)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = HudSprites.Circle;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            Fixed(img.gameObject, size, size);
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, HudTheme theme, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal, bool wrap = false)
        {
            var tmp = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = theme.ResolveFont();
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Barra horizontal (trilho + preenchimento). Ver <see cref="HudBar"/>.</summary>
        public static HudBar Bar(Transform parent, string name, HudTheme theme, Color fill, float height)
        {
            var track = Panel(parent, name, theme.Track, theme);
            track.pixelsPerUnitMultiplier = Mathf.Max(theme.CornerSharpness, 48f / Mathf.Max(height, 1f));
            var fillImg = Panel(track.transform, "Fill", fill, theme);
            fillImg.pixelsPerUnitMultiplier = track.pixelsPerUnitMultiplier;
            Stretch(fillImg.rectTransform);
            Fixed(track.gameObject, -1f, height);
            return new HudBar(track, fillImg);
        }

        /// <summary>
        /// Tecla desenhada (ex: "E", "Shift"): painel claro com a letra. Devolve o RectTransform
        /// raiz, já com LayoutElement no tamanho certo para entrar em HorizontalLayoutGroup.
        /// </summary>
        public static RectTransform KeyCap(Transform parent, string key, HudTheme theme, float height = 34f)
        {
            float width = Mathf.Max(height, 18f + key.Length * height * 0.36f);
            var cap = Panel(parent, $"Key_{key}", theme.Text, theme);
            cap.pixelsPerUnitMultiplier = theme.CornerSharpness * 2.4f;
            Fixed(cap.gameObject, width, height);

            var label = Label(cap.transform, "Label", theme, height * 0.5f, theme.Panel, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.text = key;
            return cap.rectTransform;
        }

        public static HorizontalLayoutGroup Row(GameObject go, float spacing, TextAnchor align = TextAnchor.MiddleLeft, RectOffset padding = null)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            if (padding != null) h.padding = padding;
            return h;
        }

        public static VerticalLayoutGroup Column(GameObject go, float spacing, RectOffset padding = null)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (padding != null) v.padding = padding;
            return v;
        }

        /// <summary>Faz o objeto crescer conforme o conteúdo (ContentSizeFitter).</summary>
        public static void FitContent(GameObject go, bool horizontal, bool vertical)
        {
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>Tamanho fixo dentro de layouts. Valor negativo = não fixa aquele eixo.</summary>
        public static LayoutElement Fixed(GameObject go, float width, float height)
        {
            // Sem '??': no Editor o GetComponent de componente ausente devolve um "null falso" do Unity.
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (width >= 0f) { le.minWidth = width; le.preferredWidth = width; }
            if (height >= 0f) { le.minHeight = height; le.preferredHeight = height; }
            return le;
        }

        /// <summary>"#RRGGBB" para uso em rich text do TMP.</summary>
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>
    /// Barra de progresso por âncora: o preenchimento é um painel arredondado esticado até
    /// <c>anchorMax.x = valor</c>. Funciona com sprite 9-slice (Image.Filled não combina com Sliced).
    /// </summary>
    public sealed class HudBar
    {
        public Image Track { get; }
        public Image Fill { get; }

        public HudBar(Image track, Image fill)
        {
            Track = track;
            Fill = fill;
        }

        public void SetValue(float value01)
        {
            float v = Mathf.Clamp01(value01);
            var rt = Fill.rectTransform;
            rt.anchorMax = new Vector2(v, 1f);
            rt.offsetMax = Vector2.zero;
            Fill.enabled = v > 0.001f;
        }

        public void SetColor(Color color) => Fill.color = color;
    }
}
