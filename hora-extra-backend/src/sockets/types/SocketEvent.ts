import { RemoteInfo } from 'dgram';

/**
 * Interface obrigatória para todo Socket Handler (UDP).
 * Cada classe de handler deve implementar o método 'handle'.
 * O parâmetro 'server' é passado como any para evitar dependência circular
 * com UdpSocketManager.
 */
export interface ISocketHandler {
    handle(
        server: any,
        remoteInfo: RemoteInfo,
        data: any
    ): void | Promise<void>;
}

/**
 * Tipo que representa a classe (construtor) de um handler.
 */
export type SocketHandlerConstructor = new () => ISocketHandler;

// ──────────────────────────────────────────────────────────────
// Task system types
// ──────────────────────────────────────────────────────────────

/**
 * Payload C→S: task_assign_request
 * Sem campos — playerId é obtido da sessão.
 */
export type TaskAssignRequest = Record<string, never>;

/**
 * Shape de uma task atribuída, enviada no broadcast task_assigned.
 */
export interface AssignedTaskPayload {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    currentProgress: number;
    status: 'pending' | 'in_progress' | 'completed' | 'failed';
}

/**
 * Payload S→C: task_assigned
 */
export interface TaskAssignedPayload {
    playerId: string;
    tasks: AssignedTaskPayload[];
    /** Total de tarefas do jogador na partida (atribuídas + fila). Constante desde o sorteio. */
    totalTasks: number;
}

// ──────────────────────────────────────────────────────────────
// HUD: relógio do expediente e roster da sala
// ──────────────────────────────────────────────────────────────

/** Payload C→S: shift_clock_request — sem campos (sala vem da sessão). */
export type ShiftClockRequest = Record<string, never>;

/** Payload S→C: shift_clock. Ver ShiftClockSnapshot em services/ShiftClockService.ts. */
export type { ShiftClockSnapshot as ShiftClockPayload } from '../../services/ShiftClockService.js';

/** Payload S→C: room_roster */
export interface RoomRosterPayload {
    /** Jogadores da sala: id + nome de exibição. */
    p: Array<{ id: string; n: string }>;
}
