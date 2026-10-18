using TMPro;
using UnityEngine;
using HoraExtra.Interactions;

namespace HoraExtra.UI.Hud.Widgets
{
    /// <summary>
    /// Dicas de teclas sempre visíveis (baixo-esquerda): [E] Interagir · [Q] Soltar ·
    /// [Shift] Correr · [Tab] Tarefas. O [Q] só aparece com algo na mão — é a dica que o
    /// jogador mais precisava e antes só existia no log.
    /// </summary>
    public class KeyHintsWidget : HudWidget
    {
        private const float RETRY_SECONDS = 1f;

        private GameObject _dropHint;
        private PlayerCarrier _carrier;
        private float _nextFindAt;

        protected override void Build()
        {
            HudFactory.Place(Root, Vector2.zero, new Vector2(Theme.ScreenMargin, Theme.ScreenMargin), new Vector2(600f, 52f));

            var fundo = Root.gameObject.AddComponent<UnityEngine.UI.Image>();
            fundo.sprite = HudSprites.Rounded;
            fundo.type = UnityEngine.UI.Image.Type.Sliced;
            fundo.pixelsPerUnitMultiplier = Theme.CornerSharpness * 1.2f;
            fundo.color = HudTheme.WithAlpha(Theme.Panel, Theme.Panel.a * 0.7f);
            fundo.raycastTarget = false;

            HudFactory.Row(Root.gameObject, 22f, TextAnchor.MiddleLeft, new RectOffset(12, 18, 10, 10));
            HudFactory.FitContent(Root.gameObject, true, false);

            AddHint("E", "Interagir");
            _dropHint = AddHint("Q", "Soltar");
            AddHint("Shift", "Correr");
            AddHint("Tab", "Tarefas");
            _dropHint.SetActive(false);
        }

        private GameObject AddHint(string key, string action)
        {
            var hint = HudFactory.Rect($"Hint_{key}", Root);
            HudFactory.Row(hint.gameObject, 8f, TextAnchor.MiddleLeft);
            HudFactory.KeyCap(hint, key, Theme, 30f);

            var label = HudFactory.Label(hint, "Action", Theme, Theme.SmallSize, Theme.Text, TextAlignmentOptions.Left);
            label.text = action;
            return hint.gameObject;
        }

        private void Update()
        {
            if (_carrier == null)
            {
                if (Time.unscaledTime < _nextFindAt) return;
                _nextFindAt = Time.unscaledTime + RETRY_SECONDS;
                _carrier = FindFirstObjectByType<PlayerCarrier>();
                if (_carrier == null) return;
            }

            bool carregando = _carrier.IsCarrying;
            if (_dropHint.activeSelf != carregando)
                _dropHint.SetActive(carregando);
        }
    }
}
