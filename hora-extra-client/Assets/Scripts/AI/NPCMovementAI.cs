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
        private float _timer;

        private void Awake()
        {
            _networkEntity = GetComponent<NetworkEntity>();
        }

        private void Update()
        {
            // Apenas o Master do NPC processa o Timer e envia solicitações de movimento.
            // Os outros clientes apenas recebem o broadcast via NetworkEntity.
            if (!_isMovementEnabled || _networkEntity == null || !_networkEntity.IsMaster) return;

            _timer += Time.deltaTime;
            if (_timer >= _moveInterval)
            {
                _timer = 0;
                GenerateRandomStep();
            }
        }

        /// <summary>
        /// Sorteia um novo passo e envia a solicitação ao backend.
        /// </summary>
        private void GenerateRandomStep()
        {
            // Escolhe um deslocamento aleatório
            float randomX = Random.Range(-3f, 3f);
            float randomZ = Random.Range(-3f, 3f);
            float randomRot = Random.Range(0f, 360f);

            Vector3 targetPos = transform.position + new Vector3(randomX, 0, randomZ);

            // IMPORTANTE: O NPC não se move imediatamente. 
            // Ele aguarda a resposta do backend via NetworkEntity.
            _networkEntity.RequestMove(targetPos, randomRot);
            
            Debug.Log($"[AI] NPC enviando solicitação para mover para {targetPos}");
        }
    }
}
