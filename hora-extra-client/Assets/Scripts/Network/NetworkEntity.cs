using UnityEngine;
using HoraExtra.Network.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HoraExtra.Network
{
    /// <summary>
    /// Gerencia o estado de rede de um NPC ou Boss.
    /// Registra a entidade no servidor e recebe atualizações de posição.
    /// </summary>
    public class NetworkEntity : MonoBehaviour
    {
        [Header("Configurações de Rede")]
        [SerializeField, Tooltip("ID único para este NPC no backend. Deve ser idêntico em todos os clientes.")]
        private string _networkId;

        [SerializeField, Tooltip("Tipo da entidade (npc ou boss).")]
        private string _entityType = "npc";

        [SerializeField, Tooltip("Interpolação suave para movimentos remotos.")]
        private float _interpolationSpeed = 10f;

        private Vector3 _targetPosition;
        private float _targetRotation;
        private bool _isMaster = false;
        private Rigidbody _rb;

        public string NetworkId => _networkId;
        public bool IsMaster => _isMaster;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            
            // Inicializar alvos com a posição ATUAL para evitar pulo para (0,0,0)
            _targetPosition = transform.position;
            _targetRotation = transform.eulerAngles.y;
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(_networkId))
                _networkId = gameObject.name + "_" + GetInstanceID();

            // Sincronizar com o SocketManager
            SocketManager.Instance.On(NetworkEvents.NPC_MOVE, OnMoveReceived);
            SocketManager.Instance.On(NetworkEvents.NPC_REGISTERED, OnMoveReceived); // Usar o mesmo handler para sincronia inicial
            
            RegisterOnServer();
        }

        private void RegisterOnServer()
        {
            var payload = new NpcRegisterPayload
            {
                id = _networkId,
                type = _entityType,
                p = new float[] { transform.position.x, transform.position.y, transform.position.z },
                r = transform.eulerAngles.y,
                name = gameObject.name
            };

            SocketManager.Instance.Emit(NetworkEvents.NPC_REGISTER, payload);
            Debug.Log($"[NETWORK] Registrando entidade {_networkId} ({_entityType}) no servidor.");
        }

        private void OnRegistered(JToken data)
        {
            string receivedId = data["id"]?.ToString();
            if (receivedId != _networkId) return;

            // Define se eu sou o Mestre deste NPC (autoridade de IA)
            _isMaster = data["isMaster"]?.Value<bool>() ?? false;
            
            if (_isMaster)
                Debug.Log($"[NETWORK] Eu sou o MASTER para {_networkId}. Controlando IA local.");
            else
                Debug.Log($"[NETWORK] {_networkId} registrado. Sincronia passiva ativada.");

            // Sincroniza a posição inicial vinda do servidor
            UpdateTargetFromToken(data);
        }

        private void OnMoveReceived(JToken data)
        {
            string receivedId = data["id"]?.ToString();
            if (receivedId != _networkId) return;
            
            UpdateTargetFromToken(data);
        }

        private void UpdateTargetFromToken(JToken data)
        {
            float[] p = data["p"]?.ToObject<float[]>();
            float r = data["r"]?.Value<float>() ?? 0f;

            if (p != null && p.Length == 3)
            {
                _targetPosition = new Vector3(p[0], p[1], p[2]);
                _targetRotation = r;
            }
        }

        private void Update()
        {
            // Se não houver Rigidbody, a interpolação ocorre no Update via Transform direto (modo cinemático)
            if (_rb == null)
            {
                ApplySmoothMovement();
            }
        }

        private void FixedUpdate()
        {
            // Se houver Rigidbody, a movimentação deve ocorrer no FixedUpdate para respeitar a física
            if (_rb != null)
            {
                ApplyPhysicalMovement();
            }
        }

        private void ApplySmoothMovement()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _interpolationSpeed);
            
            Quaternion targetRot = Quaternion.Euler(0, _targetRotation, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _interpolationSpeed);
        }

        private void ApplyPhysicalMovement()
        {
            // Interpolação de posição física (considera colisões)
            Vector3 nextPos = Vector3.MoveTowards(transform.position, _targetPosition, Time.fixedDeltaTime * _interpolationSpeed);
            _rb.MovePosition(nextPos);

            // Interpolação de rotação física
            Quaternion targetRot = Quaternion.Euler(0, _targetRotation, 0);
            Quaternion nextRot = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * _interpolationSpeed);
            _rb.MoveRotation(nextRot);
        }

        public void RequestMove(Vector3 position, float rotation)
        {
            var payload = new NpcMovePayload
            {
                id = _networkId,
                p = new float[] { position.x, position.y, position.z },
                r = rotation
            };

            SocketManager.Instance.Emit(NetworkEvents.NPC_MOVE_REQUEST, payload);
        }
    }
}
