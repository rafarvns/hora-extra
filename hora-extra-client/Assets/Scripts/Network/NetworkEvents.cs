/**
 * NetworkEvents: Centraliza os nomes dos eventos do Socket de forma estática.
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
