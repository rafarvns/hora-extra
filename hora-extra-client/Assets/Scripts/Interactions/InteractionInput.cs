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
        /// True no frame em que [E] foi solta. Usado para cancelar uma ação contínua
        /// em andamento sem depender de polling invertido de <see cref="InteractHeld"/>.
        /// </summary>
        public static bool InteractReleased()
        {
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
    }
}
