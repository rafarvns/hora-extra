using UnityEngine;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Indicador flutuante que aparece sobre um <see cref="TaskDepositPoint"/> quando o
    /// jogador está carregando algo que ele aceita.
    ///
    /// Some assim que a mão esvazia ou a tarefa conclui, então nunca aponta para um lugar
    /// onde não há nada a fazer. É deliberadamente visível de longe — de perto já existe o
    /// prompt de [E]; o problema que isto resolve é "peguei o item e não sei onde entregar".
    ///
    /// O visual é construído em runtime (um losango girando) para a cena não depender de
    /// nenhum asset. Atribua <c>_customVisual</c> para usar arte própria no lugar.
    /// </summary>
    [RequireComponent(typeof(TaskDepositPoint))]
    public class DepositHighlight : MonoBehaviour
    {
        [Header("Posição")]
        [Tooltip("Altura do indicador acima da origem do destino, em metros.")]
        [SerializeField] private float _altura = 0.7f;

        [Tooltip("Tamanho do losango, em metros.")]
        [SerializeField] private float _tamanho = 0.18f;

        [Header("Animação")]
        [SerializeField] private float _amplitudeSobeDesce = 0.1f;
        [SerializeField] private float _velocidadeSobeDesce = 2f;
        [SerializeField] private float _velocidadeGiro = 90f;

        [Header("Aparência")]
        [SerializeField] private Color _cor = new Color(1f, 0.82f, 0.15f);

        [Tooltip("Visual proprio. Vazio = o componente cria um losango simples.")]
        [SerializeField] private GameObject _customVisual;

        private TaskDepositPoint _destino;
        private GameObject _visual;
        private Vector3 _posicaoBase;

        /// <summary>Categoria no topo da pilha do jogador, ou null se a mão está vazia.</summary>
        private string _kindNaMao;

        private void Awake()
        {
            _destino = GetComponent<TaskDepositPoint>();
            _visual = _customVisual != null ? _customVisual : CriarLosango();
            _posicaoBase = transform.position + Vector3.up * _altura;
            _visual.SetActive(false);
        }

        private void OnEnable()
        {
            PlayerCarrier.OnCarryChanged += AoMudarMao;
        }

        private void OnDisable()
        {
            PlayerCarrier.OnCarryChanged -= AoMudarMao;
            if (_visual != null) _visual.SetActive(false);
        }

        private void AoMudarMao(CarryableItem item)
        {
            _kindNaMao = item != null ? item.Kind : null;
        }

        private void Update()
        {
            if (_visual == null) return;

            // Reavalia a cada frame em vez de só no evento de mão: a tarefa pode concluir
            // (ou ser liberada pela fila) sem que o conteúdo da mão mude.
            bool mostrar = _destino.AguardandoItem(_kindNaMao);

            if (_visual.activeSelf != mostrar)
                _visual.SetActive(mostrar);

            if (!mostrar) return;

            float sobe = Mathf.Sin(Time.time * _velocidadeSobeDesce) * _amplitudeSobeDesce;
            _visual.transform.position = _posicaoBase + Vector3.up * sobe;
            _visual.transform.Rotate(Vector3.up, _velocidadeGiro * Time.deltaTime, Space.World);
        }

        /// <summary>Cubo girado 45° nos dois eixos — lê como um losango/diamante apontando para baixo.</summary>
        private GameObject CriarLosango()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DepositHighlight (auto)";
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.localScale = Vector3.one * _tamanho;
            go.transform.rotation = Quaternion.Euler(45f, 0f, 45f);

            // O primitivo vem com collider: sem remover, ele viraria um obstáculo invisível
            // no meio da sala e atrapalharia o proprio trigger de entrega.
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;

                // Built-in usa _Color, URP usa _BaseColor. Setar os dois evita depender de
                // qual pipeline o projeto estiver usando.
                var mat = rend.material;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _cor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", _cor);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", _cor * 1.5f);
                }
            }

            return go;
        }
    }
}
