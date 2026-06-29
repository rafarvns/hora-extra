import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * Payload esperado: {} (sem campos — playerId vem da sessão)
 *
 * TaskAssignRequestHandler: jogador solicita que o servidor sorteie e atribua N tasks.
 *
 * Direção: C→S (evento: task_assign_request)
 * Resposta: S→C broadcast (evento: task_assigned)
 *
 * Validações:
 * - Sessão existe
 * - Sessão tem roomId (jogador está em sala)
 */
export class TaskAssignRequestHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, _data: unknown): Promise<void> {
        // 1. Validação de sessão
        const session = server.getSession(rinfo);
        if (!session) {
            logger.warn(`[task_assign_request] sessão não encontrada para ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Not authenticated. Send CONN first.' });
            return;
        }

        if (!session.roomId) {
            logger.warn(`[task_assign_request] sessão sem roomId de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Not in a room. Send join_room first.' });
            return;
        }

        // 2. Sortear e atribuir tasks via TaskService
        const taskService = ServiceFactory.getTaskService();
        let tasks;
        try {
            tasks = taskService.assignRandomTasks(session.roomId, session.id);
        } catch (err: any) {
            logger.warn(`[task_assign_request] falha ao atribuir tasks para '${session.id}': ${err.message}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: err.message });
            return;
        }

        // 3. Idempotência: não faz broadcast se jogador já tem tasks (retornou [])
        if (tasks.length === 0) {
            logger.debug(`[task_assign_request] jogador '${session.id}' já tem tasks — nenhum broadcast`, { module: 'UDP_SOCKET' });
            return;
        }

        // 4. Broadcast task_assigned para todos na sala (inclusive remetente)
        const broadcastPayload = {
            playerId: session.id,
            tasks,
        };

        server.broadcastToRoom(session.roomId, 'task_assigned', broadcastPayload);

        logger.info(`[UDP_SOCKET] task_assigned: ${tasks.length} tasks atribuídas a '${session.id}' na sala '${session.roomId}'`, { module: 'UDP_SOCKET' });
    }
}
