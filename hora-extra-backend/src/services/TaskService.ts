import logger from '../utils/Logger.js';
import { ApiError } from '../core/ApiError.js';

// ────────────────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────────────────

/** Par item→destino exigido por tarefas de pareamento (ex: encomenda → estante correta). */
export interface TaskItemPair {
    itemId: string;
    slotId: string;
}

/** Entrada do catálogo de tarefas (sem npcId — catálogo é da sala, não do NPC). */
export interface TaskEntry {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    roomId: string;
    /** itemIds que contam progresso nesta task. Ausente = contagem cega de +1. */
    items?: string[];
    /** Destino obrigatório por item. Ausente = qualquer destino é aceito. */
    pairs?: TaskItemPair[];
}

/**
 * Motivos pelos quais o servidor recusa um progresso de gameplay.
 * Diferente de erro de protocolo (payload malformado), que responde ERROR genérico.
 */
export type TaskRejectionCode =
    | 'NOT_ASSIGNED'      // jogador não tem a task
    | 'INVALID_STATUS'    // task já completed/failed
    | 'MISSING_ITEM'      // catálogo exige itemId e o payload não mandou
    | 'UNKNOWN_ITEM'      // itemId não pertence a esta task
    | 'WRONG_SLOT'        // par (itemId, slotId) não confere
    | 'ALREADY_COUNTED';  // itemId já contabilizado nesta atribuição

/**
 * Recusa de gameplay tipada.
 *
 * Estende ApiError de propósito: os `catch (err: any)` que já existem nos handlers
 * antigos continuam lendo `err.message` sem alteração; quem precisa do motivo
 * estruturado lê `err.code`.
 */
export class TaskRejectionError extends ApiError {
    public readonly code: TaskRejectionCode;

    constructor(code: TaskRejectionCode, message: string) {
        super(message, 400);
        this.name = 'TaskRejectionError';
        this.code = code;
        // Necessário para `instanceof` sobreviver à transpilação, mesmo padrão do ApiError.
        Object.setPrototypeOf(this, TaskRejectionError.prototype);
    }
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

/**
 * TaskService: gerencia o catálogo de tarefas e atribuições por jogador (in-memory).
 *
 * O catálogo é organizado POR SALA: cada sala tem seu próprio conjunto de tasks.
 * `assignRandomTasks` embaralha o catálogo DA SALA via Fisher-Yates e enfileira
 * todas as tarefas, entregando UMA por vez conforme o jogador conclui.
 * Idempotência silenciosa: segunda solicitação do mesmo jogador retorna [].
 * Sem persistência — estado é resetado ao reiniciar o servidor.
 */
export class TaskService {
    /** Catálogo por sala: roomId → TaskEntry[]. Substituído por registerCatalog. */
    private catalog = new Map<string, TaskEntry[]>();

    /** assignments: playerId → AssignedTask[] */
    private assignments = new Map<string, AssignedTask[]>();

    /**
     * playerId → roomId da atribuição.
     * incrementProgress só recebe playerId, mas o catálogo é POR SALA — este mapa é
     * o que permite localizar a TaskEntry certa para validar items/pairs.
     */
    private assignedRoom = new Map<string, string>();

    /** `${playerId}:${taskId}` → itemIds já contabilizados (deduplicação). */
    private collectedItems = new Map<string, Set<string>>();

    /**
     * playerId → tarefas sorteadas que ainda NÃO foram atribuídas.
     *
     * O jogador recebe uma por vez: `assignRandomTasks` sorteia o lote, entrega a
     * primeira e guarda o resto aqui; `advanceQueue` libera a seguinte quando a atual
     * termina. Evita o jogador encarar várias tarefas simultâneas sem saber por onde começar.
     */
    private taskQueue = new Map<string, TaskEntry[]>();

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    /**
     * Substitui o catálogo de tarefas da sala `roomId` pelas entradas fornecidas.
     * Chamado quando o cliente envia task_catalog_register.
     * As tarefas fornecidas não incluem npcId (campo removido nesta versão).
     */
    public registerCatalog(
        roomId: string,
        tasks: Array<{
            id: string;
            description: string;
            type: string;
            targetCount: number;
            items?: string[];
            pairs?: TaskItemPair[];
        }>,
    ): void {
        const entries: TaskEntry[] = tasks.map(t => ({ ...t, roomId }));
        this.catalog.set(roomId, entries);
        logger.info(`TaskService.registerCatalog: ${tasks.length} tarefas registradas para sala ${roomId}`, { module: 'GAME' });
    }

    /**
     * Embaralha o catálogo DA SALA, atribui a primeira tarefa ao jogador e enfileira o
     * restante (ver `advanceQueue`). Não há teto: a fila acompanha o tamanho do catálogo,
     * então acrescentar uma tarefa nova ao catálogo já passa a valer.
     *
     * Idempotente: se o jogador já tem tasks atribuídas, loga warn e retorna [].
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

        // Sem teto fixo: a fila leva o catálogo INTEIRO da sala, embaralhado. Acrescentar
        // uma tarefa nova ao catálogo passa a valer sozinho, sem tocar no servidor.
        const [primeira, ...resto] = this.fisherYates([...roomCatalog]);
        const assigned = [this.toAssigned(primeira)];

        this.assignments.set(playerId, assigned);
        this.assignedRoom.set(playerId, roomId);
        this.taskQueue.set(playerId, resto);

        logger.info(`TaskService: task '${primeira.id}' atribuída ao jogador '${playerId}' na sala '${roomId}' (${resto.length} na fila)`, { module: 'GAME' });
        return assigned;
    }

    /**
     * Libera a próxima tarefa da fila, se o jogador não tiver nenhuma aberta.
     * Devolve null quando ainda há tarefa em andamento ou quando a fila secou.
     *
     * Chamado pelos handlers logo após uma conclusão, para que o jogador nunca fique
     * sem o que fazer — e nunca com duas coisas ao mesmo tempo.
     */
    public advanceQueue(playerId: string): AssignedTask | null {
        const atribuidas = this.assignments.get(playerId);
        if (!atribuidas) return null;

        const temAberta = atribuidas.some(t => t.status === 'pending' || t.status === 'in_progress');
        if (temAberta) return null;

        const fila = this.taskQueue.get(playerId);
        if (!fila || fila.length === 0) return null;

        const proxima = this.toAssigned(fila.shift()!);
        atribuidas.push(proxima);

        logger.info(`TaskService.advanceQueue: task '${proxima.id}' liberada para '${playerId}' (${fila.length} restantes na fila)`, { module: 'GAME' });
        return proxima;
    }

    /** Converte uma entrada de catálogo na forma atribuída ao jogador. */
    private toAssigned(entry: TaskEntry): AssignedTask {
        return {
            id: entry.id,
            description: entry.description,
            type: entry.type,
            targetCount: entry.targetCount,
            currentProgress: 0,
            status: 'pending',
        };
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
     * Incrementa o progresso de uma task de coleta (ex: 'collect') em +1.
     * Usado por objetos coletáveis (papéis) que somam progresso a cada interação,
     * em vez do fluxo binário start → resolve do QTE.
     *
     * Transições:
     *   - 'pending' → 'in_progress' automaticamente no primeiro incremento
     *     (coletáveis não têm passo separado de task_start_interaction).
     *   - cada chamada faz currentProgress += 1 (clampeado em targetCount).
     *   - ao atingir targetCount → status 'completed'.
     *
     * Quando a entrada de catálogo declara `items` ou `pairs`, o incremento passa a
     * exigir `itemId` e a validar pertencimento, pareamento e não-repetição. Entradas
     * sem esses campos mantêm a contagem cega (retrocompatível).
     *
     * Lança TaskRejectionError (subclasse de ApiError) com o `code` correspondente.
     */
    public incrementProgress(playerId: string, taskId: string, itemId?: string, slotId?: string): AssignedTask {
        const playerTasks = this.assignments.get(playerId);
        if (!playerTasks || playerTasks.length === 0) {
            throw new TaskRejectionError('NOT_ASSIGNED', `Jogador '${playerId}' não tem tasks atribuídas`);
        }
        const task = playerTasks.find(t => t.id === taskId);
        if (!task) {
            throw new TaskRejectionError('NOT_ASSIGNED', `Task '${taskId}' não encontrada para jogador '${playerId}'`);
        }
        if (task.status !== 'pending' && task.status !== 'in_progress') {
            throw new TaskRejectionError('INVALID_STATUS', `Transição inválida: task '${taskId}' está '${task.status}', esperado 'pending' ou 'in_progress'`);
        }

        // Validação de identidade do item — só para entradas que a declaram.
        const entry = this.findEntry(playerId, taskId);
        const collectedKey = `${playerId}:${taskId}`;
        const declaresItems = !!(entry && (entry.items || entry.pairs));

        if (declaresItems) {
            this.validateItem(entry!, collectedKey, itemId, slotId);
        }

        if (itemId) {
            const collected = this.collectedItems.get(collectedKey) ?? new Set<string>();
            collected.add(itemId);
            this.collectedItems.set(collectedKey, collected);
        }

        if (task.status === 'pending') {
            task.status = 'in_progress';
        }

        task.currentProgress += 1;
        if (task.currentProgress >= task.targetCount) {
            task.currentProgress = task.targetCount;
            task.status = 'completed';
        }

        logger.info(`TaskService.incrementProgress: task '${taskId}' do jogador '${playerId}' → ${task.currentProgress}/${task.targetCount} (${task.status})`, { module: 'GAME' });
        return task;
    }

    /**
     * Remove todas as entradas de atribuição dos playerIds fornecidos.
     * Chamado por UdpSocketManager.resetRoomState.
     */
    public clearRoom(playerIds: string[]): void {
        for (const pid of playerIds) {
            const tasks = this.assignments.get(pid) ?? [];
            for (const task of tasks) {
                this.collectedItems.delete(`${pid}:${task.id}`);
            }
            this.assignments.delete(pid);
            this.assignedRoom.delete(pid);
            this.taskQueue.delete(pid);
        }
        logger.info(`TaskService.clearRoom: ${playerIds.length} atribuições removidas`, { module: 'GAME' });
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    /** Localiza a entrada de catálogo da task, usando a sala em que o jogador foi atribuído. */
    private findEntry(playerId: string, taskId: string): TaskEntry | undefined {
        const roomId = this.assignedRoom.get(playerId);
        if (!roomId) return undefined;
        return this.catalog.get(roomId)?.find(e => e.id === taskId);
    }

    /**
     * Valida itemId/slotId contra a entrada de catálogo. Lança TaskRejectionError na
     * primeira violação. Chamado apenas quando a entrada declara `items` ou `pairs`.
     */
    private validateItem(entry: TaskEntry, collectedKey: string, itemId?: string, slotId?: string): void {
        if (!itemId || itemId.trim() === '') {
            throw new TaskRejectionError('MISSING_ITEM', `Task '${entry.id}' exige identificar o item entregue`);
        }

        if (entry.items && !entry.items.includes(itemId)) {
            throw new TaskRejectionError('UNKNOWN_ITEM', `Item '${itemId}' não faz parte da task '${entry.id}'`);
        }

        if (entry.pairs) {
            const pair = entry.pairs.find(p => p.itemId === itemId);
            if (!pair) {
                throw new TaskRejectionError('UNKNOWN_ITEM', `Item '${itemId}' não faz parte da task '${entry.id}'`);
            }
            if (pair.slotId !== slotId) {
                throw new TaskRejectionError('WRONG_SLOT', `Item '${itemId}' não pertence a este lugar`);
            }
        }

        if (this.collectedItems.get(collectedKey)?.has(itemId)) {
            throw new TaskRejectionError('ALREADY_COUNTED', `Item '${itemId}' já foi contabilizado nesta tarefa`);
        }
    }

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
