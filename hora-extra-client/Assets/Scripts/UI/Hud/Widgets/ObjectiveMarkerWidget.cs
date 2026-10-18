using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HoraExtra.Characters;
using HoraExtra.Interactions;
using HoraExtra.Network.Models;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Marcador do próximo objetivo: um ponto com a distância ("12 m") sobre o alvo; quando o
    /// alvo sai da tela, vira uma seta presa na borda apontando a direção.
    ///
    /// O alvo é derivado do estado da cena, sem dado de rede novo:
    ///   - limpeza (DirtSpot do tipo da tarefa): sem a ferramenta → a ferramenta mais próxima;
    ///     com ela → a mancha mais próxima;
    ///   - entrega (TaskDepositPoint do tipo da tarefa): com o item certo na mão → o destino;
    ///     senão → o item daquela categoria mais próximo que ainda está no mundo;
    ///   - café: a cafeteira.
    /// Perto do alvo o marcador some — ali o prompt de [E] já orienta.
    /// </summary>
    public class ObjectiveMarkerWidget : HudWidget
    {
        private const float RETARGET_SECONDS = 0.25f;
        private const float RESCAN_SECONDS = 2f;
        private const float HIDE_DISTANCE = 2.2f;
        private const float EDGE_MARGIN = 70f;
        private const float HEIGHT_OFFSET = 0.6f;

        private CanvasGroup _group;
        private RectTransform _marker;
        private RectTransform _arrow;
        private TextMeshProUGUI _distance;
        private RectTransform _canvasRect;

        private Camera _camera;
        private PlayerCarrier _carrier;
        private Transform _target;
        private float _nextRetargetAt;
        private float _nextRescanAt;

        private TaskDepositPoint[] _deposits = new TaskDepositPoint[0];
        private DirtSpot[] _dirt = new DirtSpot[0];
        private CarryableItem[] _items = new CarryableItem[0];
        private CoffeeMakerInteraction[] _coffee = new CoffeeMakerInteraction[0];

        protected override void Build()
        {
            HudFactory.Stretch(Root);
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            _marker = HudFactory.Rect("Marker", Root);
            HudFactory.Place(_marker, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));

            var halo = HudFactory.Dot(_marker, "Halo", HudTheme.WithAlpha(Theme.Accent, 0.25f), 40f);
            HudFactory.Stretch(halo.rectTransform);
            var core = HudFactory.Dot(_marker, "Core", Theme.Accent, 16f);
            HudFactory.Place(core.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));

            // Seta: quadrado girado 45°, deslocado para fora do ponto; o pivô gira em volta do centro.
            _arrow = HudFactory.Rect("ArrowPivot", _marker);
            HudFactory.Place(_arrow, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var tip = HudFactory.Panel(_arrow, "Tip", Theme.Accent, Theme);
            tip.pixelsPerUnitMultiplier = 12f;
            HudFactory.Place(tip.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(16f, 16f));
            tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            _distance = HudFactory.Label(_marker, "Distance", Theme, Theme.SmallSize, Theme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            // Logo abaixo do ponto: âncora na base do marcador, pivô no topo do texto.
            HudFactory.Place(_distance.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(120f, 24f));
            _distance.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        private void Update()
        {
            if (_canvasRect == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) _canvasRect = (RectTransform)canvas.rootCanvas.transform;
            }
            if (_camera == null) _camera = Camera.main;
            if (_carrier == null) _carrier = FindFirstObjectByType<PlayerCarrier>();

            if (Time.unscaledTime >= _nextRescanAt)
            {
                _nextRescanAt = Time.unscaledTime + RESCAN_SECONDS;
                Rescan();
            }
            if (Time.unscaledTime >= _nextRetargetAt)
            {
                _nextRetargetAt = Time.unscaledTime + RETARGET_SECONDS;
                _target = PickTarget();
            }

            bool visivel = Position();
            _group.alpha = Mathf.MoveTowards(_group.alpha, visivel ? 1f : 0f, 6f * Time.unscaledDeltaTime);
        }

        /// <summary>Objetos de cena interagíveis (só os ativos — entregues/limpos somem sozinhos).</summary>
        private void Rescan()
        {
            _deposits = FindObjectsByType<TaskDepositPoint>(FindObjectsSortMode.None);
            _dirt = FindObjectsByType<DirtSpot>(FindObjectsSortMode.None);
            _items = FindObjectsByType<CarryableItem>(FindObjectsSortMode.None);
            _coffee = FindObjectsByType<CoffeeMakerInteraction>(FindObjectsSortMode.None);
        }

        private Transform PickTarget()
        {
            var bridge = TaskSystemBridge.Instance;
            if (bridge == null || _carrier == null) return null;

            AssignedTask task = TaskPresentation.FindActive(bridge.GetMyTasks());
            if (task == null) return null;

            Vector3 origem = _carrier.transform.position;
            string naMao = _carrier.CarriedKind;

            if (task.Type == TaskPresentation.TYPE_COFFEE_MAKER)
            {
                var cafeteira = Nearest(_coffee, c => c.isActiveAndEnabled, origem);
                return cafeteira != null ? cafeteira.transform : null;
            }

            // Limpeza: manchas do tipo da tarefa.
            DirtSpot mancha = Nearest(_dirt, d => d != null && d.isActiveAndEnabled && d.TaskType == task.Type, origem);
            if (mancha != null)
            {
                if (naMao == mancha.RequiredKind) return mancha.transform;
                return NearestLooseItem(mancha.RequiredKind, origem) ?? mancha.transform;
            }

            // Entrega: destino do tipo da tarefa e itens da categoria que ele aceita.
            TaskDepositPoint destino = Nearest(_deposits, d => d != null && d.isActiveAndEnabled && d.TaskType == task.Type, origem);
            if (destino == null) return null;

            if (naMao == destino.AcceptedKind) return destino.transform;
            return NearestLooseItem(destino.AcceptedKind, origem) ?? destino.transform;
        }

        /// <summary>Item da categoria ainda no mundo (não está na mão do jogador).</summary>
        private Transform NearestLooseItem(string kind, Vector3 origem)
        {
            var naMao = _carrier != null ? _carrier.CarriedItems : null;
            var item = Nearest(_items, i => i != null && i.isActiveAndEnabled && i.Kind == kind
                                            && (naMao == null || !Contains(naMao, i)), origem);
            return item != null ? item.transform : null;
        }

        private static bool Contains(IReadOnlyList<CarryableItem> list, CarryableItem item)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return true;
            return false;
        }

        private static T Nearest<T>(T[] candidates, System.Func<T, bool> filter, Vector3 origem) where T : Component
        {
            T melhor = null;
            float melhorDist = float.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || !filter(c)) continue;
                float d = (c.transform.position - origem).sqrMagnitude;
                if (d < melhorDist) { melhorDist = d; melhor = c; }
            }
            return melhor;
        }

        /// <summary>Posiciona o marcador. Devolve false quando não deve aparecer.</summary>
        private bool Position()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy || _camera == null || _canvasRect == null || _carrier == null)
                return false;

            Vector3 mundo = _target.position + Vector3.up * HEIGHT_OFFSET;
            float distancia = Vector3.Distance(_carrier.transform.position, _target.position);
            if (distancia < HIDE_DISTANCE) return false;

            Vector3 tela = _camera.WorldToScreenPoint(mundo);
            bool atras = tela.z < 0f;
            if (atras) tela = new Vector3(Screen.width - tela.x, Screen.height - tela.y, 0f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, tela, null, out Vector2 local);
            Rect area = _canvasRect.rect;
            Vector2 min = area.min + Vector2.one * EDGE_MARGIN;
            Vector2 max = area.max - Vector2.one * EDGE_MARGIN;

            bool foraDaTela = atras || local.x < min.x || local.x > max.x || local.y < min.y || local.y > max.y;
            if (foraDaTela)
            {
                // Projeta na borda ao longo da direção a partir do centro.
                Vector2 dir = (local - area.center);
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
                if (atras && Mathf.Abs(dir.y) < Mathf.Abs(dir.x) * 0.2f) dir.y = -Mathf.Abs(dir.x) * 0.2f;
                Vector2 half = (max - min) * 0.5f;
                float escala = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(dir.x), 0.001f), half.y / Mathf.Max(Mathf.Abs(dir.y), 0.001f));
                local = area.center + dir * escala;

                float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                _arrow.localRotation = Quaternion.Euler(0f, 0f, angulo);
            }
            _arrow.gameObject.SetActive(foraDaTela);

            // Root estica o canvas inteiro e o marcador é ancorado no centro dele, então a
            // posição local no canvas é a própria anchoredPosition.
            _marker.anchoredPosition = local;
            _distance.text = $"{Mathf.RoundToInt(distancia)} m";
            return true;
        }
    }
}
