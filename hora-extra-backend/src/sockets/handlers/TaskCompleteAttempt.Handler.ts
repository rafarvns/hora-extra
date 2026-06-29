import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * Payload esperado: { taskId: string, success: boolean }
 *
 * TaskCompleteAttemptHandler: cliente reporta o resultado do minigame QTE.
 * O servidor valida a transição de estado e determina o status final — nunca reflete
 * o campo success cru do cliente sem passar pelo service (server-authoritative).
 *
 * Direção: C→S (evento: task_complete_attempt)
 * Resposta: S→C broadcast (evento: task_updated) ou sendTo ERROR
 */
interface TaskCompleteAttemptData {
    taskId: string;
    success: boolean;
}

export class TaskCompleteAttemptHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: TaskCompleteAttemptData): Promise<void> {
        // 1. Validação de payload
        if (!data || typeof data.taskId !== 'string' || data.taskId.trim() === '' || typeof data.success !== 'boolean') {
            logger.warn(`[task_complete_attempt] payload inválido de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Payload inválido: taskId (string) e success (boolean) obrigatórios' });
            return;
        }

        // 2. Validação de sessão
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            logger.warn(`[task_complete_attempt] sessão inválida ou sem roomId de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Sessão inválida ou jogador não está em uma sala' });
            return;
        }

        // 3. Chamar TaskService.resolveTask
        const taskService = ServiceFactory.getTaskService();
        let task;
        try {
            task = taskService.resolveTask(session.id, data.taskId, data.success);
        } catch (err: any) {
            logger.warn(`[task_complete_attempt] falha ao resolver task '${data.taskId}' para '${session.id}': ${err.message}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: err.message });
            return;
        }

        // 4. Broadcast task_updated para a sala — payload construído pelo servidor (não reflete campos crus do cliente)
        const broadcastPayload = {
            playerId: session.id,
            taskId: task.id,
            currentProgress: task.currentProgress,
            status: task.status,
        };
        server.broadcastToRoom(session.roomId, 'task_updated', broadcastPayload);

        logger.info(`[UDP_SOCKET] task_updated: task '${task.id}' do jogador '${session.id}' → '${task.status}'`, { module: 'UDP_SOCKET' });
    }
}
