import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import { TaskRejectionError } from '../../services/TaskService.js';
import logger from '../../utils/Logger.js';

/**
 * Payload esperado: { taskId: string, itemId?: string, slotId?: string }
 *
 * TaskProgressHandler: jogador entregou um item de uma task incremental (ex: 'collect').
 * Cada pacote soma +1 no progresso da task no servidor. O servidor decide quando a
 * task vira 'in_progress' (primeiro incremento) e 'completed' (atinge targetCount) —
 * nunca confia em contagem enviada pelo cliente (server-authoritative).
 *
 * `itemId`/`slotId` são opcionais e identificam QUAL item foi entregue e ONDE. Quando a
 * entrada de catálogo declara `items`/`pairs`, o servidor usa esses campos para validar
 * pertencimento, pareamento e não-repetição.
 *
 * Direção: C→S (evento: task_progress)
 * Resposta: S→C broadcast (task_updated) | sendTo task_rejected (recusa de gameplay)
 *           | sendTo ERROR (falha de protocolo)
 */
interface TaskProgressData {
    taskId: string;
    itemId?: string;
    slotId?: string;
}

export class TaskProgressHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: TaskProgressData): Promise<void> {
        // 1. Validação de payload
        if (!data || typeof data.taskId !== 'string' || data.taskId.trim() === '') {
            logger.warn(`[task_progress] payload inválido de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Payload inválido: taskId obrigatório' });
            return;
        }

        // 1b. Campos opcionais: presentes têm que ser string. Tipo errado é falha de
        //     protocolo (ERROR genérico), não recusa de gameplay (task_rejected).
        if (data.itemId !== undefined && typeof data.itemId !== 'string') {
            logger.warn(`[task_progress] itemId de tipo inválido de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Payload inválido: itemId deve ser string' });
            return;
        }
        if (data.slotId !== undefined && typeof data.slotId !== 'string') {
            logger.warn(`[task_progress] slotId de tipo inválido de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Payload inválido: slotId deve ser string' });
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
            task = taskService.incrementProgress(session.id, data.taskId, data.itemId, data.slotId);
        } catch (err: any) {
            // Recusa de gameplay → task_rejected unicast, com o motivo estruturado.
            // É feedback do remetente, não estado de sala — por isso sendTo e não broadcast.
            if (err instanceof TaskRejectionError) {
                logger.info(`[task_progress] recusado (${err.code}) para '${session.id}' na task '${data.taskId}': ${err.message}`, { module: 'UDP_SOCKET' });
                server.sendTo(rinfo, 'task_rejected', {
                    taskId: data.taskId,
                    code: err.code,
                    message: err.message,
                    ...(data.itemId !== undefined ? { itemId: data.itemId } : {}),
                });
                return;
            }

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

        // Fila sequencial: concluiu, já libera a próxima para o jogador não ficar ocioso.
        releaseNextTask(server, session, taskService);
    }
}

/**
 * Libera a próxima tarefa da fila do jogador e anuncia via task_assigned.
 *
 * Fica fora da classe porque o TaskCompleteAttemptHandler faz exatamente o mesmo
 * após resolver uma task — são os dois pontos do protocolo em que uma tarefa pode
 * terminar.
 */
export function releaseNextTask(server: any, session: any, taskService: any): void {
    const proxima = taskService.advanceQueue(session.id);
    if (!proxima) return;

    server.broadcastToRoom(session.roomId, 'task_assigned', {
        playerId: session.id,
        tasks: [proxima],
    });

    logger.info(`[UDP_SOCKET] task_assigned (fila): '${proxima.id}' liberada para '${session.id}'`, { module: 'UDP_SOCKET' });
}
