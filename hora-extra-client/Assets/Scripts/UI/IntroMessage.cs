using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Characters;

namespace HoraExtra.UI
{
    /// <summary>
    /// Mensagem de abertura da partida: aparece em fade, fica alguns segundos e some.
    /// Só depois disso o jogador recebe a primeira tarefa.
    ///
    /// A UI é construída em runtime em vez de montada no Inspector para que a cena não
    /// precise de nenhum setup — e para que o componente funcione igual em qualquer cena
    /// de level. Se preferir controlar a aparência à mão, atribua <c>_label</c> e o
    /// componente usa o Text existente em vez de criar o seu.
    /// </summary>
    public class IntroMessage : MonoBehaviour
    {
        [Header("Texto")]
        [Tooltip("Mensagem exibida ao entrar na partida.")]
        [SerializeField, TextArea]
        private string _message = "Organize o escritório antes que os funcionários cheguem";

        [Tooltip("Text opcional. Vazio = o componente cria um Canvas e um Text proprios.")]
        [SerializeField] private Text _label;

        [Header("Tempos (segundos)")]
        [SerializeField, Min(0f)] private float _fadeInSeconds = 0.6f;
        [SerializeField, Min(0f)] private float _holdSeconds = 5f;
        [SerializeField, Min(0f)] private float _fadeOutSeconds = 0.8f;

        [Header("Tarefas")]
        [Tooltip("Se marcado, segura a atribuição das tarefas até a mensagem terminar.")]
        [SerializeField] private bool _holdTasksUntilDone = true;

        private void Awake()
        {
            // Precisa ser no Awake: o TaskSystemBridge agenda o pedido de tarefas poucos
            // décimos depois de registrar o catálogo, e a trava tem que já estar de pé.
            if (_holdTasksUntilDone)
                TaskSystemBridge.HoldTaskRequest = true;

            if (_label == null)
                _label = CriarUI();
        }

        private void Start()
        {
            StartCoroutine(Exibir());
        }

        private IEnumerator Exibir()
        {
            if (_label == null)
            {
                Debug.LogWarning("[UI] IntroMessage — sem Text; introducao pulada e tarefas liberadas.");
                Liberar();
                yield break;
            }

            Debug.Log($"[UI] IntroMessage — exibindo \"{_message}\" por {_holdSeconds}s.");
            _label.text = _message;
            _label.gameObject.SetActive(true);

            yield return Fade(0f, 1f, _fadeInSeconds);
            yield return new WaitForSeconds(_holdSeconds);
            yield return Fade(1f, 0f, _fadeOutSeconds);

            _label.gameObject.SetActive(false);
            Liberar();
        }

        private IEnumerator Fade(float de, float para, float duracao)
        {
            if (duracao <= 0f)
            {
                SetAlpha(para);
                yield break;
            }

            float t = 0f;
            while (t < duracao)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(de, para, t / duracao));
                yield return null;
            }
            SetAlpha(para);
        }

        private void SetAlpha(float a)
        {
            if (_label == null) return;
            var c = _label.color;
            c.a = a;
            _label.color = c;
        }

        /// <summary>Libera a atribuição de tarefas. Idempotente.</summary>
        private void Liberar()
        {
            if (!TaskSystemBridge.HoldTaskRequest) return;

            TaskSystemBridge.HoldTaskRequest = false;
            Debug.Log("[UI] IntroMessage — introducao concluida, tarefas liberadas.");
        }

        private void OnDisable()
        {
            // Sair do Play Mode (ou desativar o objeto) no meio da introducao nao pode
            // deixar a trava ligada: nenhuma tarefa seria atribuida na proxima sessao.
            Liberar();
        }

        /// <summary>
        /// Acha uma fonte utilizável, em ordem de preferência.
        ///
        /// O nome da fonte embutida mudou entre versões do Unity (Arial.ttf virou
        /// LegacyRuntime.ttf em 2022.2), e nem todo projeto tem a legada disponível. O
        /// último recurso pega a fonte de qualquer Text já existente na cena — se o HUD
        /// desenha texto, essa fonte funciona aqui também.
        /// </summary>
        private static Font ObterFonte()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) { Debug.Log("[UI] IntroMessage — fonte: LegacyRuntime.ttf"); return f; }

            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) { Debug.Log("[UI] IntroMessage — fonte: Arial.ttf"); return f; }

            var algumText = FindFirstObjectByType<Text>(FindObjectsInactive.Include);
            if (algumText != null && algumText.font != null)
            {
                Debug.Log($"[UI] IntroMessage — fonte emprestada de '{algumText.name}': {algumText.font.name}");
                return algumText.font;
            }

            return null;
        }

        /// <summary>Monta um Canvas overlay com um Text centralizado.</summary>
        private Text CriarUI()
        {
            var fonte = ObterFonte();
            if (fonte == null)
            {
                Debug.LogError("[UI] IntroMessage — nenhuma fonte disponivel; atribua um Text no campo _label.");
                return null;
            }

            var canvasGO = new GameObject("IntroCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;          // acima do HUD
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var textGO = new GameObject("IntroText");
            textGO.transform.SetParent(canvasGO.transform, false);

            var text = textGO.AddComponent<Text>();
            text.font = fonte;
            text.fontSize = 46;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(1f, 1f, 1f, 0f);   // começa invisível: o fade cuida

            var sombra = textGO.AddComponent<Outline>();
            sombra.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sombra.effectDistance = new Vector2(2f, -2f);

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 120f);   // um pouco acima do centro
            rt.sizeDelta = new Vector2(1400f, 300f);

            return text;
        }
    }
}
