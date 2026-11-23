using System;
using UnityEngine;
using HoraExtra.Characters;

namespace HoraExtra.Interactions
{
    /// <summary>
    /// Trava de "jogador ocupado": enquanto um dono (cofre recolhendo dinheiro, painel do
    /// computador, teclado do cofre) estiver com a trava, o jogador não anda, não gira a
    /// câmera e as teclas de interação do mundo ([E], [Q], clique) ficam mudas — ver o
    /// bloqueio em <see cref="InteractionInput"/>.
    ///
    /// Um dono por vez. <see cref="Release"/> só solta se quem chama for o dono, então um
    /// objeto que sai de cena não destrava a ação de outro. Os donos chamam Release no
    /// OnDisable para nunca deixar o jogador preso.
    /// </summary>
    public static class PlayerActionLock
    {
        /// <summary>Disparado com true ao travar e false ao destravar.</summary>
        public static event Action<bool> OnChanged;

        public static bool IsBusy => _owner != null;
        public static object Owner => _owner;

        private static object _owner;
        private static PlayerController _controller;
        private static bool _freedCursor;
        private static CursorLockMode _previousLock;
        private static bool _previousVisible;

        /// <summary>
        /// Tenta pegar a trava. Devolve false se outro dono já estiver com ela.
        /// </summary>
        /// <param name="lockMovement">Trava andar e câmera (PlayerController).</param>
        /// <param name="freeCursor">Solta o cursor para clicar em UI.</param>
        public static bool Acquire(object owner, bool lockMovement, bool freeCursor)
        {
            if (owner == null) return false;
            if (_owner != null && !ReferenceEquals(_owner, owner)) return false;

            _owner = owner;

            if (lockMovement)
            {
                _controller = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (_controller != null) _controller.SetMovementLocked(true);
            }

            _freedCursor = freeCursor;
            if (freeCursor)
            {
                _previousLock = Cursor.lockState;
                _previousVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            OnChanged?.Invoke(true);
            return true;
        }

        /// <summary>Solta a trava, se <paramref name="owner"/> for o dono atual.</summary>
        public static void Release(object owner)
        {
            if (_owner == null || !ReferenceEquals(_owner, owner)) return;

            if (_controller != null) _controller.SetMovementLocked(false);
            _controller = null;

            if (_freedCursor)
            {
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousVisible;
            }
            _freedCursor = false;

            _owner = null;
            OnChanged?.Invoke(false);
        }

        /// <summary>
        /// Zera estado estático ao entrar no Play Mode com "Enter Play Mode Options" sem
        /// domain reload — senão uma trava da sessão anterior sobreviveria.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _owner = null;
            _controller = null;
            _freedCursor = false;
            OnChanged = null;
        }
    }
}
