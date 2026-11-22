using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HoraExtra.Interactions;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace HoraExtra.UI.Hud.Panels
{
    /// <summary>
    /// Tela do computador do Financeiro (Tarefa 7 — "Enviar arquivos/e-mails"), no estilo
    /// do "Upload Data": duas pastas, um documento que viaja de uma para a outra, barra de
    /// progresso e o botão <b>"Enviar email"</b>.
    ///
    /// Clicável: tem Canvas próprio com GraphicRaycaster (o canvas da HUD não tem, de
    /// propósito) e garante que exista um EventSystem na cena. [Enter] também envia e [Esc]
    /// fecha. Fechar no meio do envio cancela e zera a barra.
    ///
    /// O painel não fala com a rede: avisa o dono por <c>aoTerminarEnvio</c> e o dono chama
    /// <see cref="MostrarConcluido"/> ou <see cref="MostrarFalha"/> conforme o servidor.
    /// </summary>
    public class EmailUploadPanel : MonoBehaviour
    {
        private enum Estado { Pronto, Enviando, AguardandoServidor, Concluido }

        private const float FECHAR_APOS_CONCLUIR = 1.2f;

        private static EmailUploadPanel _instance;

        public static bool IsOpen => _instance != null && _instance._aberto;

        private HudTheme _theme;
        private GameObject _canvasGo;
        private TextMeshProUGUI _mensagem;
        private TextMeshProUGUI _percentual;
        private HudBar _barra;
        private RectTransform _documento;
        private RectTransform _pastaOrigem;
        private RectTransform _pastaDestino;
        private Button _botaoEnviar;
        private TextMeshProUGUI _textoBotao;

        private bool _aberto;
        private Estado _estado;
        private float _segundos;
        private float _progresso;
        private float _fecharEm;
        private Action _aoTerminarEnvio;
        private Action _aoFechar;

        /// <summary>
        /// Abre a tela.
        /// </summary>
        /// <param name="segundosEnvio">Duração da barra.</param>
        /// <param name="aoTerminarEnvio">Barra cheia: o dono envia o resultado ao servidor.</param>
        /// <param name="aoFechar">Tela fechada (por [Esc], botão X ou após concluir).</param>
        public static void Open(float segundosEnvio, Action aoTerminarEnvio, Action aoFechar)
        {
            Ensure().AbrirInterno(segundosEnvio, aoTerminarEnvio, aoFechar);
        }

        /// <summary>O servidor confirmou: mostra "Enviado!" e fecha sozinho.</summary>
        public static void MostrarConcluido()
        {
            if (_instance == null || !_instance._aberto) return;
            _instance._estado = Estado.Concluido;
            _instance._fecharEm = Time.unscaledTime + FECHAR_APOS_CONCLUIR;
            _instance.Mensagem("E-mail enviado!", _instance._theme.Success);
            _instance._percentual.text = "100%";
            _instance._barra.SetValue(1f);
            _instance._barra.SetColor(_instance._theme.Success);
            _instance._botaoEnviar.interactable = false;
            _instance._textoBotao.text = "Enviado";
        }

        /// <summary>O servidor não confirmou: volta ao início para tentar de novo.</summary>
        public static void MostrarFalha(string motivo)
        {
            if (_instance == null || !_instance._aberto) return;
            _instance.Resetar();
            _instance.Mensagem(string.IsNullOrWhiteSpace(motivo) ? "Falha no envio. Tente de novo." : motivo, _instance._theme.Danger);
        }

        /// <summary>Fecha sem chamar callbacks (ex: o computador saiu de cena).</summary>
        public static void CloseSilently()
        {
            if (_instance == null) return;
            _instance._aoFechar = null;
            _instance.Fechar();
        }

        private static EmailUploadPanel Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("EmailUploadPanel (runtime)");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<EmailUploadPanel>();
            _instance.Montar();
            return _instance;
        }

        // =====================================================================
        // Montagem
        // =====================================================================

        private void Montar()
        {
            _theme = HudRoot.Instance != null ? HudRoot.Instance.Theme : HudTheme.LoadOrDefault();

            _canvasGo = new GameObject("Canvas", typeof(RectTransform));
            _canvasGo.layer = 5;
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();

            var root = (RectTransform)_canvasGo.transform;

            var fundo = HudFactory.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.5f), _theme);
            fundo.sprite = null;
            HudFactory.Stretch(fundo.rectTransform);

            // Janela do "monitor".
            var janela = HudFactory.Panel(root, "Janela", _theme.Panel, _theme);
            HudFactory.Place(janela.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 480f));

            // Barra de título.
            var titulo = HudFactory.Panel(janela.transform, "BarraTitulo", _theme.PanelRaised, _theme);
            HudFactory.Place(titulo.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(820f, 56f));
            var tituloTxt = HudFactory.Label(titulo.transform, "Titulo", _theme, _theme.BodySize, _theme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            HudFactory.Stretch(tituloTxt.rectTransform, 24f, 80f, 0f, 0f);
            tituloTxt.text = "Horizon Mail — Financeiro";

            var fechar = CriarBotao(titulo.transform, "Fechar", "X", _theme.Danger, Fechar);
            HudFactory.Place((RectTransform)fechar.transform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(44f, 40f));

            _mensagem = HudFactory.Label(janela.transform, "Mensagem", _theme, _theme.TitleSize, _theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            HudFactory.Place(_mensagem.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(760f, 40f));

            // Pastas e documento.
            _pastaOrigem = CriarPasta(janela.transform, "Financeiro", new Vector2(-250f, 30f));
            _pastaDestino = CriarPasta(janela.transform, "Diretoria", new Vector2(250f, 30f));

            var doc = HudFactory.Panel(janela.transform, "Documento", _theme.Text, _theme);
            doc.pixelsPerUnitMultiplier = _theme.CornerSharpness * 3f;
            _documento = doc.rectTransform;
            HudFactory.Place(_documento, new Vector2(0.5f, 0.5f), _pastaOrigem.anchoredPosition, new Vector2(42f, 54f));
            var linhas = HudFactory.Label(_documento, "Linhas", _theme, 14f, _theme.TextMuted, TextAlignmentOptions.Center);
            HudFactory.Stretch(linhas.rectTransform);
            linhas.text = "@";

            // Barra de progresso.
            var barraHolder = HudFactory.Rect("BarraHolder", janela.transform);
            HudFactory.Place(barraHolder, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(640f, 22f));
            _barra = HudFactory.Bar(barraHolder, "Barra", _theme, _theme.Info, 22f);
            HudFactory.Stretch((RectTransform)_barra.Track.transform);

            _percentual = HudFactory.Label(janela.transform, "Percentual", _theme, _theme.SmallSize, _theme.TextMuted, TextAlignmentOptions.Center);
            HudFactory.Place(_percentual.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(640f, 26f));

            // Botão "Enviar email".
            _botaoEnviar = CriarBotao(janela.transform, "Enviar", "Enviar email", _theme.Accent, AoClicarEnviar);
            HudFactory.Place((RectTransform)_botaoEnviar.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(260f, 62f));
            _textoBotao = _botaoEnviar.GetComponentInChildren<TextMeshProUGUI>();

            var dica = HudFactory.Label(janela.transform, "Dica", _theme, _theme.SmallSize, _theme.TextMuted, TextAlignmentOptions.Right);
            HudFactory.Place(dica.rectTransform, new Vector2(1f, 0f), new Vector2(-20f, 10f), new Vector2(300f, 24f));
            dica.text = "[Enter] enviar · [Esc] sair";

            _canvasGo.SetActive(false);
        }

        private RectTransform CriarPasta(Transform pai, string nome, Vector2 posicao)
        {
            var pasta = HudFactory.Rect($"Pasta_{nome}", pai);
            HudFactory.Place(pasta, new Vector2(0.5f, 0.5f), posicao, new Vector2(170f, 150f));

            var aba = HudFactory.Panel(pasta, "Aba", HudTheme.WithAlpha(_theme.Accent, 0.85f), _theme);
            HudFactory.Place(aba.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -18f), new Vector2(70f, 26f));

            var corpo = HudFactory.Panel(pasta, "Corpo", _theme.Accent, _theme);
            HudFactory.Place(corpo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(170f, 92f));

            var rotulo = HudFactory.Label(pasta, "Rotulo", _theme, _theme.BodySize, _theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            HudFactory.Place(rotulo.rectTransform, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(200f, 28f));
            rotulo.text = nome;
            return pasta;
        }

        private Button CriarBotao(Transform pai, string nome, string texto, Color cor, UnityEngine.Events.UnityAction aoClicar)
        {
            var img = HudFactory.Panel(pai, $"Botao_{nome}", cor, _theme);
            img.raycastTarget = true; // HudFactory desliga por padrão; botão precisa receber clique
            var botao = img.gameObject.AddComponent<Button>();
            botao.targetGraphic = img;
            var cores = botao.colors;
            cores.normalColor = Color.white;
            cores.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cores.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cores.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            botao.colors = cores;
            botao.onClick.AddListener(aoClicar);

            var label = HudFactory.Label(img.transform, "Texto", _theme, _theme.BodySize, _theme.Panel, TextAlignmentOptions.Center, FontStyles.Bold);
            HudFactory.Stretch(label.rectTransform);
            label.text = texto;
            return botao;
        }

        // =====================================================================
        // Fluxo
        // =====================================================================

        private void AbrirInterno(float segundosEnvio, Action aoTerminarEnvio, Action aoFechar)
        {
            GarantirEventSystem();

            _segundos = Mathf.Max(0.5f, segundosEnvio);
            _aoTerminarEnvio = aoTerminarEnvio;
            _aoFechar = aoFechar;
            _aberto = true;
            Resetar();
            _canvasGo.SetActive(true);
        }

        private void Resetar()
        {
            _estado = Estado.Pronto;
            _progresso = 0f;
            _barra.SetValue(0f);
            _barra.SetColor(_theme.Info);
            _percentual.text = "0%";
            _documento.anchoredPosition = _pastaOrigem.anchoredPosition;
            _documento.gameObject.SetActive(false);
            _botaoEnviar.interactable = true;
            _textoBotao.text = "Enviar email";
            Mensagem("Existe 1 e-mail pendente de envio", _theme.Text);
        }

        private void AoClicarEnviar()
        {
            if (_estado != Estado.Pronto) return;
            _estado = Estado.Enviando;
            _progresso = 0f;
            _botaoEnviar.interactable = false;
            _textoBotao.text = "Enviando...";
            _documento.gameObject.SetActive(true);
            Mensagem("Enviando e-mail...", _theme.Text);
        }

        private void Update()
        {
            if (!_aberto) return;

            if (_estado == Estado.Concluido)
            {
                if (Time.unscaledTime >= _fecharEm) Fechar();
                return;
            }

            if (InteractionInput.CancelPressed())
            {
                Fechar();
                return;
            }

            if (_estado == Estado.Pronto && InteractionInput.SubmitPressed())
            {
                AoClicarEnviar();
                return;
            }

            if (_estado != Estado.Enviando) return;

            _progresso += Time.deltaTime / _segundos;
            float p = Mathf.Clamp01(_progresso);
            _barra.SetValue(p);
            _percentual.text = $"{Mathf.FloorToInt(p * 100f)}%";

            // O documento faz um arco entre as pastas e reinicia a cada "pacote".
            float ciclo = Mathf.Repeat(p * 4f, 1f);
            Vector2 a = _pastaOrigem.anchoredPosition;
            Vector2 b = _pastaDestino.anchoredPosition;
            _documento.anchoredPosition = Vector2.Lerp(a, b, ciclo) + Vector2.up * Mathf.Sin(ciclo * Mathf.PI) * 70f;

            if (p >= 1f)
            {
                _estado = Estado.AguardandoServidor;
                _documento.gameObject.SetActive(false);
                _textoBotao.text = "Confirmando...";
                Mensagem("Confirmando envio...", _theme.TextMuted);
                _aoTerminarEnvio?.Invoke();
            }
        }

        private void Fechar()
        {
            if (!_aberto) return;
            _aberto = false;
            if (_canvasGo != null) _canvasGo.SetActive(false);

            var fechar = _aoFechar;
            _aoFechar = null;
            _aoTerminarEnvio = null;
            fechar?.Invoke();
        }

        private void Mensagem(string texto, Color cor)
        {
            _mensagem.text = texto;
            _mensagem.color = cor;
        }

        /// <summary>
        /// Os andares 2 e 3 não têm EventSystem autorado e nenhum persiste entre cenas; sem
        /// ele o botão não recebe clique.
        /// </summary>
        private static void GarantirEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem (runtime)", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Debug.Log("[UI] EmailUploadPanel — EventSystem criado em runtime.");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
