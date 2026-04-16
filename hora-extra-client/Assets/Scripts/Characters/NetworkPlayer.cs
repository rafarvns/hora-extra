using UnityEngine;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Representa um jogador remoto (outro player na sala).
    /// Aplica interpolação suave às posições e rotações recebidas via rede.
    /// </summary>
    public class NetworkPlayer : MonoBehaviour
    {
        [Header("Configurações de Interpolação")]
        [SerializeField, Tooltip("Velocidade de suavização do movimento.")]
        private float _moveLerpSpeed = 10f;

        [SerializeField, Tooltip("Velocidade de suavização da rotação.")]
        private float _rotationLerpSpeed = 10f;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private string _playerId;
        private bool _isSprinting;

        public void Initialize(string id, string playerName)
        {
            _playerId = id;
            gameObject.name = $"Player_{playerName} ({id})";
            
            // Posição inicial
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
        }

        /// <summary>
        /// Atualiza o alvo de interpolação com dados vindos do servidor.
        /// </summary>
        public void UpdateState(float[] position, float rotation)
        {
            _targetPosition = new Vector3(position[0], position[1], position[2]);
            _targetRotation = Quaternion.Euler(0, rotation, 0);
        }

        /// <summary>
        /// Atualiza o estado de corrida vindo do servidor.
        /// </summary>
        public void UpdateSprintState(bool isSprinting)
        {
            _isSprinting = isSprinting;
            // Aqui poderíamos disparar animações se houvesse um Animator:
            // GetComponentInChildren<Animator>().SetBool("IsRunning", isSprinting);
        }

        private void Update()
        {
            // Interpolação Suave (Evita Stuttering)
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _moveLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * _rotationLerpSpeed);
        }
    }
}
