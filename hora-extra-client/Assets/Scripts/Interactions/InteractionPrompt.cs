using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Prompt de mundo ("Aperte [E] para…"). Encapsula mostrar/ocultar o Text,
    /// eliminando os pares ShowPrompt/HidePrompt duplicados em cada interagível.
    ///
    /// Um único InteractionPrompt costuma ser compartilhado por vários objetos da cena
    /// (todos escrevem no mesmo Text do HUD). Por isso <see cref="Hide"/> só apaga se
    /// quem pediu for o mesmo dono que está exibindo: sem essa checagem, sair do trigger
    /// de um item apaga o prompt que outro acabou de mostrar.
    ///
    /// Toda mudança de estado também sai em <see cref="OnPromptChanged"/>, que é o que a HUD
    /// nova (<c>PromptWidget</c>) desenha. Com <see cref="UseHudRenderer"/> ligado, o Text
    /// legado deixa de ser exibido — o componente continua ativo e com a mesma API.
    /// </summary>
    public class InteractionPrompt : MonoBehaviour
    {
        /// <summary>Valor de progresso que significa "sem barra".</summary>
        public const float NO_PROGRESS = -1f;

        [Header("UI")]
        [Tooltip("Text de mundo/HUD onde a mensagem é exibida. Se vazio, tenta o Text deste GameObject.")]
        [SerializeField] private Text _label;

        [Tooltip("Se marcado, esconde o prompt no Awake.")]
        [SerializeField] private bool _hiddenOnAwake = true;

        /// <summary>
        /// Estado do prompt: (quem emitiu, mensagem, visível, progresso 0..1 ou <see cref="NO_PROGRESS"/>).
        /// </summary>
        public static event Action<InteractionPrompt, string, bool, float> OnPromptChanged;

        /// <summary>
        /// Ligado pela HUD nova: o Text legado não é mais exibido, só o evento é emitido.
        /// </summary>
        public static bool UseHudRenderer { get; set; }

        /// <summary>Quem está exibindo a mensagem atual. Null = prompt ocioso.</summary>
        private object _owner;

        private Coroutine _temporaryRoutine;

        private void Awake()
        {
            if (_label == null)
                _label = GetComponent<Text>();

            if (_label == null)
            {
                Debug.LogWarning($"[UI] InteractionPrompt sem Text atribuído em '{name}' — só a HUD nova exibirá os prompts.");
                return;
            }

            if (_hiddenOnAwake)
                _label.gameObject.SetActive(false);
        }

        /// <summary>Exibe <paramref name="message"/> e registra <paramref name="owner"/> como dono do prompt.</summary>
        public void Show(object owner, string message)
        {
            ShowInternal(owner, message, NO_PROGRESS);
        }

        /// <summary>
        /// Exibe <paramref name="message"/> com uma barra de progresso (ações de segurar [E]).
        /// No Text legado vira "mensagem N%".
        /// </summary>
        public void ShowProgress(object owner, string message, float progress01)
        {
            ShowInternal(owner, message, Mathf.Clamp01(progress01));
        }

        /// <summary>
        /// Exibe uma mensagem por <paramref name="seconds"/> e então apaga.
        /// Usado para o texto de recusa vindo do servidor (task_rejected).
        /// </summary>
        public void ShowTemporary(object owner, string message, float seconds)
        {
            Show(owner, message);
            if (isActiveAndEnabled)
                _temporaryRoutine = StartCoroutine(HideAfter(owner, seconds));
        }

        /// <summary>
        /// Esconde o prompt, mas só se <paramref name="owner"/> for quem está exibindo.
        /// Chamada de quem não é o dono atual é ignorada de propósito.
        /// </summary>
        public void Hide(object owner)
        {
            if (_owner != null && !ReferenceEquals(_owner, owner)) return;
            HideInternal();
        }

        /// <summary>Esconde incondicionalmente. Use em reset de estado, não no fluxo normal.</summary>
        public void ForceHide()
        {
            HideInternal();
        }

        /// <summary>True se <paramref name="owner"/> é quem está exibindo a mensagem atual.</summary>
        public bool IsShowingFor(object owner) => ReferenceEquals(_owner, owner);

        private void ShowInternal(object owner, string message, float progress)
        {
            CancelTemporary();
            _owner = owner;

            if (_label != null)
            {
                _label.text = progress >= 0f ? $"{message} {Mathf.RoundToInt(progress * 100f)}%" : message;
                _label.gameObject.SetActive(!UseHudRenderer);
            }

            OnPromptChanged?.Invoke(this, message, true, progress);
        }

        private void HideInternal()
        {
            bool estavaVisivel = _owner != null || (_label != null && _label.gameObject.activeSelf);

            CancelTemporary();
            _owner = null;
            if (_label != null) _label.gameObject.SetActive(false);

            if (estavaVisivel)
                OnPromptChanged?.Invoke(this, null, false, NO_PROGRESS);
        }

        private IEnumerator HideAfter(object owner, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _temporaryRoutine = null;
            Hide(owner);
        }

        private void CancelTemporary()
        {
            if (_temporaryRoutine != null)
            {
                StopCoroutine(_temporaryRoutine);
                _temporaryRoutine = null;
            }
        }

        private void OnDisable()
        {
            // Sem isto, desativar o objeto com uma mensagem temporária pendente deixa
            // o prompt aceso para sempre — a coroutine morre e ninguém mais apaga.
            CancelTemporary();
            if (_owner != null)
                OnPromptChanged?.Invoke(this, null, false, NO_PROGRESS);
            _owner = null;
        }
    }
}
