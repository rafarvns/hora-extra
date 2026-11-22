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
    | 'NOT_ASSIGNED'      // jogador não é o dono da task
    | 'INVALID_STATUS'    // task já completed/failed
    | 'MISSING_ITEM'      // catálogo exige itemId e o payload não mandou
    | 'UNKNOWN_ITEM'      // itemId não pertence a esta task
    | 'WRONG_SLOT'        // par (itemId, slotId) não confere
    | 'ALREADY_COUNTED';  // itemId já contabilizado nesta task

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

export type TaskStatus = 'pending' | 'in_progress' | 'completed' | 'failed';

/** Tarefa como o jogador a enxerga (payload de task_assigned). */
export interface AssignedTask {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    currentProgress: number;
    status: TaskStatus;
    /** Itens já contados nesta tarefa (nível sala). Usado pelo cliente para o mundo refletir. */
    ci: string[];
}

/** Linha do retrato da sala (evento room_state, chaves compactas). */
export interface RoomTaskSnapshot {
    id: string;
    s: TaskStatus;
    p: number;
    ci: string[];
    /** Dono atual (tarefa aberta) ou quem concluiu (tarefa encerrada). null = livre. */
    h: string | null;
}

/** Estado de uma tarefa NA SALA — único lugar onde progresso e itens moram. */
interface RoomTaskState {
    entry: TaskEntry;
    status: TaskStatus;
    currentProgress: number;
    collected: Set<string>;
    /** Quem está com a tarefa (aberta) ou quem a encerrou. null = livre no pool. */
    holder: string | null;
}

interface RoomTasks {
    catalog: TaskEntry[];
    tasks: Map<string, RoomTaskState>;
}

const isOpen = (s: TaskStatus) => s === 'pending' || s === 'in_progress';

/**
 * TaskService: tarefas COMPARTILHADAS da sala (in-memory).
 *
 * Cada sala tem um catálogo e um estado por tarefa. Cada tarefa é feita UMA vez pela
 * sala: o jogador recebe uma tarefa por vez entre as que ninguém fez nem está fazendo
 * (`assignRandomTasks` / `advanceQueue`). Progresso e itens contados ficam na sala, então
 * se o dono sai (`releasePlayer`) a tarefa volta ao pool com o progresso que tinha, e
 * quem entra no meio da partida só recebe o que sobrou.
 *
 * A primeira entrada do catálogo (ordem de autoria na cena) tem prioridade enquanto
 * estiver livre — permite fixar uma tarefa "sempre primeiro" durante o desenvolvimento.
 * Sem persistência — estado é resetado ao reiniciar o servidor ou quando a sala morre.
 */
export class TaskService {
    private rooms = new Map<string, RoomTasks>();

    /** playerId → sala em que o jogador pega tarefas. */
    private playerRoom = new Map<string, string>();

    // ──────────────────────────────────────────────
    // Catálogo
    // ──────────────────────────────────────────────

    /**
     * Registra o catálogo da sala. Só vale na PRIMEIRA vez: todo cliente manda o
     * catálogo ao conectar, e o segundo jogador não pode zerar o progresso do primeiro.
     * Devolve `true` quando registrou agora.
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
    ): boolean {
        const existente = this.rooms.get(roomId);
        if (existente && existente.catalog.length > 0) {
            logger.info(`TaskService.registerCatalog: sala ${roomId} já tem catálogo (${existente.catalog.length}) — ignorado`, { module: 'GAME' });
            return false;
        }

        const catalog: TaskEntry[] = tasks.map(t => ({ ...t, roomId }));
        const estado = new Map<string, RoomTaskState>();
        for (const entry of catalog) {
            estado.set(entry.id, {
                entry,
                status: 'pending',
                currentProgress: 0,
                collected: new Set<string>(),
                holder: null,
            });
        }
        this.rooms.set(roomId, { catalog, tasks: estado });
        logger.info(`TaskService.registerCatalog: ${tasks.length} tarefas registradas para sala ${roomId}`, { module: 'GAME' });
        return true;
    }

    // ──────────────────────────────────────────────
    // Atribuição
    // ──────────────────────────────────────────────

    /**
     * Primeira atribuição do jogador na sala: entrega UMA tarefa livre.
     *
     * Idempotente: se o jogador já está com uma tarefa aberta, devolve [] (o cliente
     * recupera a dele pelo room_state). Também devolve [] quando não sobrou tarefa livre.
     * Lança ApiError se o catálogo da sala estiver vazio ou não existir.
     */
    public assignRandomTasks(roomId: string, playerId: string): AssignedTask[] {
        const room = this.rooms.get(roomId);
        if (!room || room.catalog.length === 0) {
            throw ApiError.badRequest(`Catálogo da sala '${roomId}' está vazio — não é possível atribuir tasks`);
        }

        this.playerRoom.set(playerId, roomId);

        if (this.hasOpenTask(playerId)) {
            logger.warn(`TaskService.assignRandomTasks: jogador '${playerId}' já tem tarefa aberta — ignorando`, { module: 'GAME' });
            return [];
        }

        const proxima = this.takeNext(room, playerId);
        return proxima ? [proxima] : [];
    }

    /**
     * Entrega a próxima tarefa livre se o jogador não tiver nenhuma aberta.
     * Devolve null quando ainda há tarefa em andamento ou quando não sobrou nenhuma livre.
     */
    public advanceQueue(playerId: string): AssignedTask | null {
        const room = this.roomOf(playerId);
        if (!room) return null;
        if (this.hasOpenTask(playerId)) return null;
        return this.takeNext(room, playerId);
    }

    /** Escolhe a tarefa livre (1ª do catálogo tem prioridade, o resto é sorteado). */
    private takeNext(room: RoomTasks, playerId: string): AssignedTask | null {
        const livres = room.catalog
            .map(e => room.tasks.get(e.id)!)
            .filter(t => isOpen(t.status) && t.holder === null);

        if (livres.length === 0) {
            logger.info(`TaskService: nenhuma tarefa livre para '${playerId}'`, { module: 'GAME' });
            return null;
        }

        const primeira = room.tasks.get(room.catalog[0].id)!;
        const escolhida = livres.includes(primeira)
            ? primeira
            : livres[Math.floor(Math.random() * livres.length)];

        escolhida.holder = playerId;
        logger.info(`TaskService: task '${escolhida.entry.id}' atribuída a '${playerId}' (${livres.length - 1} livres)`, { module: 'GAME' });
        return this.toAssigned(escolhida);
    }

    /** O jogador está com alguma tarefa aberta? */
    public hasOpenTask(playerId: string): boolean {
        const room = this.roomOf(playerId);
        if (!room) return false;
        for (const t of room.tasks.values()) {
            if (t.holder === playerId && isOpen(t.status)) return true;
        }
        return false;
    }

    /**
     * Devolve ao pool as tarefas abertas do jogador (ele saiu da sala). O progresso e os
     * itens contados continuam — o próximo dono continua de onde parou. Tarefa aberta sem
     * progresso volta a 'pending' (o QTE do próximo dono começa com task_start_interaction).
     * Retorna os ids devolvidos.
     */
    public releasePlayer(playerId: string): string[] {
        const room = this.roomOf(playerId);
        this.playerRoom.delete(playerId);
        if (!room) return [];

        const devolvidas: string[] = [];
        for (const t of room.tasks.values()) {
            if (t.holder !== playerId || !isOpen(t.status)) continue;
            t.holder = null;
            if (t.currentProgress === 0) t.status = 'pending';
            devolvidas.push(t.entry.id);
        }
        if (devolvidas.length > 0) {
            logger.info(`TaskService.releasePlayer: '${playerId}' saiu — devolvidas ao pool: ${devolvidas.join(', ')}`, { module: 'GAME' });
        }
        return devolvidas;
    }

    /**
     * Tarefas que pertencem ao jogador: a aberta e as que ele encerrou.
     */
    public getAssignedTasks(_roomId: string, playerId: string): AssignedTask[] {
        const room = this.roomOf(playerId);
        if (!room) return [];
        return [...room.tasks.values()].filter(t => t.holder === playerId).map(t => this.toAssigned(t));
    }

    /** Tamanho do catálogo da sala do jogador (o "N" de "Tarefa X de N" da equipe). */
    public getTotalTaskCount(playerId: string): number {
        return this.roomOf(playerId)?.catalog.length ?? 0;
    }

    /** Retrato das tarefas da sala para quem acabou de entrar (room_state). */
    public getRoomSnapshot(roomId: string): RoomTaskSnapshot[] {
        const room = this.rooms.get(roomId);
        if (!room) return [];
        return room.catalog.map(e => {
            const t = room.tasks.get(e.id)!;
            return { id: e.id, s: t.status, p: t.currentProgress, ci: [...t.collected], h: t.holder };
        });
    }

    // ──────────────────────────────────────────────
    // Progresso
    // ──────────────────────────────────────────────

    /**
     * Muda o status de uma task de 'pending' para 'in_progress'.
     * Lança ApiError se: jogador não é o dono, taskId não encontrado, status != pending.
     */
    public startTask(playerId: string, taskId: string): AssignedTask {
        const task = this.ownedTask(playerId, taskId, msg => ApiError.badRequest(msg));
        if (task.status !== 'pending') {
            throw ApiError.badRequest(`Transição inválida: task '${taskId}' está '${task.status}', esperado 'pending'`);
        }
        task.status = 'in_progress';
        logger.info(`TaskService.startTask: task '${taskId}' iniciada pelo jogador '${playerId}'`, { module: 'GAME' });
        return this.toAssigned(task);
    }

    /**
     * Resolve uma task em andamento. success=true → 'completed' (seta currentProgress=targetCount).
     * success=false → 'failed'. Lança ApiError se status != 'in_progress' ou taskId inválido.
     */
    public resolveTask(playerId: string, taskId: string, success: boolean): AssignedTask {
        const task = this.ownedTask(playerId, taskId, msg => ApiError.badRequest(msg));
        if (task.status !== 'in_progress') {
            throw ApiError.badRequest(`Transição inválida: task '${taskId}' está '${task.status}', esperado 'in_progress'`);
        }
        if (success) {
            task.status = 'completed';
            task.currentProgress = task.entry.targetCount;
        } else {
            task.status = 'failed';
        }
        logger.info(`TaskService.resolveTask: task '${taskId}' do jogador '${playerId}' → '${task.status}'`, { module: 'GAME' });
        return this.toAssigned(task);
    }

    /**
     * Incrementa o progresso de uma task incremental em +1.
     *
     * Transições:
     *   - 'pending' → 'in_progress' automaticamente no primeiro incremento;
     *   - ao atingir targetCount → 'completed'.
     *
     * Quando a entrada de catálogo declara `items` ou `pairs`, o incremento passa a
     * exigir `itemId` e a validar pertencimento, pareamento e não-repetição (na sala).
     * Lança TaskRejectionError (subclasse de ApiError) com o `code` correspondente.
     */
    public incrementProgress(playerId: string, taskId: string, itemId?: string, slotId?: string): AssignedTask {
        const task = this.ownedTask(playerId, taskId, msg => new TaskRejectionError('NOT_ASSIGNED', msg));
        if (!isOpen(task.status)) {
            throw new TaskRejectionError('INVALID_STATUS', `Transição inválida: task '${taskId}' está '${task.status}', esperado 'pending' ou 'in_progress'`);
        }

        if (task.entry.items || task.entry.pairs) {
            this.validateItem(task, itemId, slotId);
        }
        if (itemId) task.collected.add(itemId);

        if (task.status === 'pending') task.status = 'in_progress';

        task.currentProgress += 1;
        if (task.currentProgress >= task.entry.targetCount) {
            task.currentProgress = task.entry.targetCount;
            task.status = 'completed';
        }

        logger.info(`TaskService.incrementProgress: task '${taskId}' do jogador '${playerId}' → ${task.currentProgress}/${task.entry.targetCount} (${task.status})`, { module: 'GAME' });
        return this.toAssigned(task);
    }

    /** Descarta todo o estado de tarefas da sala (sala morreu). */
    public clearRoom(roomId: string): void {
        this.rooms.delete(roomId);
        for (const [pid, rid] of [...this.playerRoom.entries()]) {
            if (rid === roomId) this.playerRoom.delete(pid);
        }
        logger.info(`TaskService.clearRoom: estado de tarefas da sala '${roomId}' descartado`, { module: 'GAME' });
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    private roomOf(playerId: string): RoomTasks | undefined {
        const roomId = this.playerRoom.get(playerId);
        return roomId ? this.rooms.get(roomId) : undefined;
    }

    /** Localiza a task e garante que o jogador é o dono. */
    private ownedTask(playerId: string, taskId: string, erro: (msg: string) => Error): RoomTaskState {
        const room = this.roomOf(playerId);
        if (!room) throw erro(`Jogador '${playerId}' não tem tasks atribuídas`);
        const task = room.tasks.get(taskId);
        if (!task || task.holder !== playerId) {
            throw erro(`Task '${taskId}' não encontrada para jogador '${playerId}'`);
        }
        return task;
    }

    private toAssigned(t: RoomTaskState): AssignedTask {
        return {
            id: t.entry.id,
            description: t.entry.description,
            type: t.entry.type,
            targetCount: t.entry.targetCount,
            currentProgress: t.currentProgress,
            status: t.status,
            ci: [...t.collected],
        };
    }

    /**
     * Valida itemId/slotId contra a entrada de catálogo. Lança TaskRejectionError na
     * primeira violação. Chamado apenas quando a entrada declara `items` ou `pairs`.
     */
    private validateItem(task: RoomTaskState, itemId?: string, slotId?: string): void {
        const entry = task.entry;
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

        if (task.collected.has(itemId)) {
            throw new TaskRejectionError('ALREADY_COUNTED', `Item '${itemId}' já foi contabilizado nesta tarefa`);
        }
    }
}

export default new TaskService();
