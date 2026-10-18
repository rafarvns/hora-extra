using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu
{
    /// <summary>
    /// Painel lateral (Como jogar, Configurações, Créditos): desliza da direita sobre um véu
    /// escuro e fecha por "Voltar" ou [Esc]. As subclasses só preenchem <see cref="Content"/>
    /// em <see cref="BuildContent"/>.
    /// </summary>
    public abstract class MenuPanel : MenuWidget
    {
        private const float WIDTH = 780f;
        private const float SLIDE_SECONDS = 0.22f;

        private CanvasGroup _group;
        private RectTransform _sheet;
        private Coroutine _anim;

        protected RectTransform Content { get; private set; }
        protected abstract string Title { get; }

        public bool IsOpen { get; private set; }

        /// <summary>Primeiro controle a receber seleção ao abrir (teclado/gamepad).</summary>
        public GameObject FirstSelected { get; protected set; }

        protected override void Build()
        {
            HudFactory.Stretch(Root);
            _group = Root.gameObject.AddComponent<CanvasGroup>();

            // Véu: escurece o menu e fecha o painel ao clicar fora.
            var veil = Root.gameObject.AddComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0.55f);
            veil.raycastTarget = true;
            var closer = Root.gameObject.AddComponent<Button>();
            closer.transition = Selectable.Transition.None;
            closer.navigation = new Navigation { mode = Navigation.Mode.None };
            closer.onClick.AddListener(() => Menu.ClosePanel(this));

            var sheetImg = HudFactory.Panel(Root, "Sheet", Theme.Panel, Theme);
            // O clique dentro do painel não pode subir até o Button do véu (que fecharia o painel).
            sheetImg.raycastTarget = true;
            sheetImg.gameObject.AddComponent<ClickBlocker>();
            sheetImg.color = HudTheme.WithAlpha(Theme.Panel, 0.97f);
            _sheet = sheetImg.rectTransform;
            _sheet.anchorMin = new Vector2(1f, 0f);
            _sheet.anchorMax = new Vector2(1f, 1f);
            _sheet.pivot = new Vector2(1f, 0.5f);
            _sheet.sizeDelta = new Vector2(WIDTH, -48f);
            _sheet.anchoredPosition = new Vector2(-24f, 0f);

            var column = HudFactory.Column(_sheet.gameObject, 14f, new RectOffset(48, 48, 40, 40));
            column.childForceExpandHeight = false;

            var header = HudFactory.Rect("Header", _sheet);
            HudFactory.Row(header.gameObject, 16f, TextAnchor.MiddleLeft);
            HudFactory.Fixed(header.gameObject, -1f, 56f);
            var title = HudFactory.Label(header, "Title", Theme, Theme.TitleSize * 1.35f, Theme.Text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.text = Title;
            HudFactory.Fixed(title.gameObject, -1f, 56f).flexibleWidth = 1f;
            var back = MenuFactory.Button(header, Theme, "Voltar", () => Menu.ClosePanel(this), height: 46f, subLabel: "Esc");
            HudFactory.Fixed(back.gameObject, 190f, 46f);

            var divider = HudFactory.Panel(_sheet, "Divider", Theme.Track, Theme);
            HudFactory.Fixed(divider.gameObject, -1f, 2f);

            Content = HudFactory.Rect("Content", _sheet);
            var content = HudFactory.Column(Content.gameObject, 10f);
            content.childForceExpandHeight = false;
            HudFactory.Fixed(Content.gameObject, -1f, -1f).flexibleHeight = 1f;

            BuildContent();
            if (FirstSelected == null) FirstSelected = back.gameObject;

            SetVisible(false, instant: true);
        }

        protected abstract void BuildContent();

        /// <summary>Chamado pelo MainMenuRoot. Use Menu.OpenPanel/ClosePanel, não isto direto.</summary>
        internal void SetVisible(bool visible, bool instant = false)
        {
            IsOpen = visible;
            if (visible) OnOpened();

            if (_anim != null) StopCoroutine(_anim);
            if (instant || !isActiveAndEnabled)
            {
                ApplyProgress(visible ? 1f : 0f);
                return;
            }
            _anim = StartCoroutine(Animate(visible));
        }

        /// <summary>Atualiza o conteúdo ao abrir (ex: valores salvos). Padrão: nada.</summary>
        protected virtual void OnOpened() { }

        private IEnumerator Animate(bool opening)
        {
            float from = opening ? 0f : 1f, to = opening ? 1f : 0f;
            for (float t = 0f; t < SLIDE_SECONDS; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(from, to, t / SLIDE_SECONDS);
                ApplyProgress(k);
                yield return null;
            }
            ApplyProgress(to);
            _anim = null;

            if (opening && FirstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(FirstSelected);
        }

        private void ApplyProgress(float k)
        {
            _group.alpha = k;
            _group.interactable = k > 0.99f;
            _group.blocksRaycasts = k > 0.01f;
            _sheet.anchoredPosition = new Vector2(Mathf.Lerp(WIDTH, -24f, k), 0f);
        }
    }

    /// <summary>Consome o clique para ele não subir até um handler ancestral.</summary>
    public class ClickBlocker : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) { }
    }
}
