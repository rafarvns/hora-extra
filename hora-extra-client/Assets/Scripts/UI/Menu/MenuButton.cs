using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu
{
    /// <summary>
    /// Botão do menu: <see cref="Selectable"/> com destaque animado (barra âmbar à esquerda,
    /// leve escala), estado indisponível com motivo ("Servidor offline") e navegação por
    /// teclado/gamepad. Passar o mouse por cima também seleciona — assim mouse e teclado
    /// nunca mostram dois botões destacados ao mesmo tempo.
    ///
    /// Criado por <see cref="MenuFactory.Button"/>; não monte à mão.
    /// </summary>
    public class MenuButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        private const float ANIM_SPEED = 9f;
        private const float BAR_WIDTH = 6f;

        private HudTheme _theme;
        private Image _bg;
        private RectTransform _bar;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _sub;
        private bool _primary;
        private Action _onClick;

        private bool _highlighted;
        private bool _pressed;
        private bool _available = true;
        private string _defaultSub;
        private float _hover;

        public TextMeshProUGUI LabelText => _label;

        internal void Setup(HudTheme theme, Image bg, RectTransform bar, TextMeshProUGUI label, TextMeshProUGUI sub,
            bool primary, Action onClick)
        {
            _theme = theme;
            _bg = bg;
            _bar = bar;
            _label = label;
            _sub = sub;
            _primary = primary;
            _onClick = onClick;
            _defaultSub = sub != null ? sub.text : null;

            transition = Transition.None;
            targetGraphic = bg;
            Apply(instant: true);
        }

        public void SetLabel(string text)
        {
            if (_label != null) _label.text = text;
        }

        /// <summary>Texto pequeno à direita (ex: "como convidado"). Vazio esconde.</summary>
        public void SetSubLabel(string text)
        {
            _defaultSub = text;
            if (_sub != null && _available) _sub.text = text ?? string.Empty;
        }

        /// <summary>
        /// Disponível ou não. Indisponível fica apagado, não aceita clique e mostra
        /// <paramref name="reason"/> no lugar do sub-rótulo.
        /// </summary>
        public void SetAvailable(bool available, string reason = null)
        {
            _available = available;
            interactable = available;
            if (_sub != null) _sub.text = available ? (_defaultSub ?? string.Empty) : (reason ?? string.Empty);
            Apply(instant: false);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            _highlighted = state == SelectionState.Highlighted || state == SelectionState.Selected || state == SelectionState.Pressed;
            _pressed = state == SelectionState.Pressed;
            if (instant) Apply(instant: true);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Press();
        }

        private void Press()
        {
            if (!IsActive() || !IsInteractable()) return;
            _onClick?.Invoke();
        }

        private void Update()
        {
            float alvo = _highlighted && IsInteractable() ? 1f : 0f;
            if (Mathf.Approximately(_hover, alvo)) return;
            _hover = Mathf.MoveTowards(_hover, alvo, ANIM_SPEED * Time.unscaledDeltaTime);
            Apply(instant: false);
        }

        private void Apply(bool instant)
        {
            if (_theme == null || _bg == null) return;
            if (instant) _hover = _highlighted && IsInteractable() ? 1f : 0f;

            bool ativo = IsInteractable();
            float k = _hover;

            Color baseBg = _primary ? _theme.Accent : HudTheme.WithAlpha(_theme.Panel, 0.55f);
            Color hoverBg = _primary ? Color.Lerp(_theme.Accent, Color.white, 0.18f) : _theme.PanelRaised;
            Color bg = Color.Lerp(baseBg, hoverBg, k);
            if (!ativo) bg = HudTheme.WithAlpha(baseBg, baseBg.a * 0.35f);
            _bg.color = bg;

            if (_label != null)
            {
                Color texto = _primary ? _theme.Panel : _theme.Text;
                _label.color = ativo ? texto : HudTheme.WithAlpha(_theme.TextMuted, 0.6f);
            }
            if (_sub != null)
                _sub.color = !ativo ? _theme.Danger : _primary ? HudTheme.WithAlpha(_theme.Panel, 0.75f) : _theme.TextMuted;

            if (_bar != null)
            {
                _bar.sizeDelta = new Vector2(BAR_WIDTH * k, _bar.sizeDelta.y);
                _bar.gameObject.SetActive(k > 0.01f);
            }

            float escala = 1f + 0.025f * k;
            if (_pressed) escala *= 0.98f;
            transform.localScale = Vector3.one * escala;
        }
    }
}
