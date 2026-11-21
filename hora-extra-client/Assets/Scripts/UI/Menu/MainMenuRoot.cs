using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using HoraExtra.Network;
using HoraExtra.UI.Hud;
using HoraExtra.UI.Menu.Sections;

namespace HoraExtra.UI.Menu
{
    /// <summary>
    /// Raiz do menu principal. Basta UM GameObject com este componente na MainMenuScene: ele
    /// cria o próprio Canvas, garante EventSystem e SessionManager (restaurando o login salvo)
    /// e monta todas as seções com o mesmo <see cref="HudTheme"/> da HUD in-game.
    ///
    /// Com <c>_hideLegacyMenu</c> marcado, desativa o Canvas antigo (MainMenuController) — que
    /// continua na cena: desmarcar a flag traz o menu antigo de volta.
    ///
    /// Também é o "controlador" das ações: jogar como convidado, trocar de cena, sair, e a
    /// pilha de painéis (Esc fecha o do topo).
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuRoot : MonoBehaviour
    {
        public const string LOBBY_SCENE = "LobbyScene";
        public const string LOGIN_SCENE = "LoginScene";
        public const string REGISTER_SCENE = "CreateAccountScene";

        [Header("Tema")]
        [Tooltip("Vazio = Resources/UI/SO_HudTheme (o mesmo da HUD in-game).")]
        [SerializeField] private HudTheme _theme;

        [Header("Cenas")]
        [SerializeField] private string _guestGameScene = GuestPlayFlow.DEFAULT_GAME_SCENE;

        [Header("Menu legado")]
        [Tooltip("Desativa o Canvas antigo do MainMenuController.")]
        [SerializeField] private bool _hideLegacyMenu = true;

        public HudTheme Theme { get; private set; }

        /// <summary>null = ainda não checado; true/false = último resultado do health check.</summary>
        public bool? ServerOnline { get; private set; }
        public float ServerLatencyMs { get; private set; }
        public event Action OnServerStatusChanged;

        /// <summary>True enquanto o "Jogar agora" está conectando.</summary>
        public bool IsBusy { get; private set; }
        public event Action<bool, string> OnBusyChanged;   // (ocupado, mensagem de status/erro)

        private RectTransform _canvasRoot;
        private CanvasGroup _mainLayer;
        private readonly Dictionary<Type, MenuPanel> _panels = new Dictionary<Type, MenuPanel>();
        private readonly Stack<MenuPanel> _openPanels = new Stack<MenuPanel>();
        private readonly Stack<GameObject> _selectionBeforePanel = new Stack<GameObject>();

        private void Awake()
        {
            Theme = _theme != null ? _theme : HudTheme.LoadOrDefault();

            // Restaura o login salvo (PlayerPrefs) já na primeira tela.
            SessionManager.EnsureExists();
            EnsureEventSystem();
            if (_hideLegacyMenu) HideLegacyMenu();

            BuildCanvas();

            Add<MenuBackdrop>("Backdrop", _canvasRoot);

            var layer = HudFactory.Rect("MainLayer", _canvasRoot);
            HudFactory.Stretch(layer);
            _mainLayer = layer.gameObject.AddComponent<CanvasGroup>();

            Add<MainColumn>("MainColumn", layer);
            Add<ProfileCard>("ProfileCard", layer);
            Add<MenuFooter>("Footer", layer);

            AddPanel<HowToPlayPanel>("Panel_HowToPlay");
            AddPanel<SettingsPanel>("Panel_Settings");
            AddPanel<CreditsPanel>("Panel_Credits");

            Debug.Log("[UI] MainMenuRoot — menu principal montado.");
        }

        private void Update()
        {
            if (_openPanels.Count > 0 && BackPressed())
                ClosePanel(_openPanels.Peek());
        }

        // === Ações ===

        public async void PlayAsGuest()
        {
            if (IsBusy) return;
            SetBusy(true, "Conectando ao servidor…");

            var result = await GuestPlayFlow.Start(_guestGameScene);

            // Sucesso troca de cena: este objeto já está sendo destruído.
            if (this == null) return;
            if (!result.Success) SetBusy(false, result.Error);
        }

        public void LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[UI] MainMenuRoot — cena '{sceneName}' não está no Build Settings.");
                return;
            }
            Debug.Log($"[UI] MainMenuRoot — carregando {sceneName}.");
            SceneManager.LoadScene(sceneName);
        }

        public void Quit()
        {
            Debug.Log("[UI] MainMenuRoot — saindo do jogo.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Logout()
        {
            SessionManager.Instance?.ClearSession();
            NetworkSettings.ClearToken();
        }

        // === Painéis ===

        public void OpenPanel<T>() where T : MenuPanel
        {
            if (!_panels.TryGetValue(typeof(T), out var panel) || panel.IsOpen) return;

            _selectionBeforePanel.Push(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null);
            _openPanels.Push(panel);
            panel.transform.SetAsLastSibling();
            panel.SetVisible(true);
            _mainLayer.interactable = false;
        }

        public void ClosePanel(MenuPanel panel)
        {
            if (panel == null || !panel.IsOpen || _openPanels.Count == 0 || _openPanels.Peek() != panel) return;

            _openPanels.Pop();
            panel.SetVisible(false);
            _mainLayer.interactable = _openPanels.Count == 0;

            var anterior = _selectionBeforePanel.Count > 0 ? _selectionBeforePanel.Pop() : null;
            if (anterior != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(anterior);
        }

        // === Estado compartilhado ===

        internal void ReportServerStatus(bool online, float latencyMs)
        {
            bool mudou = ServerOnline != online;
            ServerOnline = online;
            ServerLatencyMs = latencyMs;
            if (mudou) Debug.Log($"[UI] MainMenuRoot — servidor {(online ? "online" : "offline")} ({BackendConfig.Host}).");
            OnServerStatusChanged?.Invoke();
        }

        private void SetBusy(bool busy, string message)
        {
            IsBusy = busy;
            OnBusyChanged?.Invoke(busy, message);
        }

        // === Montagem ===

        private void BuildCanvas()
        {
            var go = new GameObject("Menu_Canvas (runtime)", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            _canvasRoot = (RectTransform)go.transform;
        }

        private T Add<T>(string name, Transform parent) where T : MenuWidget
        {
            var rt = HudFactory.Rect(name, parent);
            rt.gameObject.SetActive(false);
            var widget = rt.gameObject.AddComponent<T>();
            widget.Init(this, Theme);
            rt.gameObject.SetActive(true);
            return widget;
        }

        private void AddPanel<T>(string name) where T : MenuPanel
        {
            _panels[typeof(T)] = Add<T>(name, _canvasRoot);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Debug.Log("[UI] MainMenuRoot — EventSystem criado.");
        }

        private static void HideLegacyMenu()
        {
            foreach (var legacy in FindObjectsByType<MainMenuController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                legacy.gameObject.SetActive(false);
                Debug.Log($"[UI] MainMenuRoot — menu legado '{legacy.name}' desativado.");
            }
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }
    }
}
