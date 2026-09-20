using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// A "mão" do jogador. Componente do prefab Player.
    ///
    /// Carrega uma PILHA de itens do MESMO tipo (até <see cref="_maxStack"/>), para que
    /// recolher 4 papéis não vire 4 viagens até o recipiente. Duas restrições mantêm a
    /// entrega sem ambiguidade:
    ///   - não mistura categorias: com papel na mão, uma caneta é recusada;
    ///   - ferramenta (IsTool) ocupa a mão sozinha — rodo e vassoura não empilham.
    ///
    /// Três formas de um item sair da mão, com semânticas distintas:
    ///   - <see cref="Consume"/>      — entrega confirmada pelo servidor.
    ///   - <see cref="DropAll"/>      — o jogador largou tudo, volta ao mundo.
    ///   - <see cref="ReturnToHand"/> — o servidor recusou: desfaz o efeito otimista.
    /// </summary>
    public class PlayerCarrier : MonoBehaviour
    {
        [Header("Carregamento")]
        [Tooltip("Ponto de encaixe visual do item carregado. Se vazio, usa o transform deste objeto.")]
        [SerializeField] private Transform _handAnchor;

        [Tooltip("Quantos itens do mesmo tipo cabem na mão.")]
        [SerializeField] private int _maxStack = 6;

        [Tooltip("Deslocamento vertical entre itens empilhados, em metros de mundo.")]
        [SerializeField] private float _stackOffset = 0.035f;

        [Header("Largar [Q]")]
        [Tooltip("A que distância à frente do jogador os itens são largados.")]
        [SerializeField] private float _dropForward = 0.8f;

        [Tooltip("Altura do item largado, relativa à origem do jogador. Negativo = para baixo, " +
                 "porque a origem do Player fica na altura dos olhos.")]
        [SerializeField] private float _dropHeight = -0.5f;

        // === Estado ===
        private readonly List<CarryableItem> _carried = new List<CarryableItem>();

        /// <summary>Item no topo da pilha, ou null. Mantido para quem só precisa de "o que está na mão".</summary>
        public CarryableItem Carried => _carried.Count > 0 ? _carried[_carried.Count - 1] : null;

        /// <summary>True quando há pelo menos um item na mão.</summary>
        public bool IsCarrying => _carried.Count > 0;

        /// <summary>Quantos itens estão na mão.</summary>
        public int CarriedCount => _carried.Count;

        /// <summary>Categoria da pilha atual, ou null se a mão está vazia.</summary>
        public string CarriedKind => _carried.Count > 0 ? _carried[0].Kind : null;

        /// <summary>True quando a pilha atingiu o limite.</summary>
        public bool IsFull => _carried.Count >= _maxStack;

        /// <summary>Snapshot da pilha, do primeiro ao último pego.</summary>
        public IReadOnlyList<CarryableItem> CarriedItems => _carried;

        /// <summary>
        /// Disparado sempre que o conteúdo da mão muda (pegou, largou, entregou).
        /// Argumento: o item no topo da pilha, ou null se a mão ficou vazia.
        /// </summary>
        public static event Action<CarryableItem> OnCarryChanged;

        private void Awake()
        {
            if (_handAnchor == null)
            {
                _handAnchor = transform;
                Debug.LogWarning($"[GAMEPLAY] PlayerCarrier em '{name}' sem _handAnchor — " +
                                 "o item vai encaixar na raiz do player. Crie um filho como ponto de mão.");
            }
        }

        /// <summary>
        /// Diz se <paramref name="item"/> pode entrar na mão agora. Usado pelo CarryableItem
        /// para decidir se mostra o prompt — evita prometer uma ação que seria recusada.
        /// </summary>
        public bool CanAccept(CarryableItem item)
        {
            if (item == null || IsFull) return false;

            // Já está na mão. Sem esta guarda o item continua "pegável" enquanto carregado
            // (o trigger dele segue sobreposto ao jogador, porque agora ele ANDA junto com
            // o jogador), e cada [E] o adicionaria de novo à pilha — o mesmo papel contado
            // várias vezes.
            if (_carried.Contains(item)) return false;

            if (_carried.Count == 0) return true;

            // Ferramenta não empilha, nem com ela mesma nem sobre outra coisa.
            if (item.IsTool || _carried[0].IsTool) return false;

            return _carried[0].Kind == item.Kind;
        }

        /// <summary>
        /// Tenta empilhar <paramref name="item"/> na mão. Retorna true se pegou.
        /// </summary>
        public bool TryPickup(CarryableItem item)
        {
            if (item == null) return false;

            if (!CanAccept(item))
            {
                Debug.Log($"[GAMEPLAY] PlayerCarrier — pickup de '{item.ItemId}' recusado " +
                          $"(mão: {_carried.Count}/{_maxStack}, tipo '{CarriedKind ?? "-"}').");
                return false;
            }

            _carried.Add(item);
            AttachToHand(item, _carried.Count - 1);

            Debug.Log($"[GAMEPLAY] PlayerCarrier — item '{item.ItemId}' pego ({_carried.Count}/{_maxStack}).");
            OnCarryChanged?.Invoke(Carried);
            return true;
        }

        /// <summary>
        /// Consome UM item da pilha após o servidor CONFIRMAR a entrega dele.
        /// Ferramenta (IsTool) continua na mão — é o que permite limpar várias sujeiras
        /// com o mesmo rodo sem pegá-lo de novo.
        /// </summary>
        public void Consume(CarryableItem item)
        {
            if (item == null || !_carried.Contains(item)) return;

            if (item.IsTool)
            {
                Debug.Log($"[GAMEPLAY] PlayerCarrier — '{item.ItemId}' é ferramenta, permanece na mão.");
                return;
            }

            _carried.Remove(item);
            item.transform.SetParent(null, true);
            item.gameObject.SetActive(false);
            Restack();

            Debug.Log($"[GAMEPLAY] PlayerCarrier — item '{item.ItemId}' consumido na entrega " +
                      $"(restam {_carried.Count}).");
            OnCarryChanged?.Invoke(Carried);
        }

        private void Update()
        {
            // Válvula de escape: item que a task não aceita mais (já concluída) ficaria
            // preso na mão para sempre sem uma forma explícita de largar.
            if (IsCarrying && InteractionInput.DropPressed())
                DropAll();
        }

        /// <summary>
        /// Solta a pilha inteira no chão, à frente do jogador.
        ///
        /// Os itens são posicionados no PÉ do jogador, não na altura da mão: largar na
        /// posição da mão deixaria tudo flutuando no ar, já que papel e afins não têm
        /// Rigidbody para cair.
        /// </summary>
        public void DropAll()
        {
            if (!IsCarrying) return;

            var dropped = new List<CarryableItem>(_carried);
            _carried.Clear();

            Vector3 origem = transform.position + transform.forward * _dropForward;
            for (int i = 0; i < dropped.Count; i++)
            {
                var item = dropped[i];
                item.transform.SetParent(null, true);
                item.transform.position = origem + new Vector3(0f, _dropHeight, 0f)
                                          + transform.right * (i * 0.15f);
                item.transform.rotation = Quaternion.identity;
                // O encaixe na mão mexeu na escala para normalizar o tamanho; no chão o
                // item tem que voltar ao que era, senão vira um papel gigante ou minúsculo.
                item.transform.localScale = item.OriginalScale;
                RestoreCollider(item);
            }

            Debug.Log($"[GAMEPLAY] PlayerCarrier — {dropped.Count} item(ns) largado(s) no chão.");
            OnCarryChanged?.Invoke(null);
        }

        /// <summary>
        /// Devolve para a mão um item entregue de forma otimista que o servidor recusou.
        ///
        /// Na prática costuma ser no-op: o TaskDepositPoint não tira o item da mão antes da
        /// confirmação, então ele ainda está na pilha. Existe para o caso de a ordem inverter.
        /// </summary>
        public void ReturnToHand(CarryableItem item)
        {
            if (item == null) return;
            if (_carried.Contains(item)) return; // nunca saiu da mão — nada a desfazer

            if (!CanAccept(item))
            {
                Debug.LogWarning($"[GAMEPLAY] PlayerCarrier — não dá para devolver '{item.ItemId}' " +
                                 $"(mão: {_carried.Count}/{_maxStack}). Item devolvido ao mundo.");
                item.gameObject.SetActive(true);
                item.transform.localScale = item.OriginalScale;
                RestoreCollider(item);
                return;
            }

            item.gameObject.SetActive(true);
            _carried.Add(item);
            AttachToHand(item, _carried.Count - 1);

            Debug.Log($"[GAMEPLAY] PlayerCarrier — item '{item.ItemId}' devolvido à mão após recusa do servidor.");
            OnCarryChanged?.Invoke(Carried);
        }

        /// <summary>Reparenta no ponto de mão, empilha no índice dado e desliga física/colisão.</summary>
        private void AttachToHand(CarryableItem item, int index)
        {
            item.transform.SetParent(_handAnchor, false);
            item.transform.localRotation = Quaternion.identity;
            item.transform.localPosition = new Vector3(0f, _stackOffset * index, 0f);

            item.SetCollidersEnabled(false);

            // Um Rigidbody não-kinemático reparentado mantém a velocidade e sai andando
            // dentro da mão do jogador.
            var body = item.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        /// <summary>Recalcula o empilhamento depois que um item sai do meio da pilha.</summary>
        private void Restack()
        {
            for (int i = 0; i < _carried.Count; i++)
                _carried[i].transform.localPosition = new Vector3(0f, _stackOffset * i, 0f);
        }

        private void RestoreCollider(CarryableItem item)
        {
            item.SetCollidersEnabled(true);
        }
    }
}
