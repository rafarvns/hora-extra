using UnityEngine;
using HoraExtra.Network;

namespace HoraExtra.AI
{
    /// <summary>
    /// Script solicitado: Movimenta o NPC de forma aleatória a cada 3 segundos.
    /// O movimento é validado pelo servidor (Authority-First).
    /// </summary>
    public class NPCMovementAI : MonoBehaviour
    {
        [Header("Configurações de IA")]
        [SerializeField, Tooltip("Check box: Se habilitado, o NPC move em direções aleatórias.")]
        private bool _isMovementEnabled = true;

        [SerializeField, Tooltip("Intervalo em segundos entre mudanças de direção.")]
        private float _moveInterval = 3f;

        private NetworkEntity _networkEntity;
        private Animator _animator;
        private float _timer;
        private static readonly int IsWalking = Animator.StringToHash("IsWalking");
        private static readonly int Speed = Animator.StringToHash("Speed");

        private void Awake()
        {
            _networkEntity = GetComponent<NetworkEntity>();
            _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (!_isMovementEnabled || _networkEntity == null || !_networkEntity.IsMaster) return;

            _timer += Time.deltaTime;
            
            // Atualiza animação baseado na distância para o alvo (opcional se houver Animator)
            UpdateAnimation();

            if (_timer >= _moveInterval)
            {
                _timer = 0;
                GenerateRandomStep();
            }
        }

        private void UpdateAnimation()
        {
            if (_animator == null) return;

            // Se o mestre está parado esperando o timer, podemos setar speed 0.
            // Quando envia o RequestMove, o targetPosition muda e o NetworkEntity começa a mover.
            // Como somos o mestre, podemos estimar a velocidade localmente ou via Rigidbody.
            Rigidbody rb = GetComponent<Rigidbody>();
            float currentSpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
            
            _animator.SetFloat(Speed, currentSpeed);
            _animator.SetBool(IsWalking, currentSpeed > 0.1f);
        }

        private void GenerateRandomStep()
        {
            // Escolhe uma direção aleatória num círculo
            Vector2 randomDir = Random.insideUnitCircle * 5f;
            float randomRot = Random.Range(0f, 360f);

            Vector3 targetPos = transform.position + new Vector3(randomDir.x, 0, randomDir.y);

            // Log de depuração para o Master
            Debug.Log($"[AI] NPC {_networkEntity.NetworkId} sorteou novo destino: {targetPos}");
            
            _networkEntity.RequestMove(targetPos, randomRot);
        }
    }
}
