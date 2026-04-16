using UnityEngine;
using UnityEngine.InputSystem;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Gerencia a movimentação FPS (WASD) e rotação da câmera (Mouse).
    /// Utiliza o novo Input System do Unity via captura direta.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Configurações de Movimento")]
        [SerializeField, Tooltip("Velocidade de caminhada.")]
        private float _walkSpeed = 6.0f;

        [SerializeField, Tooltip("Força da gravidade.")]
        private float _gravity = -9.81f;

        [Header("Configurações de Câmera (Mouse Look)")]
        [SerializeField, Tooltip("Referência para a câmera filha.")]
        private Transform _playerCamera;

        [SerializeField, Range(0.01f, 5f), Tooltip("Sensibilidade do mouse (X e Y). Ajustável entre 0.01 e 5.0.")]
        private float _lookSensitivity = 0.5f;

        [SerializeField, Tooltip("Limite superior para olhar para cima.")]
        private float _upperLookLimit = 80f;

        [SerializeField, Tooltip("Limite inferior para olhar para baixo.")]
        private float _lowerLookLimit = -80f;

        [Header("Configurações de Rede")]
        [SerializeField, Tooltip("Frequência de atualização de rede (pacotes por segundo).")]
        private float _networkTickRate = 20f;

        [SerializeField, Tooltip("Se ativado, o jogador só se move após receber o pacote do servidor (Authoritative).")]
        private bool _onlyMoveViaNetwork = true;

        [SerializeField, Tooltip("Distância máxima permitida entre local e servidor antes de forçar um Snap (Reconciliação).")]
        private float _reconciliationThreshold = 1.0f;

        private CharacterBase _characterBase;
        private CharacterController _characterController;
        private Vector3 _velocity;
        private float _verticalRotation = 0f;
        private bool _canMove = true;
        
        // Controle de Rede
        private float _nextNetworkTick;
        private Vector3 _lastSentPosition;
        private float _lastSentRotation;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _characterBase = GetComponent<CharacterBase>();
            
            // Inicializa a câmera se não for atribuída manualmente
            if (_playerCamera == null)
            {
                _playerCamera = GetComponentInChildren<Camera>()?.transform;
            }
        }

        private void Start()
        {
            // Trava o cursor no centro da tela
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Registrar para receber o próprio movimento de volta do servidor (Reconciliação)
            SocketManager.Instance.On(NetworkEvents.PLAYER_MOVE, (data) => {
                if (!_onlyMoveViaNetwork) return;
                
                string id = data["id"]?.ToString();
                if (id == SocketManager.Instance.LocalPlayerId) {
                    var pos = data["p"];
                    Vector3 serverPos = new Vector3((float)pos[0], (float)pos[1], (float)pos[2]);
                    
                    if (Vector3.Distance(transform.position, serverPos) > _reconciliationThreshold)
                    {
                        Debug.Log($"[NETWORK] Reconciliação necessária: Diff {Vector3.Distance(transform.position, serverPos):F2}m. Snapping.");
                        _characterController.enabled = false;
                        transform.position = serverPos;
                        _characterController.enabled = true;
                    }
                }
            });

            // Inicializa última posição para evitar salto no primeiro frame
            _lastSentPosition = transform.position;
            _lastSentRotation = transform.eulerAngles.y;

            // Tenta aterrar o player no início para evitar o bug de "flutuar" na cena
            SnapToGround();
        }

        /// <summary>
        /// Força o player a encostar no chão no início da execução.
        /// Resolve o bug onde o CharacterController inicia em suspensão.
        /// </summary>
        private void SnapToGround()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f))
            {
                _characterController.enabled = false;
                // Ajusta a posição para o ponto de colisão + metade da altura do CharacterController 
                // Considerar se o pivot do modelo está nos pés (Y=0 local) ou no centro.
                transform.position = hit.point + Vector3.up * (_characterController.skinWidth + 0.1f);
                _characterController.enabled = true;
                Debug.Log($"[NETWORK] Player aterrado via Raycast em {hit.point}");
            }
        }

        private void Update()
        {
            if (!_canMove || (_characterBase != null && _characterBase.CurrentHealth <= 0))
            {
                // Se o personagem estiver morto ou impossibilitado, trava inputs
                return;
            }

            HandleMovement();
            HandleMouseLook();
            HandleNetworkSync();
        }

        /// <summary>
        /// Envia atualizações de posição para o servidor respeitando o tick rate.
        /// </summary>
        private void HandleNetworkSync()
        {
            if (Time.time < _nextNetworkTick) return;

            // Só envia se houver mudança significativa na posição ou rotação
            bool hasMoved = Vector3.Distance(transform.position, _lastSentPosition) > 0.05f;
            bool hasRotated = Mathf.Abs(transform.eulerAngles.y - _lastSentRotation) > 0.5f;

            if (hasMoved || hasRotated)
            {
                EmitMovement();
                _nextNetworkTick = Time.time + (1f / _networkTickRate);
                _lastSentPosition = transform.position;
                _lastSentRotation = transform.eulerAngles.y;
            }
        }

        private void EmitMovement()
        {
            // Payload otimizado conforme COMMUNICATION.md
            var payload = new
            {
                p = new float[] { transform.position.x, transform.position.y, transform.position.z },
                r = transform.eulerAngles.y
            };

            // SocketManager deve ser o Singleton central
            // Usamos System.Action ou chamada direta se o SocketManager expuser.
            // Conforme client-design-pattern.md: SocketManager.Instance.Emit
            SocketManager.Instance.Emit(NetworkEvents.PLAYER_MOVE, payload);
        }

        /// <summary>
        /// Processa WASD e aplicação de gravidade.
        /// </summary>
        private void HandleMovement()
        {
            // 1. Captura Input do Teclado (Input System Direct API)
            Vector2 moveInput = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
                if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
                if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
                if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            }

            // 2. Transforma o input em direção relativa ao jogador (Forward/Right)
            // Garantimos que a direção seja puramente horizontal para evitar flutuação por inclinação
            Vector3 direction = transform.forward * moveInput.y + transform.right * moveInput.x;
            direction.y = 0;
            direction = direction.normalized;
            
            // 3. Processamento de Gravidade
            // Resetamos a velocidade vertical se estiver no chão
            if (_characterController.isGrounded && _velocity.y < 0) {
                _velocity.y = -2f; 
            }
            
            // Aplica aceleração da gravidade
            _velocity.y += _gravity * Time.deltaTime;

            // 4. Aplica Movimento
            if (!_onlyMoveViaNetwork) {
                // Movimento Livre Local (Single Player / Client Side Prediction Off)
                _characterController.Move(direction * _walkSpeed * Time.deltaTime);
                _characterController.Move(_velocity * Time.deltaTime);
            } else {
                // Modo Autoridade de Rede: 
                // Usamos o Move() para "predizer" a posição, resolver colisões e atualizar o isGrounded localmente.
                // O HandleNetworkSync enviará esta posição resultante para o servidor, 
                // que a validará e enviará de volta o "Snap" absoluto no evento PLAYER_MOVE.
                _characterController.Move((direction * _walkSpeed + _velocity) * Time.deltaTime);
            }
        }

        /// <summary>
        /// Processa a rotação do corpo (Yaw) e da câmera (Pitch).
        /// </summary>
        private void HandleMouseLook()
        {
            if (Mouse.current == null) return;

            // Delta do mouse
            Vector2 mouseDelta = Mouse.current.delta.ReadValue() * _lookSensitivity;

            // Rotação Vertical (Pitch - Câmera)
            _verticalRotation -= mouseDelta.y;
            _verticalRotation = Mathf.Clamp(_verticalRotation, _lowerLookLimit, _upperLookLimit);
            _playerCamera.localRotation = Quaternion.Euler(_verticalRotation, 0, 0);

            // Rotação Horizontal (Yaw - Corpo)
            transform.Rotate(Vector3.up * mouseDelta.x);
        }

        /// <summary>
        /// Permite habilitar/desabilitar controle externamente.
        /// </summary>
        public void ToggleControl(bool state)
        {
            _canMove = state;
            if (!state)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
