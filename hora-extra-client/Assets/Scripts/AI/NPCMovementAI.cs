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

        [SerializeField, Tooltip("Distância máxima do passo aleatório.")]
        private float _maxStepDistance = 5f;

        [SerializeField, Tooltip("Layer que contém obstáculos intransponíveis (Waredes, etc).")]
        private LayerMask _obstacleMask;

        [SerializeField, Tooltip("Folga de distância de colisão (evita encostar na parede).")]
        private float _collisionBuffer = 0.5f;

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
            Vector2 randomDir = Random.insideUnitCircle * _maxStepDistance;
            float randomRot = Random.Range(0f, 360f);

            Vector3 currentPos = transform.position;
            Vector3 offset = new Vector3(randomDir.x, 0, randomDir.y);
            Vector3 targetPos = currentPos + offset;

            // --- MELHORIA: Detecção de Obstáculos ---
            // Faz um Raycast do centro do NPC até o destino para evitar atravessar paredes
            Vector3 rayStart = currentPos + Vector3.up * 0.5f; // Sobe um pouco o raio do chão
            Vector3 direction = offset.normalized;
            float distance = offset.magnitude;

            if (Physics.Raycast(rayStart, direction, out RaycastHit hit, distance, _obstacleMask))
            {
                // Se atingir algo, o novo destino é um pouco ANTES do ponto de impacto
                targetPos = hit.point - (direction * _collisionBuffer);
                targetPos.y = currentPos.y; // Mantém a altura original
                
                Debug.Log($"[AI] NPC {_networkEntity.NetworkId} detectou obstáculo! Ajustando destino para: {targetPos}");
            }
            else
            {
                // Se não atingir parede, ainda verificamos se o destino Final é seguro (ex: não dentro de algo)
                if (Physics.CheckSphere(targetPos + Vector3.up * 0.5f, 0.4f, _obstacleMask))
                {
                    Debug.Log($"[AI] NPC {_networkEntity.NetworkId} destino sorteado estava dentro de parede. Abortando passo.");
                    return; 
                }
            }

            // Log de depuração para o Master
            Debug.Log($"[AI] NPC {_networkEntity.NetworkId} solicitando movimento para: {targetPos}");
            
            _networkEntity.RequestMove(targetPos, randomRot);
        }
    }
}
