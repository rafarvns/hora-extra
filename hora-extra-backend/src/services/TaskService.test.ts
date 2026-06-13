import { describe, it, expect, beforeEach } from 'vitest';
import { TaskService } from './TaskService.js';

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

    it('retorna exatamente N tasks ao atribuir', () => {
        const result = service.assignRandomTasks('r1', 'p1');
        expect(result).toHaveLength(3);
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

    it('assignRandomTasks retorna TODAS as tasks se catálogo da sala tem menos que TASK_ASSIGN_COUNT', () => {
        // Registra catálogo com apenas 2 tasks (< 3)
        service.registerCatalog('r1', [
            { id: 'small-1', description: 'Pequena 1', type: 'collect', targetCount: 1 },
            { id: 'small-2', description: 'Pequena 2', type: 'deliver', targetCount: 1 },
        ]);

        const result = service.assignRandomTasks('r1', 'p-small');
        expect(result).toHaveLength(2);
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
        expect(p3Tasks).toHaveLength(3);
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
