namespace HoraExtra.Network
{
    /// <summary>
    /// Estado estático da sessão guest. Setado pelo controller de UI antes de chamar
    /// SocketManager.SetAuthTokenAndReconnect(token). Permite que o SocketManager saiba
    /// que deve aguardar o token guest em vez de autoconectar com o TestToken.
    /// </summary>
    public static class GuestSession
    {
        /// <summary>
        /// Indica que o jogador entrou via fluxo guest (sem cadastro/login).
        /// Sete para true ANTES de chamar SetAuthTokenAndReconnect.
        /// </summary>
        public static bool IsGuestMode { get; set; }

        /// <summary>
        /// ID da sala guest retornada pelo backend — sempre "guest-room".
        /// Disponível para leitura por qualquer componente de gameplay.
        /// </summary>
        public static string GuestRoomId { get; set; }

        /// <summary>Limpa o estado guest (útil ao voltar ao menu principal).</summary>
        public static void Clear()
        {
            IsGuestMode = false;
            GuestRoomId = null;
        }
    }
}
