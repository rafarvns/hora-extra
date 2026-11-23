using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using HoraExtra.Network.Rest.Services;
using HoraExtra.Settings;

namespace HoraExtra.Network
{
    /// <summary>
    /// Entrar numa partida: abre a sessão com o nome digitado, cria a sala (se for o caso),
    /// conecta pelo UDP e, só depois do servidor aceitar, carrega o 1º andar. Todos começam
    /// no 1º andar, inclusive quem entra no meio da partida.
    ///
    /// Também cuida da volta ao menu: <see cref="LeaveToMenu"/> e queda de conexão.
    /// </summary>
    public static class MatchJoinFlow
    {
        public const string FIRST_FLOOR_SCENE = FloorTracker.SCENE_FLOOR_1;
        public const string MENU_SCENE = "MainMenuScene";

        private const float CONNECT_TIMEOUT_SECONDS = 8f;

        /// <summary>Resultado para a UI: sucesso, ou a mensagem de erro em pt-BR.</summary>
        public readonly struct Result
        {
            public readonly bool Success;
            public readonly string Error;
            public readonly string Code;

            private Result(bool success, string error, string code)
            {
                Success = success;
                Error = error;
                Code = code;
            }

            public static Result Ok() => new Result(true, null, null);
            public static Result Fail(string error, string code = null) => new Result(false, error, code);
        }

        /// <summary>Cria uma sala nova e entra nela.</summary>
        public static async Task<Result> CreateAndJoin(string nomeJogador, string nomeSala)
        {
            var sessao = await AbrirSessao(nomeJogador);
            if (!sessao.Success) return sessao;

            var sala = await RoomService.CreateRoom(nomeSala);
            if (sala == null || !sala.Success || sala.Data == null)
                return Result.Fail(ErroHttp(sala?.Error?.Message, "Não foi possível criar a sala."));

            return await Conectar(sala.Data.Id, sala.Data.Nome);
        }

        /// <summary>Entra numa sala existente (inclusive em andamento).</summary>
        public static async Task<Result> Join(string nomeJogador, string roomId, string nomeSala)
        {
            var sessao = await AbrirSessao(nomeJogador);
            if (!sessao.Success) return sessao;
            return await Conectar(roomId, nomeSala);
        }

        /// <summary>Sai da sala e volta ao menu principal.</summary>
        public static void LeaveToMenu(string mensagem = null)
        {
            SocketManager.Instance?.Disconnect();
            MatchSession.Clear();
            MatchSession.MenuMessage = mensagem;

            // A HUD in-game é persistente (DontDestroyOnLoad): sem isto ela apareceria por
            // cima do menu. A próxima partida recria a dela a partir da cena do 1º andar.
            if (HoraExtra.UI.Hud.HudRoot.Instance != null)
                Object.Destroy(HoraExtra.UI.Hud.HudRoot.Instance.gameObject);

            SceneManager.LoadScene(MENU_SCENE);
        }

        // === Etapas ===

        private static async Task<Result> AbrirSessao(string nomeJogador)
        {
            var resp = await RoomService.CreateSession(nomeJogador);
            if (resp == null || !resp.Success || resp.Data == null)
                return Result.Fail(ErroHttp(resp?.Error?.Message, "Não foi possível falar com o servidor."));

            MatchSession.Token = resp.Data.Token;
            MatchSession.PlayerId = resp.Data.PlayerId;
            MatchSession.PlayerName = resp.Data.Nome;
            GameSettings.Nickname = resp.Data.Nome;
            Debug.Log($"[NETWORK] Sessão aberta: {resp.Data.PlayerId} ({resp.Data.Nome})");
            return Result.Ok();
        }

        private static async Task<Result> Conectar(string roomId, string nomeSala)
        {
            // Objetos de rede criados ANTES do handshake e persistentes entre cenas. A ordem
            // importa: a ponte de tarefas e o spawner precisam estar inscritos em
            // CONN_SUCCESS / room_state quando a resposta chegar.
            var socket = SocketManager.EnsureExists();
            RemotePlayerSpawner.EnsureExists();
            HoraExtra.Characters.TaskSystemBridge.EnsureExists();
            ConnectionWatcher.EnsureInstalled();

            var tcs = new TaskCompletionSource<Result>();
            void Ok(string _) => tcs.TrySetResult(Result.Ok());
            void Erro(string code, string msg) => tcs.TrySetResult(Result.Fail(TraduzirErro(code, msg), code));

            socket.OnConnected += Ok;
            socket.OnConnectionError += Erro;
            try
            {
                socket.Connect(MatchSession.Token, roomId);
                var timeout = Task.Delay((int)(CONNECT_TIMEOUT_SECONDS * 1000));
                var venceu = await Task.WhenAny(tcs.Task, timeout);
                var result = venceu == tcs.Task ? tcs.Task.Result : Result.Fail("O servidor não respondeu.", "TIMEOUT");

                if (!result.Success)
                {
                    socket.Disconnect();
                    return result;
                }
            }
            finally
            {
                socket.OnConnected -= Ok;
                socket.OnConnectionError -= Erro;
            }

            MatchSession.RoomId = roomId;
            MatchSession.RoomName = nomeSala;
            Debug.Log($"[NETWORK] Entrou na sala '{nomeSala}' ({roomId}) — carregando o 1º andar.");
            SceneManager.LoadScene(FIRST_FLOOR_SCENE);
            return Result.Ok();
        }

        private static string TraduzirErro(string code, string msg)
        {
            switch (code)
            {
                case "ROOM_FULL": return "A sala está cheia (2 de 2).";
                case "ROOM_NOT_FOUND": return "Essa sala não existe mais. Atualize a lista.";
                case "INVALID_TOKEN": return "Sessão inválida. Tente de novo.";
                case "TIMEOUT": return "O servidor não respondeu.";
                default: return string.IsNullOrEmpty(msg) ? "Não foi possível entrar na sala." : msg;
            }
        }

        private static string ErroHttp(string doServidor, string padrao)
        {
            // Erros de validação (nome curto etc.) vêm prontos do servidor em pt-BR.
            return string.IsNullOrEmpty(doServidor) || doServidor.StartsWith("Cannot") || doServidor.Contains("HTTP")
                ? padrao
                : doServidor;
        }
    }

    /// <summary>
    /// Vigia persistente: se a conexão cair no meio da partida, volta ao menu com um aviso.
    /// </summary>
    public class ConnectionWatcher : MonoBehaviour
    {
        private static ConnectionWatcher _instance;

        public static void EnsureInstalled()
        {
            if (_instance != null) return;
            var go = new GameObject("ConnectionWatcher (runtime)");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ConnectionWatcher>();
        }

        private SocketManager _socket;

        private void Update()
        {
            // O SocketManager pode ser (re)criado depois deste objeto: assina quando aparecer.
            if (_socket == SocketManager.Instance) return;
            if (_socket != null) _socket.OnConnectionLost -= AoPerderConexao;
            _socket = SocketManager.Instance;
            if (_socket != null) _socket.OnConnectionLost += AoPerderConexao;
        }

        private void OnDestroy()
        {
            if (_socket != null) _socket.OnConnectionLost -= AoPerderConexao;
        }

        private static void AoPerderConexao()
        {
            if (!MatchSession.InMatch) return;
            Debug.LogWarning("[NETWORK] Conexão perdida — voltando ao menu.");
            MatchJoinFlow.LeaveToMenu("A conexão com o servidor caiu.");
        }
    }
}
