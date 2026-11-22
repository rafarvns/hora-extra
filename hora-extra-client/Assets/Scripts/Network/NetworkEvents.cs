/**
 * NetworkEvents: Centraliza os nomes dos eventos do Socket de forma estática.
 *
 * Entrada numa partida (ver MatchJoinFlow e COMMUNICATION.md):
 *   1. POST /api/session { nome } → { token, playerId, nome }.
 *   2. POST /api/rooms { nome } (só quem cria) → { id }.
 *   3. CONN { token, d: { roomId } } → CONN_SUCCESS + ROOM_STATE, ou CONN_ERROR { code }.
 *   4. Ao sair: LEAVE_ROOM (libera a vaga na hora).
 */
public static class NetworkEvents
{
    // Cliente -> Servidor
    public const string CONN = "CONN";
    public const string LEAVE_ROOM = "leave_room";
    public const string PLAYER_MOVE = "player_move";
    public const string PLAYER_SPRINT = "player_sprint";
    public const string NPC_REGISTER = "npc_register";
    public const string NPC_MOVE_REQUEST = "npc_move_request";
    public const string PING = "ping";
    public const string TASK_CATALOG_REGISTER = "task_catalog_register";
    public const string TASK_ASSIGN_REQUEST = "task_assign_request";
    public const string TASK_START_INTERACTION = "task_start_interaction";
    public const string TASK_COMPLETE_ATTEMPT = "task_complete_attempt";
    public const string TASK_PROGRESS = "task_progress";

    /// <summary>
    /// Pede o relógio do expediente da sala. O primeiro pedido da sala inicia o
    /// expediente; os seguintes só sincronizam. Payload: {}. Resposta: SHIFT_CLOCK.
    /// </summary>
    public const string SHIFT_CLOCK_REQUEST = "shift_clock_request";

    // Servidor -> Cliente
    public const string CONN_SUCCESS = "CONN_SUCCESS";
    public const string CONN_ERROR = "CONN_ERROR";

    /// <summary>Evento local (disparado pelo SocketManager) quando o CONN é aceito.</summary>
    public const string CONNECTION_SUCCESS = "connection_success";

    /// <summary>
    /// Retrato da sala para quem acabou de entrar (unicast, logo após CONN_SUCCESS).
    /// Payload: { t: [{ id, s, p, ci, h }], pl: [{ id, n, p, r, f }] } — ver RoomStatePayload.
    /// </summary>
    public const string ROOM_STATE = "room_state";

    public const string PLAYER_JOINED = "player_joined";
    public const string NPC_MOVE = "npc_move";
    public const string NPC_REGISTERED = "npc_registered";
    public const string PLAYER_DISCONNECTED = "player_disconnected";
    public const string PONG = "pong";
    public const string TASK_ASSIGNED = "task_assigned";
    public const string TASK_UPDATED = "task_updated";

    /// <summary>
    /// Unicast ao remetente: o servidor recusou um task_progress por regra de gameplay
    /// (item errado, destino errado, item repetido...). Diferente de ERROR, que sinaliza
    /// pacote malformado. Payload: { taskId, code, message, itemId? }.
    /// </summary>
    public const string TASK_REJECTED = "task_rejected";

    /// <summary>
    /// Relógio do expediente (autoritativo). Payload: { m, s, e, k, st } — ver
    /// ShiftClockPayload. O cliente extrapola m + k·Δt entre sincronizações.
    /// </summary>
    public const string SHIFT_CLOCK = "shift_clock";

    /// <summary>
    /// Lista completa de jogadores da sala, reenviada a cada entrada/saída.
    /// Payload: { p: [{ id, n }] } — ver RoomRosterPayload.
    /// </summary>
    public const string ROOM_ROSTER = "room_roster";
}
