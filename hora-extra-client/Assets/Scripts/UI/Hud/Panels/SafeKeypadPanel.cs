using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Audio;
using HoraExtra.Interactions;

namespace HoraExtra.UI.Hud.Panels
{
    /// <summary>
    /// Teclado numérico do cofre (Tarefa 6). Overlay no centro da tela, digitado pelo
    /// teclado físico ([0-9], [Backspace], [Enter], [Esc]) — não precisa de EventSystem
    /// nem de cursor solto.
    ///
    /// Instância única criada sob demanda e persistente entre cenas. Quem abre decide se a
    /// senha está certa (<c>validar</c>); o painel só cuida de digitar e do feedback.
    /// Completar os dígitos já confirma, sem precisar de [Enter].
    /// </summary>
    public class SafeKeypadPanel : MonoBehaviour
    {
        private const float ERRO_SEGUNDOS = 0.9f;
        private const float SUCESSO_SEGUNDOS = 0.6f;

        private static SafeKeypadPanel _instance;

        public static bool IsOpen => _instance != null && _instance._aberto;

        private HudTheme _theme;
        private GameObject _canvasGo;
        private TextMeshProUGUI _titulo;
        private TextMeshProUGUI _visor;
        private TextMeshProUGUI _status;
        private Image _moldura;

        private bool _aberto;
        private bool _travado; // durante o feedback de erro/sucesso
        private int _digitos;
        private string _digitado = string.Empty;
        private Func<string, bool> _validar;
        private Action _aoAbrir;
        private Action _aoCancelar;
        private AudioClip _somDigito;
        private float _volumeDigito = 1f;

        /// <summary>
        /// Abre o teclado.
        /// </summary>
        /// <param name="titulo">Ex: "COFRE".</param>
        /// <param name="digitos">Quantidade de dígitos da senha.</param>
        /// <param name="validar">Recebe o que foi digitado; true = senha certa.</param>
        /// <param name="aoAbrir">Chamado depois do feedback verde, com o painel já fechado.</param>
        /// <param name="aoCancelar">Chamado ao sair com [Esc].</param>
        /// <param name="somDigito">Tocado a cada dígito digitado (SFX_Password). Null = sem som.
        /// Vem de quem abre porque o painel é criado em runtime e não tem Inspector.</param>
        /// <param name="volumeDigito">Volume do <paramref name="somDigito"/> (0..1).</param>
        public static void Open(string titulo, int digitos, Func<string, bool> validar, Action aoAbrir, Action aoCancelar,
            AudioClip somDigito = null, float volumeDigito = 1f)
        {
            var painel = Ensure();
            painel._somDigito = somDigito;
            painel._volumeDigito = volumeDigito;
            painel.AbrirInterno(titulo, digitos, validar, aoAbrir, aoCancelar);
        }

        /// <summary>Fecha sem chamar callbacks (ex: o cofre saiu de cena).</summary>
        public static void CloseSilently()
        {
            if (_instance != null) _instance.Fechar();
        }

        private static SafeKeypadPanel Ensure()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("SafeKeypadPanel (runtime)");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SafeKeypadPanel>();
            _instance.Montar();
            return _instance;
        }

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

            var root = (RectTransform)_canvasGo.transform;

            // Escurece o fundo para focar no teclado.
            var fundo = HudFactory.Panel(root, "Dim", new Color(0f, 0f, 0f, 0.45f), _theme);
            fundo.sprite = null;
            HudFactory.Stretch(fundo.rectTransform);

            _moldura = HudFactory.Panel(root, "Keypad", _theme.Panel, _theme);
            HudFactory.Place(_moldura.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 330f));
            HudFactory.Column(_moldura.gameObject, 18f, new RectOffset(32, 32, 28, 28)).childAlignment = TextAnchor.UpperCenter;

            _titulo = HudFactory.Label(_moldura.transform, "Titulo", _theme, _theme.TitleSize, _theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            HudFactory.Fixed(_titulo.gameObject, -1f, 36f);

            var visorFundo = HudFactory.Panel(_moldura.transform, "VisorFundo", _theme.PanelRaised, _theme);
            HudFactory.Fixed(visorFundo.gameObject, -1f, 96f);
            _visor = HudFactory.Label(visorFundo.transform, "Visor", _theme, 64f, _theme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            HudFactory.Stretch(_visor.rectTransform);
            _visor.characterSpacing = 30f;

            _status = HudFactory.Label(_moldura.transform, "Status", _theme, _theme.BodySize, _theme.TextMuted, TextAlignmentOptions.Center);
            HudFactory.Fixed(_status.gameObject, -1f, 30f);

            var dicas = HudFactory.Label(_moldura.transform, "Dicas", _theme, _theme.SmallSize, _theme.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal, wrap: true);
            HudFactory.Fixed(dicas.gameObject, -1f, 48f);
            string a = HudFactory.Hex(_theme.Text);
            dicas.text = $"<color={a}>[0-9]</color> digitar   <color={a}>[Backspace]</color> apagar\n" +
                         $"<color={a}>[Enter]</color> confirmar   <color={a}>[Esc]</color> sair";

            _canvasGo.SetActive(false);
        }

        private void AbrirInterno(string titulo, int digitos, Func<string, bool> validar, Action aoAbrir, Action aoCancelar)
        {
            StopAllCoroutines();
            _digitos = Mathf.Max(1, digitos);
            _validar = validar;
            _aoAbrir = aoAbrir;
            _aoCancelar = aoCancelar;
            _digitado = string.Empty;
            _travado = false;
            _aberto = true;

            _titulo.text = string.IsNullOrWhiteSpace(titulo) ? "COFRE" : titulo;
            MostrarStatus("Digite a senha", _theme.TextMuted);
            AtualizarVisor(_theme.Accent);
            _canvasGo.SetActive(true);
        }

        private void Update()
        {
            if (!_aberto || _travado) return;

            if (InteractionInput.CancelPressed())
            {
                var cancelar = _aoCancelar;
                Fechar();
                cancelar?.Invoke();
                return;
            }

            if (InteractionInput.BackspacePressed() && _digitado.Length > 0)
            {
                _digitado = _digitado.Substring(0, _digitado.Length - 1);
                AtualizarVisor(_theme.Accent);
                return;
            }

            if (InteractionInput.DigitPressed(out int d) && _digitado.Length < _digitos)
            {
                _digitado += d.ToString();
                InteractionAudio.PlayOneShot(_somDigito, _volumeDigito);
                AtualizarVisor(_theme.Accent);
                if (_digitado.Length == _digitos) Confirmar();
                return;
            }

            if (InteractionInput.SubmitPressed()) Confirmar();
        }

        private void Confirmar()
        {
            if (_digitado.Length < _digitos)
            {
                MostrarStatus($"A senha tem {_digitos} dígitos", _theme.Danger);
                return;
            }

            bool certa = _validar != null && _validar(_digitado);
            StartCoroutine(certa ? Sucesso() : Erro());
        }

        private IEnumerator Erro()
        {
            _travado = true;
            MostrarStatus("Senha incorreta", _theme.Danger);
            AtualizarVisor(_theme.Danger);
            yield return new WaitForSeconds(ERRO_SEGUNDOS);
            _digitado = string.Empty;
            MostrarStatus("Tente de novo", _theme.TextMuted);
            AtualizarVisor(_theme.Accent);
            _travado = false;
        }

        private IEnumerator Sucesso()
        {
            _travado = true;
            MostrarStatus("Cofre destravado!", _theme.Success);
            AtualizarVisor(_theme.Success);
            yield return new WaitForSeconds(SUCESSO_SEGUNDOS);
            var abrir = _aoAbrir;
            Fechar();
            abrir?.Invoke();
        }

        private void Fechar()
        {
            StopAllCoroutines();
            _aberto = false;
            _travado = false;
            _digitado = string.Empty;
            _validar = null;
            _aoAbrir = null;
            _aoCancelar = null;
            if (_canvasGo != null) _canvasGo.SetActive(false);
        }

        private void AtualizarVisor(Color cor)
        {
            var sb = new System.Text.StringBuilder(_digitos);
            for (int i = 0; i < _digitos; i++)
                sb.Append(i < _digitado.Length ? _digitado[i] : '_');
            _visor.text = sb.ToString();
            _visor.color = cor;
        }

        private void MostrarStatus(string texto, Color cor)
        {
            _status.text = texto;
            _status.color = cor;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
