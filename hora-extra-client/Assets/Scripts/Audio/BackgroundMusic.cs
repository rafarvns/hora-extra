using System.Collections;
using UnityEngine;

namespace HoraExtra.Audio
{
    /// <summary>
    /// Música de fundo de uma cena, em loop.
    ///
    /// O <see cref="AudioSource"/> é criado e configurado por código em vez de ser montado no
    /// Inspector: assim as opções que importam para música (2D, loop, prioridade) ficam
    /// garantidas em toda cena que usar o componente, sem depender de alguém lembrar de
    /// marcá-las de novo.
    ///
    /// Uso: um GameObject vazio na cena com este componente e o clipe atribuído.
    /// NÃO usa DontDestroyOnLoad de propósito — cada cena tem a sua trilha, e uma música
    /// sobrevivente atravessaria o menu para dentro do gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public class BackgroundMusic : MonoBehaviour
    {
        [Header("Trilha")]
        [Tooltip("Clipe tocado em loop enquanto esta cena estiver ativa.")]
        [SerializeField] private AudioClip _clip;

        [Tooltip("Volume final da música (0 a 1).")]
        [SerializeField, Range(0f, 1f)] private float _volume = 0.5f;

        [Tooltip("Segundos de fade-in. Zero começa no volume cheio.")]
        [SerializeField, Min(0f)] private float _fadeInSeconds = 1.5f;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();

            _source.clip = _clip;
            _source.loop = true;
            _source.playOnAwake = false;   // quem dispara é o OnEnable, junto com o fade
            _source.spatialBlend = 0f;     // 2D: música não pode atenuar com a distância do jogador
            _source.priority = 64;         // acima do padrão (128) para não ser descartada por SFX
            _source.volume = 0f;
        }

        private void OnEnable()
        {
            if (_clip == null)
            {
                Debug.LogWarning($"[AUDIO] BackgroundMusic em '{name}' está sem clipe — nada será tocado.");
                return;
            }

            _source.Play();

            if (_fadeInSeconds > 0f)
                StartCoroutine(FadeIn());
            else
                _source.volume = _volume;
        }

        private void OnDisable()
        {
            // Sem isto, uma coroutine de fade pendente volta a subir o volume se o objeto
            // for reativado no meio da transição de cena.
            StopAllCoroutines();
            if (_source != null)
                _source.Stop();
        }

        private IEnumerator FadeIn()
        {
            float t = 0f;
            while (t < _fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;   // unscaled: um menu com timeScale 0 ainda faz o fade
                _source.volume = Mathf.Lerp(0f, _volume, t / _fadeInSeconds);
                yield return null;
            }
            _source.volume = _volume;
        }
    }
}
