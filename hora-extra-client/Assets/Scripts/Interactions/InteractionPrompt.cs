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
    /// </summary>
    public class InteractionPrompt : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Text de mundo/HUD onde a mensagem é exibida. Se vazio, tenta o Text deste GameObject.")]
        [SerializeField] private Text _label;

        [Tooltip("Se marcado, esconde o prompt no Awake.")]
        [SerializeField] private bool _hiddenOnAwake = true;

        /// <summary>Quem está exibindo a mensagem atual. Null = prompt ocioso.</summary>
        private object _owner;

        private Coroutine _temporaryRoutine;

        private void Awake()
        {
            if (_label == null)
                _label = GetComponent<Text>();

            if (_label == null)
            {
                Debug.LogWarning($"[UI] InteractionPrompt sem Text atribuído em '{name}' — prompts não serão exibidos.");
                return;
            }

            if (_hiddenOnAwake)
                _label.gameObject.SetActive(false);
        }

        /// <summary>Exibe <paramref name="message"/> e registra <paramref name="owner"/> como dono do prompt.</summary>
        public void Show(object owner, string message)
        {
            if (_label == null) return;

            CancelTemporary();
            _owner = owner;
            _label.text = message;
            _label.gameObject.SetActive(true);
        }

        /// <summary>
        /// Exibe uma mensagem por <paramref name="seconds"/> e então apaga.
        /// Usado para o texto de recusa vindo do servidor (task_rejected).
        /// </summary>
        public void ShowTemporary(object owner, string message, float seconds)
        {
            if (_label == null) return;

            Show(owner, message);
            _temporaryRoutine = StartCoroutine(HideAfter(owner, seconds));
        }

        /// <summary>
        /// Esconde o prompt, mas só se <paramref name="owner"/> for quem está exibindo.
        /// Chamada de quem não é o dono atual é ignorada de propósito.
        /// </summary>
        public void Hide(object owner)
        {
            if (_label == null) return;
            if (_owner != null && !ReferenceEquals(_owner, owner)) return;

            CancelTemporary();
            _owner = null;
            _label.gameObject.SetActive(false);
        }

        /// <summary>Esconde incondicionalmente. Use em reset de estado, não no fluxo normal.</summary>
        public void ForceHide()
        {
            if (_label == null) return;

            CancelTemporary();
            _owner = null;
            _label.gameObject.SetActive(false);
        }

        /// <summary>True se <paramref name="owner"/> é quem está exibindo a mensagem atual.</summary>
        public bool IsShowingFor(object owner) => ReferenceEquals(_owner, owner);

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
            _owner = null;
        }
    }
}
