using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using HoraExtra.Network;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>
    /// Coluna principal: logo, tagline e as ações do menu. "Jogar" fica indisponível com o
    /// servidor offline.
    /// </summary>
    public class MainColumn : MenuWidget
    {
        private const float WIDTH = 520f;

        private MenuButton _play;
        private TextMeshProUGUI _status;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(0f, 0.5f), new Vector2(140f, 10f), new Vector2(WIDTH, 820f));
            var col = HudFactory.Column(Root.gameObject, 12f);
            col.childAlignment = TextAnchor.MiddleLeft;

            var logo = HudFactory.Label(Root, "Logo", Theme, 104f, Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            logo.characterSpacing = 4f;
            logo.text = $"HORA<color={HudFactory.Hex(Theme.Accent)}>·</color>EXTRA";
            HudFactory.Fixed(logo.gameObject, -1f, 120f);

            var tagline = HudFactory.Label(Root, "Tagline", Theme, 26f, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Italic);
            tagline.text = "O expediente acabou. Você ainda não.";
            HudFactory.Fixed(tagline.gameObject, -1f, 40f);

            HudFactory.Fixed(HudFactory.Rect("Spacer", Root).gameObject, -1f, 36f);

            _play = MenuFactory.Button(Root, Theme, "JOGAR", () => Menu.OpenPanel<PlayPanel>(), primary: true, height: 70f, subLabel: "criar ou entrar numa sala");
            MenuFactory.Button(Root, Theme, "COMO JOGAR", () => Menu.OpenPanel<HowToPlayPanel>());
            MenuFactory.Button(Root, Theme, "CONFIGURAÇÕES", () => Menu.OpenPanel<SettingsPanel>());
            MenuFactory.Button(Root, Theme, "CRÉDITOS", () => Menu.OpenPanel<CreditsPanel>());
            MenuFactory.Button(Root, Theme, "SAIR", Menu.Quit);

            _status = HudFactory.Label(Root, "Status", Theme, Theme.BodySize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal, wrap: true);
            HudFactory.Fixed(_status.gameObject, -1f, 60f);
            _status.text = string.Empty;
        }

        private void OnEnable()
        {
            Menu.OnServerStatusChanged += Refresh;
            Menu.OnBusyChanged += HandleBusy;
            Refresh();
        }

        private void OnDisable()
        {
            Menu.OnServerStatusChanged -= Refresh;
            Menu.OnBusyChanged -= HandleBusy;
        }

        private void Start()
        {
            // Teclado/gamepad já começam com o botão principal selecionado.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_play.gameObject);
        }

        private void Refresh()
        {
            bool offline = Menu.ServerOnline == false;

            if (Menu.IsBusy) _play.SetAvailable(false, "conectando…");
            else if (offline) _play.SetAvailable(false, "servidor offline");
            else _play.SetAvailable(true);
        }

        private void HandleBusy(bool busy, string message)
        {
            _status.text = message ?? string.Empty;
            _status.color = busy ? Theme.TextMuted : Theme.Danger;
            Refresh();

            // Depois de um erro, devolve a seleção ao botão para tentar de novo pelo teclado.
            if (!busy && EventSystem.current != null && _play.IsInteractable())
                EventSystem.current.SetSelectedGameObject(_play.gameObject);
        }
    }
}
