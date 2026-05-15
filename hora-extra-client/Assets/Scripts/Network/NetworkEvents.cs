/**
 * NetworkEvents: Centraliza os nomes dos eventos do Socket de forma estática.
 *
 * Fluxo Guest Mode (2-player sem cadastro):
 *   1. UI chama GuestService.JoinAsGuest() → POST /api/auth/guest → recebe { token, guestId, roomId }.
 *   2. UI seta GuestSession.IsGuestMode = true e GuestSession.GuestRoomId = roomId.
 *   3. UI chama SocketManager.Instance.SetAuthTokenAndReconnect(token).
 *   4. SocketManager envia CONN com o JWT guest.
 *   5. Servidor detecta prefixo "guest-" no decoded.id e auto-joina "guest-room".
 *   6. Servidor responde CONN_SUCCESS + room_joined automaticamente.
 *   7. Nenhum evento novo é necessário — todos os eventos abaixo são reaproveitados.
 *
 * Nenhuma constante nova foi adicionada para o fluxo guest; os eventos existentes
 * (CONN_SUCCESS, ROOM_JOINED, PLAYER_JOINED, PLAYER_MOVE, etc.) cobrem todo o ciclo.
 */
public static class NetworkEvents
{
    // Cliente -> Servidor
    public const string JOIN_ROOM = "join_room";
    public const string PLAYER_INPUT = "player_input";
    public const string PLAYER_MOVE = "player_move";
    public const string PLAYER_SPRINT = "player_sprint";
    public const string NPC_REGISTER = "npc_register";
    public const string NPC_MOVE_REQUEST = "npc_move_request";
    public const string PING = "ping";

    // Servidor -> Cliente
    public const string CONNECTION_SUCCESS = "connection_success";
    public const string ROOM_JOINED = "room_joined";
    public const string PLAYER_JOINED = "player_joined";
    public const string STATE_UPDATE = "state_update";
    public const string NPC_MOVE = "npc_move";
    public const string NPC_REGISTERED = "npc_registered";
    public const string PLAYER_DISCONNECTED = "player_disconnected";
    public const string PONG = "pong";
}
