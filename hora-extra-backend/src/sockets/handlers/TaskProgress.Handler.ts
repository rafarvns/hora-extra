import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * Payload esperado: { taskId: string }
 *
 * TaskProgressHandler: jogador coletou um item de uma task incremental (ex: 'collect').
 * Cada pacote soma +1 no progresso da task no servidor. O servidor decide quando a
 * task vira 'in_progress' (primeiro incremento) e 'completed' (atinge targetCount) —
 * nunca confia em contagem enviada pelo cliente (server-authoritative).
 *
 * Direção: C→S (evento: task_progress)
 * Resposta: S→C broadcast (evento: task_updated) ou sendTo ERROR
 */
interface TaskProgressData {
    taskId: string;
}

export class TaskProgressHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: TaskProgressData): Promise<void> {
        // 1. Validação de payload
        if (!data || typeof data.taskId !== 'string' || data.taskId.trim() === '') {
            logger.warn(`[task_progress] payload inválido de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Payload inválido: taskId obrigatório' });
            return;
        }

        // 2. Validação de sessão
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            logger.warn(`[task_progress] sessão inválida ou sem roomId de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Sessão inválida ou jogador não está em uma sala' });
            return;
        }

        // 3. Chamar TaskService.incrementProgress
        const taskService = ServiceFactory.getTaskService();
        let task;
        try {
            task = taskService.incrementProgress(session.id, data.taskId);
        } catch (err: any) {
            logger.warn(`[task_progress] falha ao incrementar task '${data.taskId}' para '${session.id}': ${err.message}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: err.message });
            return;
        }

        // 4. Broadcast task_updated para a sala — payload construído pelo servidor
        const broadcastPayload = {
            playerId: session.id,
            taskId: task.id,
            currentProgress: task.currentProgress,
            status: task.status,
        };
        server.broadcastToRoom(session.roomId, 'task_updated', broadcastPayload);

        logger.info(`[UDP_SOCKET] task_updated: task '${task.id}' do jogador '${session.id}' → ${task.currentProgress}/${task.targetCount} (${task.status})`, { module: 'UDP_SOCKET' });
    }
}
