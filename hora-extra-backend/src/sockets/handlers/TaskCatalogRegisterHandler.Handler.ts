import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * Payload de cada tarefa enviada pelo cliente no catálogo.
 * npcId removido — catálogo é da sala, não vinculado a NPC específico.
 */
interface TaskCatalogEntry {
    id: string;
    description: string;
    type: string;
    targetCount: number;
}

/**
 * Payload esperado: { tasks: Array<{ id, description, type, targetCount }> }
 */
interface TaskCatalogRegisterData {
    tasks: TaskCatalogEntry[];
}

/**
 * TaskCatalogRegisterHandler: recebe o catálogo de tarefas definido pelo cliente e
 * popula o TaskService in-memory para a sala correspondente.
 *
 * Direção: C→S (evento: task_catalog_register)
 */
export class TaskCatalogRegisterHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: TaskCatalogRegisterData): Promise<void> {
        // 1. Validação de payload
        if (!data || !Array.isArray(data.tasks)) {
            logger.warn(`[task_catalog_register] payload inválido de ${rinfo.address}:${rinfo.port} — campo 'tasks' ausente ou não é array`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: "Payload inválido: campo 'tasks' ausente ou não é array" });
            return;
        }

        // 2. Validação de sessão
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            logger.warn(`[task_catalog_register] sessão sem roomId de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Not in a room. Send join_room first.' });
            return;
        }

        // 3. Registrar catálogo via TaskService (payload sem npcId)
        const taskService = ServiceFactory.getTaskService();
        taskService.registerCatalog(session.roomId, data.tasks);

        logger.info(`[UDP_SOCKET] task_catalog_register recebido: ${data.tasks.length} tarefas para sala ${session.roomId}`, { module: 'UDP_SOCKET' });
    }
}
