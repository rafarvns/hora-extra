using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HoraExtra.UI.Hud
{
    /// <summary>
    /// Teclas da HUD, no mesmo molde de <see cref="HoraExtra.Interactions.InteractionInput"/>:
    /// funciona com o Input System novo (o que o projeto usa) e com o legado.
    /// </summary>
    public static class HudInput
    {
        /// <summary>[Tab] segurado — expande a lista de tarefas.</summary>
        public static bool TaskListHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tabKey.isPressed)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.Tab))
                return true;
#endif
            return false;
        }

        /// <summary>[F3] pressionado neste frame — liga/desliga o console de debug.</summary>
        public static bool DebugTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F3))
                return true;
#endif
            return false;
        }
    }
}
