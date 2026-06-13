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
    status: 'pending' | 'in_progress' | 'completed';
}

/**
 * Payload S→C: task_assigned
 */
export interface TaskAssignedPayload {
    playerId: string;
    tasks: AssignedTaskPayload[];
}
