import logger from '../utils/Logger.js';
import { ApiError } from '../core/ApiError.js';

// ────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────

/** Entrada do catálogo de tarefas (sem npcId — catálogo é da sala, não do NPC). */
export interface TaskEntry {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    roomId: string;
}

/** Tarefa atribuída a um jogador com estado de progresso. */
export interface AssignedTask {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    currentProgress: number;
    status: 'pending' | 'in_progress' | 'completed' | 'failed';
}

const TASK_ASSIGN_COUNT = 3;

/**
 * TaskService: gerencia o catálogo de tarefas e atribuições por jogador (in-memory).
 *
 * O catálogo é organizado POR SALA: cada sala tem seu próprio conjunto de tasks.
 * `assignRandomTasks` sorteia N=3 tasks do catálogo DA SALA via Fisher-Yates.
 * Idempotência silenciosa: segunda solicitação do mesmo jogador retorna [].
 * Sem persistência — estado é resetado ao reiniciar o servidor.
 */
export class TaskService {
    /** Catálogo por sala: roomId → TaskEntry[]. Substituído por registerCatalog. */
    private catalog = new Map<string, TaskEntry[]>();

    /** assignments: playerId → AssignedTask[] */
    private assignments = new Map<string, AssignedTask[]>();

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    /**
     * Substitui o catálogo de tarefas da sala `roomId` pelas entradas fornecidas.
     * Chamado quando o cliente envia task_catalog_register.
     * As tarefas fornecidas não incluem npcId (campo removido nesta versão).
     */
    public registerCatalog(roomId: string, tasks: Array<{ id: string; description: string; type: string; targetCount: number }>): void {
        const entries: TaskEntry[] = tasks.map(t => ({ ...t, roomId }));
        this.catalog.set(roomId, entries);
        logger.info(`TaskService.registerCatalog: ${tasks.length} tarefas registradas para sala ${roomId}`, { module: 'GAME' });
    }

    /**
     * Sorteia N tasks aleatórias do catálogo DA SALA e atribui ao jogador.
     * Idempotente: se o jogador já tem tasks atribuídas, loga warn e retorna [].
     * Se o catálogo da sala tiver menos de N tasks, atribui todas.
     * Lança ApiError se o catálogo da sala estiver vazio ou não existir.
     */
    public assignRandomTasks(roomId: string, playerId: string): AssignedTask[] {
        const roomCatalog = this.catalog.get(roomId);
        if (!roomCatalog || roomCatalog.length === 0) {
            throw ApiError.badRequest(`Catálogo da sala '${roomId}' está vazio — não é possível atribuir tasks`);
        }

        // Idempotência silenciosa
        if (this.assignments.has(playerId) && (this.assignments.get(playerId)!.length > 0)) {
            logger.warn(`TaskService.assignRandomTasks: jogador '${playerId}' já tem tasks atribuídas — ignorando`, { module: 'GAME' });
            return [];
        }

        const n = Math.min(TASK_ASSIGN_COUNT, roomCatalog.length);
        const shuffled = this.fisherYates([...roomCatalog]);
        const picked = shuffled.slice(0, n);

        const assigned: AssignedTask[] = picked.map(entry => ({
            id: entry.id,
            description: entry.description,
            type: entry.type,
            targetCount: entry.targetCount,
            currentProgress: 0,
            status: 'pending',
        }));

        this.assignments.set(playerId, assigned);

        logger.info(`TaskService: ${n} tasks atribuídas ao jogador '${playerId}' na sala '${roomId}'`, { module: 'GAME' });
        return assigned;
    }

    /**
     * Retorna as tasks atribuídas ao jogador, ou [] se não houver.
     */
    public getAssignedTasks(roomId: string, playerId: string): AssignedTask[] {
        return this.assignments.get(playerId) ?? [];
    }

    /**
     * Muda o status de uma task de 'pending' para 'in_progress'.
     * Lança ApiError se: jogador sem tasks, taskId não encontrado, status != pending.
     */
    public startTask(playerId: string, taskId: string): AssignedTask {
        const playerTasks = this.assignments.get(playerId);
        if (!playerTasks || playerTasks.length === 0) {
            throw ApiError.badRequest(`Jogador '${playerId}' não tem tasks atribuídas`);
        }
        const task = playerTasks.find(t => t.id === taskId);
        if (!task) {
            throw ApiError.badRequest(`Task '${taskId}' não encontrada para jogador '${playerId}'`);
        }
        if (task.status !== 'pending') {
            throw ApiError.badRequest(`Transição inválida: task '${taskId}' está '${task.status}', esperado 'pending'`);
        }
        task.status = 'in_progress';
        logger.info(`TaskService.startTask: task '${taskId}' iniciada pelo jogador '${playerId}'`, { module: 'GAME' });
        return task;
    }

    /**
     * Resolve uma task em andamento. success=true → 'completed' (seta currentProgress=targetCount).
     * success=false → 'failed'. Lança ApiError se status != 'in_progress' ou taskId inválido.
     */
    public resolveTask(playerId: string, taskId: string, success: boolean): AssignedTask {
        const playerTasks = this.assignments.get(playerId);
        if (!playerTasks || playerTasks.length === 0) {
            throw ApiError.badRequest(`Jogador '${playerId}' não tem tasks atribuídas`);
        }
        const task = playerTasks.find(t => t.id === taskId);
        if (!task) {
            throw ApiError.badRequest(`Task '${taskId}' não encontrada para jogador '${playerId}'`);
        }
        if (task.status !== 'in_progress') {
            throw ApiError.badRequest(`Transição inválida: task '${taskId}' está '${task.status}', esperado 'in_progress'`);
        }
        if (success) {
            task.status = 'completed';
            task.currentProgress = task.targetCount;
        } else {
            task.status = 'failed';
        }
        logger.info(`TaskService.resolveTask: task '${taskId}' do jogador '${playerId}' → '${task.status}'`, { module: 'GAME' });
        return task;
    }

    /**
     * Remove todas as entradas de atribuição dos playerIds fornecidos.
     * Chamado por UdpSocketManager.resetRoomState.
     */
    public clearRoom(playerIds: string[]): void {
        for (const pid of playerIds) {
            this.assignments.delete(pid);
        }
        logger.info(`TaskService.clearRoom: ${playerIds.length} atribuições removidas`, { module: 'GAME' });
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    /** Fisher-Yates in-place shuffle, returns the array for chaining. */
    private fisherYates<T>(arr: T[]): T[] {
        for (let i = arr.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [arr[i], arr[j]] = [arr[j], arr[i]];
        }
        return arr;
    }
}

export default new TaskService();
