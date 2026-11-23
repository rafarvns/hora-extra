using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Ponto único de leitura da tecla de interação [E].
    ///
    /// Existe para eliminar o par de blocos #if ENABLE_INPUT_SYSTEM /
    /// ENABLE_LEGACY_INPUT_MANAGER que estava copiado em MissionPaperCollectible e
    /// CoffeeMakerInteraction. O projeto pode estar configurado como "Input System",
    /// "Input Manager" ou "Both" — com os dois blocos ativos, ler nos dois lugares é
    /// inofensivo porque um dos dois simplesmente não compila.
    ///
    /// <see cref="InteractPressed"/> é borda (um frame só) e serve para ações pontuais:
    /// pegar, guardar, abrir. <see cref="InteractHeld"/> é contínuo e serve para ações
    /// de manter pressionado, como limpar uma sujeira com o rodo.
    /// </summary>
    public static class InteractionInput
    {
        /// <summary>
        /// True no frame em que [E] foi pressionada. Use para ações pontuais —
        /// chamar isto em Update dispara no máximo uma vez por toque.
        /// </summary>
        public static bool InteractPressed()
        {
            if (PlayerActionLock.IsBusy) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E))
                return true;
#endif

            return false;
        }

        /// <summary>
        /// True enquanto [E] estiver pressionada. Use para ações contínuas
        /// (segurar para limpar/varrer). Consumido a partir do plano 0006.
        /// </summary>
        public static bool InteractHeld()
        {
            if (PlayerActionLock.IsBusy) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.isPressed)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.E))
                return true;
#endif

            return false;
        }

        /// <summary>
        /// True no frame em que [Q] foi pressionada — largar o que está na mão.
        ///
        /// Existe como válvula de escape: um item que a task não aceita mais (porque já foi
        /// concluída) ficaria preso na mão para sempre sem isto.
        /// </summary>
        public static bool DropPressed()
        {
            if (PlayerActionLock.IsBusy) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Q))
                return true;
#endif

            return false;
        }

        /// <summary>
        /// True no frame em que o botão de arremesso (clique esquerdo) foi pressionado.
        /// Usado por <see cref="PaperThrower"/> para lançar o item arremessável na mão.
        /// </summary>
        public static bool ThrowPressed()
        {
            if (PlayerActionLock.IsBusy) return false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
                return true;
#endif

            return false;
        }

        /// <summary>
        /// True no frame em que [E] foi solta. Usado para cancelar uma ação contínua
        /// em andamento sem depender de polling invertido de <see cref="InteractHeld"/>.
        /// </summary>
        public static bool InteractReleased()
        {
            if (PlayerActionLock.IsBusy) return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasReleasedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyUp(KeyCode.E))
                return true;
#endif

            return false;
        }

        // =====================================================================
        // Leituras de UI (teclado do cofre, painel do computador, elevador).
        // NÃO passam pelo bloqueio da PlayerActionLock: quem está com a trava é
        // justamente quem precisa ler estas teclas.
        // =====================================================================

        /// <summary>
        /// True no frame em que um dígito 0–9 foi pressionado (linha de cima ou numpad).
        /// </summary>
        public static bool DigitPressed(out int digit)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                for (int i = 0; i <= 9; i++)
                {
                    if (kb[DigitKey(i)].wasPressedThisFrame || kb[NumpadKey(i)].wasPressedThisFrame)
                    {
                        digit = i;
                        return true;
                    }
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                {
                    digit = i;
                    return true;
                }
            }
#endif

            digit = -1;
            return false;
        }

        /// <summary>True no frame em que [Backspace] foi pressionada.</summary>
        public static bool BackspacePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Backspace))
                return true;
#endif

            return false;
        }

        /// <summary>True no frame em que [Enter] (ou o Enter do numpad) foi pressionado.</summary>
        public static bool SubmitPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                return true;
#endif

            return false;
        }

        /// <summary>True no frame em que [Esc] foi pressionada — fechar painel.</summary>
        public static bool CancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
                return true;
#endif

            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private static Key DigitKey(int i) => i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1);
        private static Key NumpadKey(int i) => Key.Numpad0 + i;
#endif
    }
}
