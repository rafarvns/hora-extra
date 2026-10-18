using UnityEngine;

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Base dos widgets da HUD.
    ///
    /// O <see cref="HudRoot"/> cria o GameObject INATIVO, adiciona o widget, chama
    /// <see cref="Init"/> (que monta a UI) e só então ativa. Assim o OnEnable de cada widget —
    /// onde ele assina os eventos — sempre encontra a UI pronta.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class HudWidget : MonoBehaviour
    {
        protected HudTheme Theme { get; private set; }
        protected HudRoot Hud { get; private set; }
        protected RectTransform Root { get; private set; }

        public void Init(HudRoot hud, HudTheme theme)
        {
            Hud = hud;
            Theme = theme;
            Root = (RectTransform)transform;
            Build();
        }

        /// <summary>Monta a hierarquia de UI do widget. Chamado uma vez, antes do OnEnable.</summary>
        protected abstract void Build();

        /// <summary>Id do jogador local, ou null antes do CONN_SUCCESS.</summary>
        protected static string LocalPlayerId => SocketManager.Instance != null ? SocketManager.Instance.LocalPlayerId : null;

        protected static bool IsLocalPlayer(string playerId)
        {
            var local = LocalPlayerId;
            return !string.IsNullOrEmpty(playerId) && playerId == local;
        }
    }
}
