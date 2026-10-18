using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Characters;
using HoraExtra.Interactions;
using HoraExtra.UI.Hud.Widgets;

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Raiz da HUD in-game. Basta UM GameObject com este componente na cena de level:
    /// ele cria o próprio Canvas, os componentes de dados (<see cref="ShiftClock"/>,
    /// <see cref="RoomRoster"/>) e todos os widgets, com o visual do <see cref="HudTheme"/>.
    ///
    /// Com <c>_hideLegacyHud</c> marcado, esconde a HUD antiga (MissionHud, StaminaBarUI, ping do
    /// NetworkUI e o Text do InteractionPrompt) sem desativar os componentes que a gameplay
    /// ainda usa — o InteractionPrompt continua ativo e só deixa de desenhar o Text legado.
    ///
    /// Layout (referência 1920×1080):
    ///   topo-esq: tarefa atual · topo-centro: relógio · topo-dir: rede + equipe
    ///   direita: avisos · baixo-esq: teclas · baixo-centro: prompt + fôlego · baixo-dir: mão
    /// </summary>
    [DisallowMultipleComponent]
    public class HudRoot : MonoBehaviour
    {
        [Header("Tema")]
        [Tooltip("Vazio = carrega Resources/UI/SO_HudTheme (ou um tema padrão em memória).")]
        [SerializeField] private HudTheme _theme;

        [Header("Canvas")]
        [SerializeField] private int _sortingOrder = 100;

        [Header("Widgets")]
        [SerializeField] private bool _showClock = true;
        [SerializeField] private bool _showMissionCard = true;
        [SerializeField] private bool _showTeam = true;
        [SerializeField] private bool _showToasts = true;
        [SerializeField] private bool _showPrompt = true;
        [SerializeField] private bool _showCarrySlot = true;
        [SerializeField] private bool _showStamina = true;
        [SerializeField] private bool _showKeyHints = true;
        [SerializeField] private bool _showObjectiveMarker = true;
        [SerializeField] private bool _showNetworkStatus = true;

        [Header("HUD legada")]
        [Tooltip("Esconde MissionHud, StaminaBarUI, o ping do NetworkUI e o Text do InteractionPrompt.")]
        [SerializeField] private bool _hideLegacyHud = true;

        public static HudRoot Instance { get; private set; }

        public HudTheme Theme { get; private set; }

        private RectTransform _canvasRoot;
        private ToastWidget _toasts;

        /// <summary>Publica um aviso na HUD (no-op se não houver HUD na cena).</summary>
        public static void Toast(ToastWidget.Kind kind, string title, string body = null)
        {
            if (Instance != null && Instance._toasts != null)
                Instance._toasts.Show(kind, title, body);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[UI] HudRoot duplicado na cena — removendo o extra.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // ShiftClock/RoomRoster assinam eventos do SocketManager no OnEnable: ele precisa existir antes.
            SocketManager.EnsureExists();

            Theme = _theme != null ? _theme : HudTheme.LoadOrDefault();
            InteractionPrompt.UseHudRenderer = _hideLegacyHud && _showPrompt;

            gameObject.AddComponent<ShiftClock>();
            gameObject.AddComponent<RoomRoster>();

            BuildCanvas();

            // Ordem = ordem de desenho: o marcador de mundo fica por baixo dos painéis.
            if (_showObjectiveMarker) Add<ObjectiveMarkerWidget>("ObjectiveMarker");
            if (_showMissionCard)     Add<MissionCardWidget>("MissionCard");
            if (_showClock)           Add<ShiftClockWidget>("ShiftClock");
            if (_showNetworkStatus)   Add<NetworkStatusWidget>("NetworkStatus");
            if (_showTeam)            Add<TeamWidget>("Team");
            if (_showKeyHints)        Add<KeyHintsWidget>("KeyHints");
            if (_showStamina)         Add<StaminaWidget>("Stamina");
            if (_showCarrySlot)       Add<CarrySlotWidget>("CarrySlot");
            if (_showPrompt)          Add<PromptWidget>("Prompt");
            if (_showToasts)          _toasts = Add<ToastWidget>("Toasts");

            Debug.Log("[UI] HudRoot — HUD in-game montada.");
        }

        private void Start()
        {
            // No Start, e não no Awake: os componentes legados já rodaram o próprio Awake/OnEnable.
            if (_hideLegacyHud) HideLegacyHud();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            InteractionPrompt.UseHudRenderer = false;
        }

        private void BuildCanvas()
        {
            var go = new GameObject("HUD_Canvas (runtime)", typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Sem GraphicRaycaster de propósito: a HUD não é clicável e não pode roubar input.
            _canvasRoot = (RectTransform)go.transform;
        }

        /// <summary>Cria o GameObject INATIVO, monta o widget e só então ativa (ver HudWidget).</summary>
        private T Add<T>(string name) where T : HudWidget
        {
            var rt = HudFactory.Rect(name, _canvasRoot);
            rt.gameObject.SetActive(false);
            var widget = rt.gameObject.AddComponent<T>();
            widget.Init(this, Theme);
            rt.gameObject.SetActive(true);
            return widget;
        }

        private static void HideLegacyHud()
        {
            int escondidos = 0;

            foreach (var m in FindObjectsByType<MissionHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                m.HideLegacyVisuals();
                escondidos++;
            }
            foreach (var s in FindObjectsByType<StaminaBarUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                s.HideLegacyVisuals();
                escondidos++;
            }
            foreach (var n in FindObjectsByType<NetworkUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                n.SetPingVisible(false);
                escondidos++;
            }

            Debug.Log($"[UI] HudRoot — HUD legada escondida ({escondidos} componente(s)).");
        }
    }
}
