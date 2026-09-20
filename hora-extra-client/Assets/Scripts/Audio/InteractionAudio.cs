using UnityEngine;

namespace HoraExtra.Audio
{
    /// <summary>
    /// Tocador compartilhado de sons pontuais de interação (pegar, guardar, limpar).
    ///
    /// Por que compartilhado e não um AudioSource em cada item: o objeto interagível é
    /// **desativado** no momento em que é consumido (ver PlayerCarrier.Consume). Um
    /// AudioSource nele seria cortado no mesmo frame em que o som deveria tocar. Este
    /// componente vive num GameObject próprio e sobrevive ao item.
    ///
    /// Cria-se sozinho no primeiro uso — nenhuma cena precisa ser preparada. Mesmo padrão
    /// de <see cref="HoraExtra.Characters.TaskSystemBridge.EnsureExists"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionAudio : MonoBehaviour
    {
        private static InteractionAudio _instance;
        private AudioSource _source;

        /// <summary>
        /// Toca <paramref name="clip"/> uma vez, sem loop. Clipe nulo é ignorado em silêncio —
        /// o som é opcional em todo interagível, e faltar um não pode virar erro.
        /// </summary>
        public static void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            var player = EnsureExists();
            if (player == null) return;   // saindo do Play Mode

            player._source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static InteractionAudio EnsureExists()
        {
            if (_instance != null) return _instance;
            if (!Application.isPlaying) return null;

            var go = new GameObject("InteractionAudio (auto-created)");
            _instance = go.AddComponent<InteractionAudio>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            _source = GetComponent<AudioSource>();
            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();

            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;   // 2D: o feedback é da ação do jogador, não do ponto no mundo
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
