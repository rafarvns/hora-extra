import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * Payload de cada tarefa enviada pelo cliente no catálogo.
 * npcId removido — catálogo é da sala, não vinculado a NPC específico.
 *
 * `items` e `pairs` são opcionais (plano 0004) e dizem ao servidor QUAIS itens contam
 * progresso nesta task e, no caso de `pairs`, em qual destino cada um deve ser entregue.
 */
interface TaskCatalogEntry {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    items?: string[];
    pairs?: Array<{ itemId: string; slotId: string }>;
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

        // 3. Sanitizar items/pairs e registrar catálogo via TaskService (payload sem npcId)
        const taskService = ServiceFactory.getTaskService();
        const sanitized = data.tasks.map(task => this.sanitizeEntry(task, session.roomId));
        taskService.registerCatalog(session.roomId, sanitized);

        logger.info(`[UDP_SOCKET] task_catalog_register recebido: ${data.tasks.length} tarefas para sala ${session.roomId}`, { module: 'UDP_SOCKET' });
    }

    /**
     * Devolve a entrada com `items`/`pairs` validados.
     *
     * Campo malformado é DESCARTADO com warn, nunca derruba o registro da sala inteira:
     * um catálogo recusado deixaria o jogador sem nenhuma tarefa, o que é um estrago
     * muito maior do que uma task que volta a contar às cegas.
     */
    private sanitizeEntry(task: TaskCatalogEntry, roomId: string): TaskCatalogEntry {
        const clean: TaskCatalogEntry = {
            id: task.id,
            description: task.description,
            type: task.type,
            targetCount: task.targetCount,
        };

        if (task.items !== undefined) {
            const items = this.sanitizeItems(task.items, task.id, roomId);
            if (items) clean.items = items;
        }

        if (task.pairs !== undefined) {
            const pairs = this.sanitizePairs(task.pairs, task.id, roomId);
            if (pairs) clean.pairs = pairs;
        }

        return clean;
    }

    /** `items` válido = array com ao menos uma string. Elementos não-string são filtrados. */
    private sanitizeItems(raw: unknown, taskId: string, roomId: string): string[] | undefined {
        if (!Array.isArray(raw)) {
            logger.warn(`[task_catalog_register] 'items' da task '${taskId}' (sala ${roomId}) não é array — campo descartado`, { module: 'UDP_SOCKET' });
            return undefined;
        }

        const items = raw.filter((i): i is string => typeof i === 'string' && i.trim() !== '');
        if (items.length !== raw.length) {
            logger.warn(`[task_catalog_register] 'items' da task '${taskId}' (sala ${roomId}) tinha ${raw.length - items.length} entrada(s) inválida(s) — descartadas`, { module: 'UDP_SOCKET' });
        }

        // Lista vazia significaria "nenhum item é válido" e travaria a task —
        // indistinguível de campo ausente, então é descartada por inteiro.
        return items.length > 0 ? items : undefined;
    }

    /** `pairs` válido = array de `{ itemId: string, slotId: string }`. Entradas tortas são filtradas. */
    private sanitizePairs(raw: unknown, taskId: string, roomId: string): Array<{ itemId: string; slotId: string }> | undefined {
        if (!Array.isArray(raw)) {
            logger.warn(`[task_catalog_register] 'pairs' da task '${taskId}' (sala ${roomId}) não é array — campo descartado`, { module: 'UDP_SOCKET' });
            return undefined;
        }

        const pairs = raw
            .filter((p: any) => p && typeof p.itemId === 'string' && p.itemId.trim() !== ''
                && typeof p.slotId === 'string' && p.slotId.trim() !== '')
            .map((p: any) => ({ itemId: p.itemId, slotId: p.slotId }));

        if (pairs.length !== raw.length) {
            logger.warn(`[task_catalog_register] 'pairs' da task '${taskId}' (sala ${roomId}) tinha ${raw.length - pairs.length} entrada(s) sem itemId/slotId — descartadas`, { module: 'UDP_SOCKET' });
        }

        return pairs.length > 0 ? pairs : undefined;
    }
}
