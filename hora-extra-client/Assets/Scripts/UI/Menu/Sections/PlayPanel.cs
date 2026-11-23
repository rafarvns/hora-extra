using System.Collections.Generic;
using TMPro;
using UnityEngine;
using HoraExtra.Network.Models;
using HoraExtra.Network.Rest.Services;
using HoraExtra.Settings;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu.Sections
{
    /// <summary>
    /// Painel "Jogar": o jogador digita o nome e cria uma sala ou entra numa da lista.
    /// Não há cadastro. Sala cheia (2/2) aparece bloqueada; sala em andamento pode ser
    /// acessada — quem entra recebe as tarefas que ainda não foram feitas.
    /// </summary>
    public class PlayPanel : MenuPanel
    {
        private const int MAX_SALAS_NA_LISTA = 7;
        private const int NOME_SALA_MAX = 24;

        protected override string Title => "Jogar";

        private TMP_InputField _nome;
        private TMP_InputField _nomeSala;
        private MenuButton _criar;
        private MenuButton _atualizar;
        private RectTransform _lista;
        private TextMeshProUGUI _status;
        private readonly List<MenuButton> _botoesSala = new List<MenuButton>();
        private bool _carregando;

        protected override void BuildContent()
        {
            MenuFactory.SectionTitle(Content, Theme, "SEU NOME");
            _nome = MenuFactory.Input(Content, Theme, GameSettings.Nickname, "Como o colega vai te ver",
                GameSettings.NICKNAME_MAX_LENGTH, v => GameSettings.Nickname = v);
            _nome.onValueChanged.AddListener(_ => RefreshButtons());

            MenuFactory.SectionTitle(Content, Theme, "NOVA SALA");
            _nomeSala = MenuFactory.Input(Content, Theme, string.Empty, "Nome da sala (opcional)", NOME_SALA_MAX, null);
            _criar = MenuFactory.Button(Content, Theme, "CRIAR SALA", Criar, primary: true, height: 58f, subLabel: "máx. 2 jogadores");

            var header = HudFactory.Rect("SalasHeader", Content);
            HudFactory.Row(header.gameObject, 12f, TextAnchor.MiddleLeft);
            HudFactory.Fixed(header.gameObject, -1f, 46f);
            var titulo = MenuFactory.SectionTitle(header, Theme, "SALAS ABERTAS");
            HudFactory.Fixed(titulo.gameObject, -1f, 34f).flexibleWidth = 1f;
            _atualizar = MenuFactory.Button(header, Theme, "Atualizar", Atualizar, height: 42f);
            HudFactory.Fixed(_atualizar.gameObject, 190f, 42f);

            _lista = HudFactory.Rect("Lista", Content);
            var col = HudFactory.Column(_lista.gameObject, 8f);
            col.childForceExpandHeight = false;
            HudFactory.FitContent(_lista.gameObject, false, true);

            _status = HudFactory.Label(Content, "Status", Theme, Theme.BodySize, Theme.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal, wrap: true);
            HudFactory.Fixed(_status.gameObject, -1f, 60f);
            _status.text = string.Empty;

            FirstSelected = _nome.gameObject;
        }

        private void OnEnable()
        {
            if (Menu != null) Menu.OnBusyChanged += AoMudarOcupado;
        }

        private void OnDisable()
        {
            if (Menu != null) Menu.OnBusyChanged -= AoMudarOcupado;
        }

        protected override void OnOpened()
        {
            _nome.SetTextWithoutNotify(GameSettings.Nickname);
            Status(string.Empty, false);
            RefreshButtons();
            Atualizar();
        }

        // === Ações ===

        private string NomeDigitado => (_nome.text ?? string.Empty).Trim();

        private bool NomeValido => NomeDigitado.Length >= 2;

        private void Criar()
        {
            if (!NomeValido) { Status("Digite seu nome (pelo menos 2 letras).", true); return; }
            string sala = (_nomeSala.text ?? string.Empty).Trim();
            if (sala.Length < 2) sala = $"Sala de {NomeDigitado}";
            if (sala.Length > NOME_SALA_MAX) sala = sala.Substring(0, NOME_SALA_MAX);
            Menu.CreateRoom(NomeDigitado, sala);
        }

        private void Entrar(RoomData sala)
        {
            if (!NomeValido) { Status("Digite seu nome (pelo menos 2 letras).", true); return; }
            Menu.JoinRoom(NomeDigitado, sala);
        }

        private void Atualizar() => Atualizar(manterStatus: false);

        /// <param name="manterStatus">true = não troca a mensagem atual (ex: "A sala está cheia").</param>
        private async void Atualizar(bool manterStatus)
        {
            if (_carregando) return;
            _carregando = true;
            if (!manterStatus) Status("Buscando salas…", false);

            var resp = await RoomService.GetRooms();
            _carregando = false;
            if (this == null) return;

            if (resp == null || !resp.Success || resp.Data == null)
            {
                MontarLista(new List<RoomData>());
                if (!manterStatus) Status("Não foi possível buscar as salas. O servidor está online?", true);
                return;
            }

            MontarLista(resp.Data);
            if (!manterStatus) Status(resp.Data.Count == 0 ? "Nenhuma sala aberta. Crie uma!" : string.Empty, false);
        }

        // === Lista ===

        private void MontarLista(List<RoomData> salas)
        {
            foreach (var b in _botoesSala) if (b != null) Destroy(b.gameObject);
            _botoesSala.Clear();

            int n = Mathf.Min(salas.Count, MAX_SALAS_NA_LISTA);
            for (int i = 0; i < n; i++)
            {
                var sala = salas[i];
                string sub = $"{sala.Jogadores}/{sala.Max}" + (sala.EmAndamento ? "  ·  em andamento" : string.Empty);
                var botao = MenuFactory.Button(_lista, Theme, sala.Nome, () => Entrar(sala), height: 52f, subLabel: sub);
                botao.LabelText.characterSpacing = 0f;
                if (sala.Cheia) botao.SetAvailable(false, $"cheia ({sala.Jogadores}/{sala.Max})");
                _botoesSala.Add(botao);
            }
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool livre = !Menu.IsBusy;
            if (!livre) _criar.SetAvailable(false, "conectando…");
            else if (!NomeValido) _criar.SetAvailable(false, "digite seu nome");
            else _criar.SetAvailable(true);
        }

        private void AoMudarOcupado(bool ocupado, string mensagem)
        {
            if (!IsOpen) return;
            Status(mensagem, !ocupado && !string.IsNullOrEmpty(mensagem));
            RefreshButtons();
            // Depois de um erro (ex: sala cheia), a lista pode ter mudado.
            if (!ocupado && !string.IsNullOrEmpty(mensagem)) Atualizar(manterStatus: true);
        }

        private void Status(string texto, bool erro)
        {
            _status.text = texto ?? string.Empty;
            _status.color = erro ? Theme.Danger : Theme.TextMuted;
        }
    }
}
