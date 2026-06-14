using UnityEngine;

namespace HoraExtra.UI
{
    /// <summary>
    /// Faz o GameObject (tipicamente um canvas World Space) sempre encarar a câmera principal,
    /// garantindo que textos/UI no mundo 3D fiquem legíveis de qualquer ângulo.
    ///
    /// Uso: anexe a um canvas World Space (ex: prompt "Pressione E" da cafeteira).
    /// A câmera é resolvida via Camera.main (tag MainCamera) — no projeto, a câmera é
    /// filha do Player, então fica disponível assim que o jogador existe na cena.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Header("Billboard")]
        [Tooltip("Se verdadeiro, ignora a inclinação vertical da câmera e mantém o texto na vertical (recomendado para prompts).")]
        [SerializeField] private bool _lockVertical = false;

        private Transform _cameraTransform;

        private void Awake()
        {
            CacheCamera();
        }

        private void CacheCamera()
        {
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void LateUpdate()
        {
            // A câmera pode não existir ainda no Awake (player spawna depois) — tenta de novo.
            if (_cameraTransform == null)
            {
                CacheCamera();
                if (_cameraTransform == null) return;
            }

            // Alinha o forward do objeto com o forward da câmera → o plano do canvas
            // fica paralelo à tela, sempre legível.
            Vector3 forward = _cameraTransform.forward;
            if (_lockVertical)
                forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}
