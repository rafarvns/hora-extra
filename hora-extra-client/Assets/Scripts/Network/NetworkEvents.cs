/**
 * NetworkEvents: Centraliza os nomes dos eventos do Socket de forma estática.
 */
public static class NetworkEvents
{
    // Cliente -> Servidor
    public const string JOIN_ROOM = "join_room";
    public const string PLAYER_INPUT = "player_input";
    public const string PING = "ping";

    // Servidor -> Cliente
    public const string CONNECTION_SUCCESS = "connection_success";
    public const string ROOM_JOINED = "room_joined";
    public const string PLAYER_JOINED = "player_joined";
    public const string STATE_UPDATE = "state_update";
    public const string PLAYER_DISCONNECTED = "player_disconnected";
    public const string PONG = "pong";
}
