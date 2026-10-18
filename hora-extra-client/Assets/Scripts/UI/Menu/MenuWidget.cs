using UnityEngine;
using HoraExtra.UI.Hud;

namespace HoraExtra.UI.Menu
{
    /// <summary>
    /// Base das seções do menu. Mesmo contrato do <see cref="HudWidget"/>: o
    /// <see cref="MainMenuRoot"/> cria o objeto inativo, chama <see cref="Init"/> (que monta a
    /// UI) e só então ativa — o OnEnable sempre encontra a UI pronta.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class MenuWidget : MonoBehaviour
    {
        protected HudTheme Theme { get; private set; }
        protected MainMenuRoot Menu { get; private set; }
        protected RectTransform Root { get; private set; }

        public void Init(MainMenuRoot menu, HudTheme theme)
        {
            Menu = menu;
            Theme = theme;
            Root = (RectTransform)transform;
            Build();
        }

        protected abstract void Build();
    }
}
