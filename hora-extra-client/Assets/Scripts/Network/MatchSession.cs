namespace HoraExtra.Network
{
    /// <summary>
    /// Dados da partida atual, só em memória (nada vai para PlayerPrefs: não há conta).
    /// Preenchido pelo <see cref="MatchJoinFlow"/>.
    /// </summary>
    public static class MatchSession
    {
        public static string Token { get; internal set; }
        public static string PlayerId { get; internal set; }
        public static string PlayerName { get; internal set; }
        public static string RoomId { get; internal set; }
        public static string RoomName { get; internal set; }

        /// <summary>
        /// Mensagem para o menu mostrar ao voltar (ex: "Conexão perdida"). O menu lê e
        /// limpa com <see cref="TakeMenuMessage"/>.
        /// </summary>
        public static string MenuMessage { get; internal set; }

        public static bool InMatch => !string.IsNullOrEmpty(RoomId);

        public static string TakeMenuMessage()
        {
            var m = MenuMessage;
            MenuMessage = null;
            return m;
        }

        internal static void Clear()
        {
            Token = PlayerId = PlayerName = RoomId = RoomName = null;
        }
    }
}
