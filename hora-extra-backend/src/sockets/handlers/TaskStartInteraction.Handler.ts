import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * Payload esperado: { taskId: string }
 *
 * TaskStartInteractionHandler: jogador inicia interação com um objeto de task.
 * Valida que o jogador possui a task e que está 'pending' → muda para 'in_progress'.
 *
 * Direção: C→S (evento: task_start_interaction)
 * Resposta: S→C broadcast (evento: task_updated) ou sendTo ERROR
 */
interface TaskStartInteractionData {
    taskId: string;
}

export class TaskStartInteractionHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: TaskStartInteractionData): Promise<void> {
        // 1. Validação de payload
        if (!data || typeof data.taskId !== 'string' || data.taskId.trim() === '') {
            logger.warn(`[task_start_interaction] payload inválido de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Payload inválido: taskId obrigatório' });
            return;
        }

        // 2. Validação de sessão
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            logger.warn(`[task_start_interaction] sessão inválida ou sem roomId de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Sessão inválida ou jogador não está em uma sala' });
            return;
        }

        // 3. Chamar TaskService.startTask
        const taskService = ServiceFactory.getTaskService();
        let task;
        try {
            task = taskService.startTask(session.id, data.taskId);
        } catch (err: any) {
            logger.warn(`[task_start_interaction] falha ao iniciar task '${data.taskId}' para '${session.id}': ${err.message}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: err.message });
            return;
        }

        // 4. Broadcast task_updated para a sala
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
