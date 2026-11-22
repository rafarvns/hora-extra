using TMPro;
using UnityEngine;
using HoraExtra.UI;

namespace HoraExtra.Characters
{
    /// <summary>
    /// Representa um jogador remoto (o colega da sala).
    /// Aplica interpolação suave às posições e rotações recebidas via rede e mostra o
    /// nome dele em cima da cabeça.
    /// </summary>
    public class NetworkPlayer : MonoBehaviour
    {
        [Header("Configurações de Interpolação")]
        [SerializeField, Tooltip("Velocidade de suavização do movimento.")]
        private float _moveLerpSpeed = 10f;

        [SerializeField, Tooltip("Velocidade de suavização da rotação.")]
        private float _rotationLerpSpeed = 10f;

        [Header("Nome")]
        [SerializeField] private float _alturaNome = 1.35f;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private TextMeshPro _nome;

        public string PlayerId { get; private set; }
        public string PlayerName { get; private set; }

        /// <summary>Andar em que o colega está (1/2/3).</summary>
        public int Floor { get; set; } = 1;

        /// <summary>Já recebemos ao menos uma posição real dele.</summary>
        public bool HasPosition { get; private set; }

        public void Initialize(string id, string playerName)
        {
            PlayerId = id;
            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            CriarNome();
            SetName(playerName);
        }

        public void SetName(string playerName)
        {
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Colega" : playerName;
            gameObject.name = $"Player_{PlayerName} ({PlayerId})";
            if (_nome != null) _nome.text = PlayerName;
        }

        /// <summary>
        /// Atualiza o alvo de interpolação com dados vindos do servidor. Na primeira posição
        /// (ou quando <paramref name="snap"/>) teleporta, para não "voar" desde a origem.
        /// </summary>
        public void UpdateState(float[] position, float rotation, bool snap = false)
        {
            _targetPosition = new Vector3(position[0], position[1], position[2]);
            _targetRotation = Quaternion.Euler(0, rotation, 0);
            if (snap || !HasPosition) Snap();
            HasPosition = true;
        }

        /// <summary>Vai direto para o alvo (ao reaparecer depois de trocar de andar).</summary>
        public void Snap()
        {
            transform.SetPositionAndRotation(_targetPosition, _targetRotation);
        }

        private void Update()
        {
            // Interpolação Suave (Evita Stuttering)
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _moveLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, Time.deltaTime * _rotationLerpSpeed);
        }

        private void CriarNome()
        {
            if (_nome != null) return;

            var go = new GameObject("NameTag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, _alturaNome, 0f);

            _nome = go.AddComponent<TextMeshPro>();
            _nome.fontSize = 3f;
            _nome.alignment = TextAlignmentOptions.Center;
            _nome.color = Color.white;
            _nome.outlineWidth = 0.2f;
            _nome.outlineColor = new Color32(0, 0, 0, 200);
            _nome.enableWordWrapping = false;
            _nome.rectTransform.sizeDelta = new Vector2(4f, 1f);

            go.AddComponent<Billboard>();
        }
    }
}
