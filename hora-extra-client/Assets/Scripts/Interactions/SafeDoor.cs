using System.Collections;
using UnityEngine;
using HoraExtra.Audio;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Abre a porta do cofre girando em torno de uma dobradiça.
    ///
    /// O FBX do cofre (SM_Env_Safe) traz a porta como filho separado, mas o pivô dela não
    /// fica necessariamente na dobradiça. Por isso a dobradiça é configurável: um Transform
    /// (<see cref="_dobradica"/>) ou, sem ele, um ponto em coordenadas locais da porta.
    /// Ajuste no Editor olhando o gizmo amarelo.
    /// </summary>
    public class SafeDoor : MonoBehaviour
    {
        [Tooltip("Transform da porta. Vazio = este GameObject.")]
        [SerializeField] private Transform _porta;

        [Tooltip("Ponto da dobradiça. Vazio = usa _dobradicaLocal (coordenadas locais da porta).")]
        [SerializeField] private Transform _dobradica;

        [Tooltip("Dobradiça em coordenadas locais da porta (usado se _dobradica estiver vazio).")]
        [SerializeField] private Vector3 _dobradicaLocal;

        [Tooltip("Eixo de giro em coordenadas locais da porta.")]
        [SerializeField] private Vector3 _eixoLocal = Vector3.up;

        [Tooltip("Ângulo de abertura (graus). Inverta o sinal se a porta abrir para dentro.")]
        [SerializeField] private float _angulo = -100f;

        [SerializeField, Min(0.05f)] private float _duracao = 0.6f;

        [Tooltip("Objetos de cena colados na porta (ex: o teclado numérico) que giram junto com ela.")]
        [SerializeField] private Transform[] _acompanham = new Transform[0];

        [Header("Som (opcional)")]
        [SerializeField] private AudioClip _somAbrir;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        public bool Aberta { get; private set; }

        private Transform Porta => _porta != null ? _porta : transform;

        public void Abrir()
        {
            if (Aberta) return;
            Aberta = true;
            InteractionAudio.PlayOneShot(_somAbrir, _volume);
            StartCoroutine(Girar());
        }

        /// <summary>
        /// Deixa a porta aberta na hora, sem animação nem som — usado quando o cofre já foi
        /// aberto antes (pelo colega, ou por nós antes de trocar de andar).
        /// </summary>
        public void AbrirInstantaneo()
        {
            if (Aberta) return;
            Aberta = true;
            Transform porta = Porta;
            Vector3 pivo = _dobradica != null ? _dobradica.position : porta.TransformPoint(_dobradicaLocal);
            Vector3 eixo = porta.TransformDirection(_eixoLocal.sqrMagnitude > 0f ? _eixoLocal.normalized : Vector3.up);
            GirarTudo(porta, pivo, eixo, _angulo);
        }

        private IEnumerator Girar()
        {
            Transform porta = Porta;
            Vector3 pivo = _dobradica != null ? _dobradica.position : porta.TransformPoint(_dobradicaLocal);
            Vector3 eixo = porta.TransformDirection(_eixoLocal.sqrMagnitude > 0f ? _eixoLocal.normalized : Vector3.up);

            float girado = 0f;
            float t = 0f;
            while (t < _duracao)
            {
                t += Time.deltaTime;
                float alvo = Mathf.SmoothStep(0f, _angulo, Mathf.Clamp01(t / _duracao));
                GirarTudo(porta, pivo, eixo, alvo - girado);
                girado = alvo;
                yield return null;
            }
            GirarTudo(porta, pivo, eixo, _angulo - girado);
        }

        private void GirarTudo(Transform porta, Vector3 pivo, Vector3 eixo, float delta)
        {
            porta.RotateAround(pivo, eixo, delta);
            if (_acompanham == null) return;
            foreach (var t in _acompanham)
                if (t != null && t != porta) t.RotateAround(pivo, eixo, delta);
        }

        private void OnDrawGizmosSelected()
        {
            Transform porta = Porta;
            Vector3 pivo = _dobradica != null ? _dobradica.position : porta.TransformPoint(_dobradicaLocal);
            Vector3 eixo = porta.TransformDirection(_eixoLocal.sqrMagnitude > 0f ? _eixoLocal.normalized : Vector3.up);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pivo, 0.02f);
            Gizmos.DrawLine(pivo - eixo * 0.15f, pivo + eixo * 0.15f);
        }
    }
}
