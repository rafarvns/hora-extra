using TMPro;
using UnityEngine;

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Tema visual da HUD in-game: paleta, fonte, tamanhos e forma.
    ///
    /// Todas as cores/tamanhos da HUD saem daqui — nenhum widget hardcoda cor. Para mudar o
    /// visual, edite o asset <c>Assets/Resources/UI/SO_HudTheme.asset</c> no Inspector (vale em
    /// Play Mode, mas widgets já construídos só pegam a mudança no próximo Play).
    /// </summary>
    [CreateAssetMenu(fileName = "SO_HudTheme", menuName = "Hora-Extra/HUD Theme")]
    public class HudTheme : ScriptableObject
    {
        public const string RESOURCES_PATH = "UI/SO_HudTheme";

        [Header("Fonte")]
        [Tooltip("Fonte TMP da HUD. Vazio = fonte padrão do TMP (LiberationSans SDF).")]
        public TMP_FontAsset Font;

        [Header("Superfícies")]
        public Color Panel       = new Color32(0x14, 0x1C, 0x2B, 0xE0);
        public Color PanelRaised = new Color32(0x22, 0x2D, 0x42, 0xF0);
        public Color Track       = new Color32(0xFF, 0xFF, 0xFF, 0x22);
        public Color Shadow      = new Color32(0x00, 0x00, 0x00, 0x66);

        [Header("Texto")]
        public Color Text      = new Color32(0xF2, 0xF4, 0xF8, 0xFF);
        public Color TextMuted = new Color32(0x9A, 0xA6, 0xB8, 0xFF);

        [Header("Semânticas")]
        [Tooltip("Identidade 'hora extra' — destaques, relógio, item cheio.")]
        public Color Accent  = new Color32(0xFF, 0xB0, 0x20, 0xFF);
        public Color Success = new Color32(0x3D, 0xDC, 0x84, 0xFF);
        public Color Danger  = new Color32(0xFF, 0x5A, 0x5F, 0xFF);
        public Color Info    = new Color32(0x4D, 0xA3, 0xFF, 0xFF);

        [Header("Categorias de tarefa (badge)")]
        public Color CategoryOrganize = new Color32(0x4D, 0xA3, 0xFF, 0xFF);
        public Color CategoryClean    = new Color32(0x2E, 0xC4, 0xB6, 0xFF);
        public Color CategoryCoffee   = new Color32(0xC0, 0x84, 0x52, 0xFF);
        public Color CategoryOther    = new Color32(0x9A, 0xA6, 0xB8, 0xFF);

        [Header("Tamanhos de fonte")]
        public float ClockSize = 52f;
        public float TitleSize = 28f;
        public float BodySize  = 21f;
        public float SmallSize = 17f;

        [Header("Forma")]
        [Tooltip("Arredondamento dos painéis. Maior = cantos mais retos (multiplicador de pixels por unidade do 9-slice).")]
        [Range(0.5f, 6f)] public float CornerSharpness = 2.2f;

        [Tooltip("Margem das bordas da tela, em pixels de referência (1920×1080).")]
        public float ScreenMargin = 28f;

        [Header("Relógio")]
        [Tooltip("Quantos minutos de jogo antes do fim o relógio fica em destaque (âmbar).")]
        public float ClockWarningMinutes = 60f;

        [Tooltip("Quantos minutos de jogo antes do fim aparece o aviso (toast).")]
        public float ClockReminderMinutes = 30f;

        public TMP_FontAsset ResolveFont() => Font != null ? Font : TMP_Settings.defaultFontAsset;

        public Color CategoryColor(TaskPresentation.TaskCategory category)
        {
            switch (category)
            {
                case TaskPresentation.TaskCategory.Organize: return CategoryOrganize;
                case TaskPresentation.TaskCategory.Clean:    return CategoryClean;
                case TaskPresentation.TaskCategory.Coffee:   return CategoryCoffee;
                default:                                     return CategoryOther;
            }
        }

        /// <summary>
        /// Tema do Resources; sem o asset, um tema em memória com os valores padrão acima —
        /// a HUD nunca fica sem visual por falta de setup.
        /// </summary>
        public static HudTheme LoadOrDefault()
        {
            var theme = Resources.Load<HudTheme>(RESOURCES_PATH);
            if (theme != null) return theme;

            Debug.LogWarning($"[UI] HudTheme não encontrado em Resources/{RESOURCES_PATH} — usando tema padrão em memória.");
            return CreateInstance<HudTheme>();
        }

        /// <summary>Mesma cor com outro alpha.</summary>
        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
