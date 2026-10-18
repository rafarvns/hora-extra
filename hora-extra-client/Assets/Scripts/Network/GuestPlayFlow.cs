using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using HoraExtra.Network.Rest.Services;

namespace HoraExtra.Network
{
    /// <summary>
    /// Entrar no jogo como convidado: pede o JWT guest, prepara os objetos de rede e carrega
    /// a cena de gameplay. Compartilhado pelo menu novo e pelo MainMenuController legado.
    /// </summary>
    public static class GuestPlayFlow
    {
        public const string DEFAULT_GAME_SCENE = "SCN_FirstFloor";

        /// <summary>Resultado para a UI: sucesso, ou a mensagem de erro em pt-BR.</summary>
        public readonly struct Result
        {
            public readonly bool Success;
            public readonly string Error;

            private Result(bool success, string error)
            {
                Success = success;
                Error = error;
            }

            public static Result Ok() => new Result(true, null);
            public static Result Fail(string error) => new Result(false, error);
        }

        public static async Task<Result> Start(string gameSceneName = DEFAULT_GAME_SCENE)
        {
            Debug.Log("[UI] GuestPlayFlow — solicitando acesso de convidado...");

            var resp = await GuestService.JoinAsGuest();
            if (resp == null || !resp.Success || resp.Data == null)
            {
                string detalhe = resp?.Error?.Message ?? "sem resposta";
                Debug.LogError($"[UI] GuestPlayFlow — guest join falhou: {detalhe}");
                return Result.Fail("Não foi possível conectar ao servidor. Verifique se ele está online.");
            }

            Debug.Log($"[UI] GuestPlayFlow — guest concedido: id={resp.Data.GuestId}, room={resp.Data.RoomId}");

            // Configura a sessão antes do SocketManager conectar.
            NetworkSettings.AuthToken = resp.Data.Token;
            GuestSession.IsGuestMode = true;
            GuestSession.GuestRoomId = resp.Data.RoomId;

            // Objetos de rede criados ANTES do handshake: a cena de level não tem objetos de
            // netplay pré-configurados, e os três sobrevivem ao LoadScene via DontDestroyOnLoad.
            // A ordem importa — o TaskSystemBridge precisa já estar inscrito em CONN_SUCCESS
            // quando a resposta do servidor chegar, senão perderia o registro do catálogo.
            SocketManager.EnsureExists();
            RemotePlayerSpawner.EnsureExists();
            HoraExtra.Characters.TaskSystemBridge.EnsureExists();

            SocketManager.Instance.SetAuthTokenAndReconnect(resp.Data.Token);

            SceneManager.LoadScene(gameSceneName);
            return Result.Ok();
        }
    }
}
