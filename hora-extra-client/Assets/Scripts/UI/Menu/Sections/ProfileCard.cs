using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Network;
using HoraExtra.Settings;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>
    /// Cartão de perfil (topo-direita).
    ///   - Sem login: campo de apelido (vira o nome visto pelos colegas na HUD) + Entrar / Criar conta.
    ///   - Com login: nome da conta + Sair da conta.
    /// </summary>
    public class ProfileCard : MenuWidget
    {
        private const float WIDTH = 420f;

        private RectTransform _body;

        protected override void Build()
        {
            HudFactory.Place(Root, new Vector2(1f, 1f), new Vector2(-48f, -48f), new Vector2(WIDTH, 100f));

            var panel = Root.gameObject.AddComponent<Image>();
            panel.sprite = HudSprites.Rounded;
            panel.type = Image.Type.Sliced;
            panel.pixelsPerUnitMultiplier = Theme.CornerSharpness;
            panel.color = HudTheme.WithAlpha(Theme.Panel, 0.88f);
            panel.raycastTarget = true;

            HudFactory.Column(Root.gameObject, 10f, new RectOffset(24, 24, 20, 22));
            HudFactory.FitContent(Root.gameObject, false, true);

            _body = Root;
        }

        private void OnEnable()
        {
            var session = SessionManager.Instance;
            if (session != null) session.OnSessionUpdated += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            var session = SessionManager.Instance;
            if (session != null) session.OnSessionUpdated -= Rebuild;
        }

        private void Rebuild()
        {
            for (int i = _body.childCount - 1; i >= 0; i--)
                Destroy(_body.GetChild(i).gameObject);

            var session = SessionManager.Instance;
            bool logado = session != null && session.IsLoggedIn && session.CurrentPlayer != null;

            if (logado) BuildLoggedIn(session.CurrentPlayer.Nome);
            else BuildGuest();
        }

        private void BuildLoggedIn(string nome)
        {
            Caption("CONECTADO COMO");

            var name = HudFactory.Label(_body, "Name", Theme, Theme.TitleSize * 1.15f, Theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            name.text = string.IsNullOrWhiteSpace(nome) ? "Jogador" : nome;
            name.overflowMode = TextOverflowModes.Ellipsis;
            HudFactory.Fixed(name.gameObject, -1f, 40f);

            MenuFactory.Button(_body, Theme, "Sair da conta", Menu.Logout, height: 48f);
        }

        private void BuildGuest()
        {
            Caption("SEU APELIDO");

            MenuFactory.Input(_body, Theme, GameSettings.Nickname, "Como os colegas vão te ver",
                GameSettings.NICKNAME_MAX_LENGTH, v => GameSettings.Nickname = v);

            var hint = HudFactory.Label(_body, "Hint", Theme, Theme.SmallSize * 0.9f, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal, wrap: true);
            hint.text = "Aparece no painel de equipe dentro do jogo. Em branco, você entra com um nome aleatório.";

            var row = HudFactory.Rect("Account", _body);
            var h = HudFactory.Row(row.gameObject, 10f);
            h.childForceExpandWidth = true;
            HudFactory.Fixed(row.gameObject, -1f, 48f);

            var entrar = MenuFactory.Button(row, Theme, "Entrar", () => Menu.LoadScene(MainMenuRoot.LOGIN_SCENE), height: 48f);
            HudFactory.Fixed(entrar.gameObject, -1f, 48f).flexibleWidth = 1f;
            var criar = MenuFactory.Button(row, Theme, "Criar conta", () => Menu.LoadScene(MainMenuRoot.REGISTER_SCENE), height: 48f);
            HudFactory.Fixed(criar.gameObject, -1f, 48f).flexibleWidth = 1f;
        }

        private void Caption(string text)
        {
            var c = HudFactory.Label(_body, "Caption", Theme, Theme.SmallSize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Bold);
            c.characterSpacing = 6f;
            c.text = text;
            HudFactory.Fixed(c.gameObject, -1f, 24f);
        }
    }
}
