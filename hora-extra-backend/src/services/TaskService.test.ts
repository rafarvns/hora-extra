import { describe, it, expect, beforeEach } from 'vitest';
import { TaskService, TaskRejectionError } from './TaskService.js';
import { ApiError } from '../core/ApiError.js';

// ────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────
function makeService(): TaskService {
    return new TaskService();
}

/** Seed a service with 5 tasks for roomId via registerCatalog. */
function seedCatalog(service: TaskService, roomId: string = 'r1'): void {
    service.registerCatalog(roomId, [
        { id: 't1', description: 'Tarefa 1', type: 'collect', targetCount: 3 },
        { id: 't2', description: 'Tarefa 2', type: 'deliver', targetCount: 1 },
        { id: 't3', description: 'Tarefa 3', type: 'collect', targetCount: 2 },
        { id: 't4', description: 'Tarefa 4', type: 'escort',  targetCount: 1 },
        { id: 't5', description: 'Tarefa 5', type: 'collect', targetCount: 5 },
    ]);
}

// ────────────────────────────────────────────────────────────
// TaskService — registerCatalog
// ────────────────────────────────────────────────────────────
describe('TaskService — registerCatalog', () => {
    let service: TaskService;

    beforeEach(() => {
        service = makeService();
    });

    it('registerCatalog armazena tasks sem npcId indexadas por roomId — catálogo de outra sala não vaza', () => {
        // Registra catálogo só para room-A
        service.registerCatalog('room-A', [
            { id: 'task-01', description: 'Tarefa 01', type: 'collect', targetCount: 3 },
        ]);

        // room-B NÃO tem catálogo — deve lançar ApiError (catálogo da sala vazio)
        expect(() => service.assignRandomTasks('room-B', 'p-room-b')).toThrow();

        // room-A ainda funciona normalmente (isolamento)
        const result = service.assignRandomTasks('room-A', 'p-room-a');
        expect(result).toHaveLength(1);
        expect(result[0]).toMatchObject({ id: 'task-01', description: 'Tarefa 01', type: 'collect', targetCount: 3 });
        expect((result[0] as any).npcId).toBeUndefined();
    });

    it('registerCatalog sobrescreve catálogo existente da MESMA sala (não afeta outras salas)', () => {
        service.registerCatalog('room-A', [
            { id: 'old-task', description: 'Antiga', type: 'collect', targetCount: 1 },
        ]);
        service.registerCatalog('room-B', [
            { id: 'room-b-task', description: 'Room B', type: 'deliver', targetCount: 1 },
        ]);
        // Segunda chamada para room-A substitui só o catálogo dela
        service.registerCatalog('room-A', [
            { id: 'new-task-1', description: 'Nova 1', type: 'deliver', targetCount: 2 },
            { id: 'new-task-2', description: 'Nova 2', type: 'repair',  targetCount: 1 },
        ]);

        const resultA = service.assignRandomTasks('room-A', 'p-a');
        const idsA = resultA.map(t => t.id);
        expect(idsA).not.toContain('old-task');
        expect(idsA.every(id => ['new-task-1', 'new-task-2'].includes(id))).toBe(true);

        // room-B continua com seu catálogo intacto
        const resultB = service.assignRandomTasks('room-B', 'p-b');
        expect(resultB.map(t => t.id)).toContain('room-b-task');
    });
});

// ────────────────────────────────────────────────────────────
// TaskService — assignRandomTasks
// ────────────────────────────────────────────────────────────
describe('TaskService', () => {
    let service: TaskService;

    beforeEach(() => {
        service = makeService();
        seedCatalog(service);
    });

    // Antes da fila sequencial este teste esperava 3. O contrato mudou: o sorteio
    // continua de 3, mas a entrega é de uma por vez (ver 'TaskService — fila sequencial').
    it('atribui uma task por vez, mesmo sorteando um lote maior', () => {
        const result = service.assignRandomTasks('r1', 'p1');
        expect(result).toHaveLength(1);
    });

    it('tasks retornadas pertencem ao catálogo da sala', () => {
        const catalogIds = ['t1', 't2', 't3', 't4', 't5'];
        const result = service.assignRandomTasks('r1', 'p1');
        for (const task of result) {
            expect(catalogIds).toContain(task.id);
        }
    });

    it('tasks atribuídas têm status pending e progress 0', () => {
        const result = service.assignRandomTasks('r1', 'p1');
        for (const task of result) {
            expect(task.status).toBe('pending');
            expect(task.currentProgress).toBe(0);
        }
    });

    it('não repete tasks na mesma atribuição', () => {
        const result = service.assignRandomTasks('r1', 'p1');
        const ids = result.map(t => t.id);
        const uniqueIds = new Set(ids);
        expect(uniqueIds.size).toBe(ids.length);
    });

    it('armazena no map e getAssignedTasks retorna os mesmos dados', () => {
        const assigned = service.assignRandomTasks('r1', 'p1');
        const retrieved = service.getAssignedTasks('r1', 'p1');
        expect(retrieved).toEqual(assigned);
    });

    it('entrega a primeira e enfileira o resto do catálogo', () => {
        // Registra catálogo com apenas 2 tasks (< 3)
        service.registerCatalog('r1', [
            { id: 'small-1', description: 'Pequena 1', type: 'collect', targetCount: 1 },
            { id: 'small-2', description: 'Pequena 2', type: 'deliver', targetCount: 1 },
        ]);

        const result = service.assignRandomTasks('r1', 'p-small');
        expect(result).toHaveLength(1);

        // A segunda existe, mas só sai quando a primeira terminar.
        expect(service.advanceQueue('p-small')).toBeNull();
        service.incrementProgress('p-small', result[0].id);
        expect(service.advanceQueue('p-small')).not.toBeNull();
    });

    it('assignRandomTasks lança ApiError se catálogo DA SALA estiver vazio', () => {
        // Sala sem catálogo registrado
        expect(() => service.assignRandomTasks('sala-sem-catalogo', 'p-qualquer')).toThrow();
        // Também deve lançar se catálogo foi registrado mas vazio
        service.registerCatalog('sala-vazia', []);
        expect(() => service.assignRandomTasks('sala-vazia', 'p-qualquer')).toThrow();
    });

    it('segunda chamada de assignRandomTasks é idempotente: retorna [] e não sobrescreve', () => {
        const first = service.assignRandomTasks('r1', 'p1');
        const second = service.assignRandomTasks('r1', 'p1');
        expect(second).toEqual([]);
        // original assignment untouched
        expect(service.getAssignedTasks('r1', 'p1')).toEqual(first);
    });

    it('clearRoom remove entradas dos playerIds fornecidos', () => {
        service.assignRandomTasks('r1', 'p1');
        service.assignRandomTasks('r1', 'p2');
        service.clearRoom(['p1', 'p2']);
        expect(service.getAssignedTasks('r1', 'p1')).toEqual([]);
        expect(service.getAssignedTasks('r1', 'p2')).toEqual([]);
    });

    it('clearRoom não remove entradas de players não listados', () => {
        service.assignRandomTasks('r1', 'p1');
        service.assignRandomTasks('r1', 'p2');
        // p3 tem catálogo separado (simula outra sala)
        seedCatalog(service, 'r2');
        service.assignRandomTasks('r2', 'p3');
        service.clearRoom(['p1', 'p2']);
        const p3Tasks = service.getAssignedTasks('r2', 'p3');
        expect(p3Tasks).toHaveLength(1);           // uma por vez, e a dele não foi limpa
        expect(p3Tasks[0].status).toBe('pending');
    });
});

// ────────────────────────────────────────────────────────────
// TaskService — startTask
// ────────────────────────────────────────────────────────────
describe('TaskService — startTask', () => {
    let service: TaskService;

    beforeEach(() => {
        service = makeService();
        seedCatalog(service);
    });

    it('startTask muda status pending → in_progress e retorna AssignedTask atualizada', () => {
        service.assignRandomTasks('r1', 'p1');
        const tasks = service.getAssignedTasks('r1', 'p1');
        const taskId = tasks[0].id;

        const result = service.startTask('p1', taskId);

        expect(result.status).toBe('in_progress');
        expect(result.id).toBe(taskId);
        // deve atualizar o objeto no map também
        const updated = service.getAssignedTasks('r1', 'p1').find(t => t.id === taskId);
        expect(updated?.status).toBe('in_progress');
    });

    it('startTask lança ApiError se jogador não tem tasks atribuídas', () => {
        // 'p-sem-tasks' nunca chamou assignRandomTasks
        expect(() => service.startTask('p-sem-tasks', 'any-task-id')).toThrow();
    });

    it('startTask lança ApiError se taskId não encontrado no array do jogador', () => {
        service.assignRandomTasks('r1', 'p1');
        expect(() => service.startTask('p1', 'task-inexistente-xyz')).toThrow();
    });

    it('startTask lança ApiError se task não está pending — transição inválida', () => {
        service.assignRandomTasks('r1', 'p1');
        const tasks = service.getAssignedTasks('r1', 'p1');
        const taskId = tasks[0].id;
        // Primeira chamada leva para in_progress
        service.startTask('p1', taskId);
        // Segunda chamada deve lançar (já não é pending)
        expect(() => service.startTask('p1', taskId)).toThrow();
    });
});

// ────────────────────────────────────────────────────────────
// TaskService — resolveTask
// ────────────────────────────────────────────────────────────
describe('TaskService — resolveTask', () => {
    let service: TaskService;

    beforeEach(() => {
        service = makeService();
        seedCatalog(service);
    });

    it('resolveTask com success=true muda in_progress → completed e seta currentProgress=targetCount', () => {
        service.assignRandomTasks('r1', 'p1');
        const tasks = service.getAssignedTasks('r1', 'p1');
        const taskId = tasks[0].id;
        const targetCount = tasks[0].targetCount;
        service.startTask('p1', taskId);

        const result = service.resolveTask('p1', taskId, true);

        expect(result.status).toBe('completed');
        expect(result.currentProgress).toBe(targetCount);
    });

    it('resolveTask com success=false muda in_progress → failed', () => {
        service.assignRandomTasks('r1', 'p1');
        const tasks = service.getAssignedTasks('r1', 'p1');
        const taskId = tasks[0].id;
        service.startTask('p1', taskId);

        const result = service.resolveTask('p1', taskId, false);

        expect(result.status).toBe('failed');
    });

    it('resolveTask lança ApiError se task não está in_progress', () => {
        service.assignRandomTasks('r1', 'p1');
        const tasks = service.getAssignedTasks('r1', 'p1');
        const taskId = tasks[0].id;
        // status=pending → deve lançar (não está in_progress)
        expect(() => service.resolveTask('p1', taskId, true)).toThrow();

        // status=completed → deve lançar
        service.startTask('p1', taskId);
        service.resolveTask('p1', taskId, true); // → completed
        expect(() => service.resolveTask('p1', taskId, true)).toThrow();
    });

    it('resolveTask lança ApiError se taskId não encontrado', () => {
        service.assignRandomTasks('r1', 'p1');
        expect(() => service.resolveTask('p1', 'id-inexistente-999', true)).toThrow();
    });
});

// ────────────────────────────────────────────────────────────
// TaskService — incrementProgress
// ────────────────────────────────────────────────────────────
describe('TaskService — incrementProgress', () => {
    let service: TaskService;

    /** Catálogo com uma única task de coleta de targetCount conhecido. */
    function seedCollectTask(targetCount: number): { taskId: string } {
        service.registerCatalog('r1', [
            { id: 'collect-1', description: 'Colete os documentos', type: 'collect', targetCount },
        ]);
        service.assignRandomTasks('r1', 'p1');
        return { taskId: 'collect-1' };
    }

    beforeEach(() => {
        service = makeService();
    });

    it('primeiro incremento muda pending → in_progress e seta currentProgress=1', () => {
        const { taskId } = seedCollectTask(3);

        const result = service.incrementProgress('p1', taskId);

        expect(result.status).toBe('in_progress');
        expect(result.currentProgress).toBe(1);
    });

    it('incrementos sucessivos somam currentProgress mantendo in_progress até o alvo', () => {
        const { taskId } = seedCollectTask(3);

        service.incrementProgress('p1', taskId); // 1
        const result = service.incrementProgress('p1', taskId); // 2

        expect(result.currentProgress).toBe(2);
        expect(result.status).toBe('in_progress');
    });

    it('ao atingir targetCount muda status → completed e clampeia currentProgress', () => {
        const { taskId } = seedCollectTask(2);

        service.incrementProgress('p1', taskId); // 1
        const result = service.incrementProgress('p1', taskId); // 2 → completed

        expect(result.currentProgress).toBe(2);
        expect(result.status).toBe('completed');
    });

    it('lança ApiError se a task já está completed (transição inválida)', () => {
        const { taskId } = seedCollectTask(1);
        service.incrementProgress('p1', taskId); // 1 → completed

        expect(() => service.incrementProgress('p1', taskId)).toThrow();
    });

    it('lança ApiError se jogador não tem tasks atribuídas', () => {
        expect(() => service.incrementProgress('p-sem-tasks', 'collect-1')).toThrow();
    });

    it('lança ApiError se taskId não encontrado', () => {
        seedCollectTask(3);
        expect(() => service.incrementProgress('p1', 'id-inexistente-999')).toThrow();
    });
});

// ────────────────────────────────────────────────────────────
// TaskService — fila sequencial de tarefas
//
// Regra: o jogador recebe UMA tarefa por vez. As demais ficam numa fila e só são
// atribuídas quando a atual termina. Substitui o comportamento anterior de atribuir
// as 3 de uma vez (plano 0001).
// ────────────────────────────────────────────────────────────
describe('TaskService — fila sequencial', () => {
    let service: TaskService;

    /** Catálogo com `n` tarefas de coleta, cada uma com targetCount 1. */
    function seedMany(n: number): void {
        service.registerCatalog('r1', Array.from({ length: n }, (_, i) => ({
            id: `t${i + 1}`,
            description: `Tarefa ${i + 1}`,
            type: 'collect',
            targetCount: 1,
        })));
    }

    /** Conclui a task aberta do jogador. */
    function concluir(playerId: string): void {
        const aberta = service.getAssignedTasks('r1', playerId)
            .find(t => t.status === 'pending' || t.status === 'in_progress')!;
        service.incrementProgress(playerId, aberta.id);
    }

    beforeEach(() => {
        service = makeService();
    });

    it('assignRandomTasks entrega UMA tarefa, não três', () => {
        seedMany(5);
        const result = service.assignRandomTasks('r1', 'p1');
        expect(result).toHaveLength(1);
        expect(service.getAssignedTasks('r1', 'p1')).toHaveLength(1);
    });

    it('advanceQueue devolve null enquanto a tarefa atual está aberta', () => {
        seedMany(5);
        service.assignRandomTasks('r1', 'p1');
        expect(service.advanceQueue('p1')).toBeNull();
    });

    it('advanceQueue entrega a próxima quando a atual conclui', () => {
        seedMany(5);
        const [primeira] = service.assignRandomTasks('r1', 'p1');
        concluir('p1');

        const proxima = service.advanceQueue('p1');

        expect(proxima).not.toBeNull();
        expect(proxima!.id).not.toBe(primeira.id);
        expect(proxima!.status).toBe('pending');
        expect(proxima!.currentProgress).toBe(0);
    });

    it('a tarefa concluída continua no histórico do jogador', () => {
        seedMany(5);
        const [primeira] = service.assignRandomTasks('r1', 'p1');
        concluir('p1');
        service.advanceQueue('p1');

        const todas = service.getAssignedTasks('r1', 'p1');
        expect(todas).toHaveLength(2);
        expect(todas.find(t => t.id === primeira.id)!.status).toBe('completed');
    });

    it('a fila entrega TODAS as tarefas do catálogo da sala, uma por vez', () => {
        seedMany(10);
        service.assignRandomTasks('r1', 'p1');

        let entregues = 1;
        for (let i = 0; i < 20; i++) {
            concluir('p1');
            const proxima = service.advanceQueue('p1');
            if (proxima === null) break;
            entregues++;
        }

        // Sem teto fixo: a fila acompanha o tamanho do catálogo. Acrescentar uma tarefa
        // nova ao catálogo passa a valer sem mexer no servidor.
        expect(entregues).toBe(10);
        expect(service.advanceQueue('p1')).toBeNull(); // seca e continua seca
    });

    it('nunca repete a mesma tarefa na fila de um jogador', () => {
        seedMany(10);
        const vistos = new Set<string>();
        vistos.add(service.assignRandomTasks('r1', 'p1')[0].id);
        for (let i = 0; i < 9; i++) {
            concluir('p1');
            vistos.add(service.advanceQueue('p1')!.id);
        }
        expect(vistos.size).toBe(10);
    });

    it('catálogo menor que o total: a fila entrega só o que existe', () => {
        seedMany(2);
        service.assignRandomTasks('r1', 'p1');
        concluir('p1');
        expect(service.advanceQueue('p1')).not.toBeNull();
        concluir('p1');
        expect(service.advanceQueue('p1')).toBeNull();
    });

    it('advanceQueue de jogador sem atribuição devolve null em vez de lançar', () => {
        seedMany(5);
        expect(service.advanceQueue('p-fantasma')).toBeNull();
    });

    it('clearRoom limpa a fila — o jogador recomeça do zero', () => {
        seedMany(5);
        service.assignRandomTasks('r1', 'p1');
        concluir('p1');

        service.clearRoom(['p1']);

        expect(service.getAssignedTasks('r1', 'p1')).toEqual([]);
        expect(service.advanceQueue('p1')).toBeNull();
        expect(service.assignRandomTasks('r1', 'p1')).toHaveLength(1);
    });
});

// ────────────────────────────────────────────────────────────
// TaskService — incrementProgress com identidade de item (plano 0004)
//
// O servidor passa a saber QUAL item foi entregue e ONDE. Entradas de catálogo
// que declaram `items` ou `pairs` ganham validação de pertencimento, pareamento
// e deduplicação; entradas sem esses campos mantêm a contagem cega de +1.
// ────────────────────────────────────────────────────────────
describe('TaskService — incrementProgress com items/pairs', () => {
    let service: TaskService;

    /** Catálogo de uma task só, com os campos novos opcionais. */
    function seedItemTask(entry: {
        targetCount: number;
        items?: string[];
        pairs?: Array<{ itemId: string; slotId: string }>;
    }): string {
        service.registerCatalog('r1', [
            {
                id: 'coleta-1',
                description: 'Guarde os itens',
                type: 'collect',
                targetCount: entry.targetCount,
                ...(entry.items ? { items: entry.items } : {}),
                ...(entry.pairs ? { pairs: entry.pairs } : {}),
            },
        ]);
        service.assignRandomTasks('r1', 'p1');
        return 'coleta-1';
    }

    /** Captura o TaskRejectionError lançado, para inspecionar o `code`. */
    function rejectionFrom(fn: () => unknown): TaskRejectionError {
        try {
            fn();
        } catch (err) {
            return err as TaskRejectionError;
        }
        throw new Error('esperava TaskRejectionError, mas nada foi lançado');
    }

    beforeEach(() => {
        service = makeService();
    });

    // Cycle 1
    it('sem items/pairs no catálogo mantém o comportamento atual de +1', () => {
        const taskId = seedItemTask({ targetCount: 3 });

        // Sem itemId nenhum — contagem cega, como antes do plano 0004.
        // incrementProgress devolve o objeto vivo da atribuição, então o valor é
        // copiado a cada passo em vez de guardar a referência.
        const afterFirst = service.incrementProgress('p1', taskId).currentProgress;
        const second = service.incrementProgress('p1', taskId);

        expect(afterFirst).toBe(1);
        expect(second.currentProgress).toBe(2);
        expect(second.status).toBe('in_progress');
    });

    // Cycle 2
    it('aceita itemId declarado em items e soma +1', () => {
        const taskId = seedItemTask({ targetCount: 2, items: ['papel-01', 'papel-02'] });

        const result = service.incrementProgress('p1', taskId, 'papel-01');

        expect(result.currentProgress).toBe(1);
        expect(result.status).toBe('in_progress');
    });

    // Cycle 3
    it('lança MISSING_ITEM quando o catálogo declara items e o payload não manda itemId', () => {
        const taskId = seedItemTask({ targetCount: 2, items: ['papel-01', 'papel-02'] });

        const err = rejectionFrom(() => service.incrementProgress('p1', taskId));

        expect(err.code).toBe('MISSING_ITEM');
        expect(service.getAssignedTasks('r1', 'p1')[0].currentProgress).toBe(0);
    });

    // Cycle 4
    it('lança UNKNOWN_ITEM quando itemId não está em items', () => {
        const taskId = seedItemTask({ targetCount: 2, items: ['papel-01', 'papel-02'] });

        const err = rejectionFrom(() => service.incrementProgress('p1', taskId, 'caneta-99'));

        expect(err.code).toBe('UNKNOWN_ITEM');
        expect(service.getAssignedTasks('r1', 'p1')[0].currentProgress).toBe(0);
    });

    // Cycle 5
    it('lança ALREADY_COUNTED ao repetir o mesmo itemId', () => {
        const taskId = seedItemTask({ targetCount: 4, items: ['papel-01', 'papel-02'] });

        service.incrementProgress('p1', taskId, 'papel-01');
        const err = rejectionFrom(() => service.incrementProgress('p1', taskId, 'papel-01'));

        expect(err.code).toBe('ALREADY_COUNTED');
        expect(service.getAssignedTasks('r1', 'p1')[0].currentProgress).toBe(1);
    });

    // Cycle 6
    it('aceita par (itemId, slotId) declarado em pairs', () => {
        const taskId = seedItemTask({
            targetCount: 2,
            pairs: [
                { itemId: 'caixa-ti', slotId: 'estante-ti' },
                { itemId: 'caixa-rh', slotId: 'estante-rh' },
            ],
        });

        const result = service.incrementProgress('p1', taskId, 'caixa-ti', 'estante-ti');

        expect(result.currentProgress).toBe(1);
    });

    // Cycle 7
    it('lança WRONG_SLOT quando slotId não confere com o par declarado', () => {
        const taskId = seedItemTask({
            targetCount: 2,
            pairs: [
                { itemId: 'caixa-ti', slotId: 'estante-ti' },
                { itemId: 'caixa-rh', slotId: 'estante-rh' },
            ],
        });

        const err = rejectionFrom(() => service.incrementProgress('p1', taskId, 'caixa-ti', 'estante-rh'));

        expect(err.code).toBe('WRONG_SLOT');
        expect(service.getAssignedTasks('r1', 'p1')[0].currentProgress).toBe(0);
    });

    // Cycle 8
    it('lança UNKNOWN_ITEM quando itemId não está em pairs', () => {
        const taskId = seedItemTask({
            targetCount: 2,
            pairs: [{ itemId: 'caixa-ti', slotId: 'estante-ti' }],
        });

        const err = rejectionFrom(() => service.incrementProgress('p1', taskId, 'caixa-intrusa', 'estante-ti'));

        expect(err.code).toBe('UNKNOWN_ITEM');
    });

    // Cycle 9
    it('lança NOT_ASSIGNED quando o jogador não tem a task', () => {
        seedItemTask({ targetCount: 2, items: ['papel-01'] });

        const semTasks = rejectionFrom(() => service.incrementProgress('p-fantasma', 'coleta-1', 'papel-01'));
        expect(semTasks.code).toBe('NOT_ASSIGNED');

        const taskErrada = rejectionFrom(() => service.incrementProgress('p1', 'task-que-nao-existe', 'papel-01'));
        expect(taskErrada.code).toBe('NOT_ASSIGNED');
    });

    // Cycle 10
    it('lança INVALID_STATUS quando a task já está completed', () => {
        const taskId = seedItemTask({ targetCount: 1, items: ['papel-01', 'papel-02'] });
        service.incrementProgress('p1', taskId, 'papel-01'); // → completed

        const err = rejectionFrom(() => service.incrementProgress('p1', taskId, 'papel-02'));

        expect(err.code).toBe('INVALID_STATUS');
    });

    // Cycle 11
    it('TaskRejectionError expõe code e é instanceof ApiError', () => {
        const taskId = seedItemTask({ targetCount: 2, items: ['papel-01'] });

        const err = rejectionFrom(() => service.incrementProgress('p1', taskId, 'intruso'));

        // instanceof ApiError garante que os `catch` antigos continuam funcionando
        expect(err).toBeInstanceOf(ApiError);
        expect(err).toBeInstanceOf(TaskRejectionError);
        expect(err.statusCode).toBe(400);
        expect(typeof err.message).toBe('string');
        expect(err.message.length).toBeGreaterThan(0);
    });

    // Cycle 12
    it('clearRoom limpa assignedRoom e collectedItems — mesmo itemId volta a contar após reset', () => {
        const taskId = seedItemTask({ targetCount: 4, items: ['papel-01', 'papel-02'] });
        service.incrementProgress('p1', taskId, 'papel-01');

        service.clearRoom(['p1']);

        // Re-atribuição após o reset: o mesmo item é aceito de novo
        service.assignRandomTasks('r1', 'p1');
        const result = service.incrementProgress('p1', taskId, 'papel-01');

        expect(result.currentProgress).toBe(1);
    });
});
