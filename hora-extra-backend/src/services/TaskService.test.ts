import { describe, it, expect, beforeEach } from 'vitest';
import { TaskService, TaskRejectionError } from './TaskService.js';
import { ApiError } from '../core/ApiError.js';

// ────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────

const CATALOGO = [
    { id: 't1', description: 'Tarefa 1', type: 'collect', targetCount: 3 },
    { id: 't2', description: 'Tarefa 2', type: 'deliver', targetCount: 1 },
    { id: 't3', description: 'Tarefa 3', type: 'collect', targetCount: 2 },
    { id: 't4', description: 'Tarefa 4', type: 'escort',  targetCount: 1 },
    { id: 't5', description: 'Tarefa 5', type: 'collect', targetCount: 5 },
];

function seedCatalog(service: TaskService, roomId: string = 'r1'): void {
    service.registerCatalog(roomId, CATALOGO);
}

/** Conclui a tarefa aberta do jogador somando progresso até o alvo. */
function concluir(service: TaskService, playerId: string, taskId: string): void {
    const total = service.getAssignedTasks('', playerId).find(t => t.id === taskId)!.targetCount;
    for (let i = 0; i < total; i++) service.incrementProgress(playerId, taskId);
}

// ────────────────────────────────────────────────────────────
// Catálogo
// ────────────────────────────────────────────────────────────
describe('TaskService — registerCatalog', () => {
    let service: TaskService;
    beforeEach(() => { service = new TaskService(); });

    it('registra o catálogo da sala e devolve true na primeira vez', () => {
        expect(service.registerCatalog('r1', CATALOGO)).toBe(true);
        expect(service.getRoomSnapshot('r1').map(t => t.id)).toEqual(['t1', 't2', 't3', 't4', 't5']);
    });

    it('segundo registro na MESMA sala é ignorado — não zera o progresso de quem já joga', () => {
        seedCatalog(service);
        const [t] = service.assignRandomTasks('r1', 'A');
        service.incrementProgress('A', t.id);

        expect(service.registerCatalog('r1', [{ id: 'x', description: 'X', type: 'collect', targetCount: 1 }])).toBe(false);
        const snap = service.getRoomSnapshot('r1');
        expect(snap.map(s => s.id)).toEqual(['t1', 't2', 't3', 't4', 't5']);
        expect(snap.find(s => s.id === t.id)!.p).toBe(1);
    });

    it('catálogo de outra sala não vaza', () => {
        seedCatalog(service, 'r1');
        service.registerCatalog('r2', [{ id: 'z', description: 'Z', type: 'collect', targetCount: 1 }]);
        expect(service.getRoomSnapshot('r2').map(t => t.id)).toEqual(['z']);
    });

    it('snapshot de sala inexistente é vazio', () => {
        expect(service.getRoomSnapshot('nada')).toEqual([]);
    });
});

// ────────────────────────────────────────────────────────────
// Atribuição compartilhada
// ────────────────────────────────────────────────────────────
describe('TaskService — tarefas compartilhadas da sala', () => {
    let service: TaskService;
    beforeEach(() => { service = new TaskService(); seedCatalog(service); });

    it('entrega UMA tarefa por vez, pending com progresso 0', () => {
        const tasks = service.assignRandomTasks('r1', 'A');
        expect(tasks).toHaveLength(1);
        expect(tasks[0]).toMatchObject({ status: 'pending', currentProgress: 0, ci: [] });
    });

    it('a 1ª entrada do catálogo sai primeiro', () => {
        expect(service.assignRandomTasks('r1', 'A')[0].id).toBe('t1');
    });

    it('dois jogadores nunca recebem a mesma tarefa', () => {
        const [a] = service.assignRandomTasks('r1', 'A');
        const [b] = service.assignRandomTasks('r1', 'B');
        expect(a.id).not.toBe(b.id);
    });

    it('lança ApiError se o catálogo da sala estiver vazio ou não existir', () => {
        expect(() => service.assignRandomTasks('sem-catalogo', 'A')).toThrow(ApiError);
    });

    it('é idempotente: com tarefa aberta devolve []', () => {
        service.assignRandomTasks('r1', 'A');
        expect(service.assignRandomTasks('r1', 'A')).toEqual([]);
    });

    it('advanceQueue devolve null enquanto a tarefa atual está aberta', () => {
        service.assignRandomTasks('r1', 'A');
        expect(service.advanceQueue('A')).toBeNull();
    });

    it('advanceQueue entrega outra tarefa livre quando a atual conclui', () => {
        const [t] = service.assignRandomTasks('r1', 'A');
        concluir(service, 'A', t.id);
        const proxima = service.advanceQueue('A');
        expect(proxima).not.toBeNull();
        expect(proxima!.id).not.toBe(t.id);
    });

    it('cada tarefa é feita uma vez pela sala: os dois juntos esgotam o catálogo sem repetir', () => {
        const feitas: string[] = [];
        let [a] = service.assignRandomTasks('r1', 'A');
        let [b] = service.assignRandomTasks('r1', 'B');
        let atualA: any = a, atualB: any = b;
        while (atualA || atualB) {
            if (atualA) { concluir(service, 'A', atualA.id); feitas.push(atualA.id); atualA = service.advanceQueue('A'); }
            if (atualB) { concluir(service, 'B', atualB.id); feitas.push(atualB.id); atualB = service.advanceQueue('B'); }
        }
        expect([...feitas].sort()).toEqual(['t1', 't2', 't3', 't4', 't5']);
    });

    it('quem entra no meio só recebe tarefas ainda não feitas e sem dono', () => {
        const [a] = service.assignRandomTasks('r1', 'A');
        concluir(service, 'A', a.id);
        const a2 = service.advanceQueue('A')!;

        const [b] = service.assignRandomTasks('r1', 'B');
        expect([a.id, a2.id]).not.toContain(b.id);
    });

    it('sem tarefa livre devolve [] / null', () => {
        service = new TaskService();
        service.registerCatalog('r1', [{ id: 'unica', description: 'U', type: 'collect', targetCount: 1 }]);
        service.assignRandomTasks('r1', 'A');
        expect(service.assignRandomTasks('r1', 'B')).toEqual([]);
        expect(service.advanceQueue('B')).toBeNull();
    });

    it('failed encerra a tarefa (não volta ao pool)', () => {
        service = new TaskService();
        service.registerCatalog('r1', [{ id: 'qte', description: 'Q', type: 'coffee', targetCount: 1 }]);
        service.assignRandomTasks('r1', 'A');
        service.startTask('A', 'qte');
        service.resolveTask('A', 'qte', false);
        expect(service.assignRandomTasks('r1', 'B')).toEqual([]);
    });

    it('getTotalTaskCount = tamanho do catálogo da sala; 0 sem sala', () => {
        expect(service.getTotalTaskCount('A')).toBe(0);
        service.assignRandomTasks('r1', 'A');
        expect(service.getTotalTaskCount('A')).toBe(5);
    });

    it('getAssignedTasks traz a aberta e as encerradas pelo jogador', () => {
        const [t] = service.assignRandomTasks('r1', 'A');
        concluir(service, 'A', t.id);
        const t2 = service.advanceQueue('A')!;
        expect(service.getAssignedTasks('r1', 'A').map(x => x.id).sort()).toEqual([t.id, t2.id].sort());
    });
});

// ────────────────────────────────────────────────────────────
// Saída do jogador
// ────────────────────────────────────────────────────────────
describe('TaskService — releasePlayer', () => {
    let service: TaskService;
    beforeEach(() => { service = new TaskService(); seedCatalog(service); });

    it('a tarefa aberta volta ao pool com o progresso que tinha', () => {
        const [t] = service.assignRandomTasks('r1', 'A'); // t1, alvo 3
        service.incrementProgress('A', t.id);

        expect(service.releasePlayer('A')).toEqual([t.id]);
        const snap = service.getRoomSnapshot('r1').find(s => s.id === t.id)!;
        expect(snap).toMatchObject({ h: null, p: 1, s: 'in_progress' });

        const [b] = service.assignRandomTasks('r1', 'B');
        expect(b).toMatchObject({ id: t.id, currentProgress: 1 });
    });

    it('tarefa aberta sem progresso volta a pending (QTE recomeça com start)', () => {
        service = new TaskService();
        service.registerCatalog('r1', [{ id: 'email', description: 'E', type: 'send_email_upload', targetCount: 1 }]);
        service.assignRandomTasks('r1', 'A');
        service.startTask('A', 'email');
        service.releasePlayer('A');

        service.assignRandomTasks('r1', 'B');
        expect(() => service.startTask('B', 'email')).not.toThrow();
    });

    it('tarefas concluídas continuam concluídas', () => {
        const [t] = service.assignRandomTasks('r1', 'A');
        concluir(service, 'A', t.id);
        service.releasePlayer('A');
        expect(service.getRoomSnapshot('r1').find(s => s.id === t.id)!.s).toBe('completed');
    });

    it('quem saiu não consegue mais mexer na tarefa', () => {
        const [t] = service.assignRandomTasks('r1', 'A');
        service.releasePlayer('A');
        expect(() => service.incrementProgress('A', t.id)).toThrow(TaskRejectionError);
    });

    it('jogador desconhecido não devolve nada', () => {
        expect(service.releasePlayer('ninguem')).toEqual([]);
    });

    it('hasOpenTask acompanha atribuição e saída', () => {
        expect(service.hasOpenTask('A')).toBe(false);
        service.assignRandomTasks('r1', 'A');
        expect(service.hasOpenTask('A')).toBe(true);
        service.releasePlayer('A');
        expect(service.hasOpenTask('A')).toBe(false);
    });

    it('clearRoom descarta o estado da sala inteira', () => {
        service.assignRandomTasks('r1', 'A');
        service.clearRoom('r1');
        expect(service.getRoomSnapshot('r1')).toEqual([]);
        expect(service.hasOpenTask('A')).toBe(false);
        expect(service.registerCatalog('r1', CATALOGO)).toBe(true);
    });
});

// ────────────────────────────────────────────────────────────
// Dono da tarefa
// ────────────────────────────────────────────────────────────
describe('TaskService — só o dono mexe na tarefa', () => {
    let service: TaskService;
    beforeEach(() => { service = new TaskService(); seedCatalog(service); });

    it('incrementProgress de outro jogador → NOT_ASSIGNED', () => {
        const [t] = service.assignRandomTasks('r1', 'A');
        service.assignRandomTasks('r1', 'B');
        try {
            service.incrementProgress('B', t.id);
            expect.unreachable();
        } catch (e: any) {
            expect(e).toBeInstanceOf(TaskRejectionError);
            expect(e.code).toBe('NOT_ASSIGNED');
        }
    });

    it('startTask/resolveTask de outro jogador lançam ApiError', () => {
        const [t] = service.assignRandomTasks('r1', 'A');
        service.assignRandomTasks('r1', 'B');
        expect(() => service.startTask('B', t.id)).toThrow(ApiError);
        service.startTask('A', t.id);
        expect(() => service.resolveTask('B', t.id, true)).toThrow(ApiError);
    });

    it('jogador sem sala → NOT_ASSIGNED', () => {
        expect(() => service.incrementProgress('fantasma', 't1')).toThrow(TaskRejectionError);
        expect(() => service.startTask('fantasma', 't1')).toThrow(ApiError);
    });
});

// ────────────────────────────────────────────────────────────
// start / resolve
// ────────────────────────────────────────────────────────────
describe('TaskService — startTask / resolveTask', () => {
    let service: TaskService;
    beforeEach(() => { service = new TaskService(); seedCatalog(service); service.assignRandomTasks('r1', 'A'); });

    it('startTask: pending → in_progress', () => {
        expect(service.startTask('A', 't1').status).toBe('in_progress');
    });

    it('startTask fora de pending lança', () => {
        service.startTask('A', 't1');
        expect(() => service.startTask('A', 't1')).toThrow(ApiError);
    });

    it('resolveTask(true): completed com progresso cheio', () => {
        service.startTask('A', 't1');
        expect(service.resolveTask('A', 't1', true)).toMatchObject({ status: 'completed', currentProgress: 3 });
    });

    it('resolveTask(false): failed', () => {
        service.startTask('A', 't1');
        expect(service.resolveTask('A', 't1', false).status).toBe('failed');
    });

    it('resolveTask fora de in_progress lança', () => {
        expect(() => service.resolveTask('A', 't1', true)).toThrow(ApiError);
    });
});

// ────────────────────────────────────────────────────────────
// incrementProgress
// ────────────────────────────────────────────────────────────
describe('TaskService — incrementProgress', () => {
    let service: TaskService;
    beforeEach(() => { service = new TaskService(); seedCatalog(service); service.assignRandomTasks('r1', 'A'); });

    it('primeiro incremento: pending → in_progress, progresso 1', () => {
        expect(service.incrementProgress('A', 't1')).toMatchObject({ status: 'in_progress', currentProgress: 1 });
    });

    it('ao atingir o alvo: completed', () => {
        service.incrementProgress('A', 't1');
        service.incrementProgress('A', 't1');
        expect(service.incrementProgress('A', 't1')).toMatchObject({ status: 'completed', currentProgress: 3 });
    });

    it('task encerrada → INVALID_STATUS', () => {
        concluir(service, 'A', 't1');
        expect(() => service.incrementProgress('A', 't1')).toThrow(TaskRejectionError);
    });
});

describe('TaskService — incrementProgress com items/pairs', () => {
    let service: TaskService;

    beforeEach(() => {
        service = new TaskService();
        service.registerCatalog('r1', [
            { id: 'itens', description: 'I', type: 'collect', targetCount: 2, items: ['a', 'b'] },
            { id: 'pares', description: 'P', type: 'collect', targetCount: 2, pairs: [{ itemId: 'x', slotId: 's1' }, { itemId: 'y', slotId: 's2' }] },
        ]);
    });

    const codigo = (fn: () => void) => { try { fn(); return null; } catch (e: any) { return e.code; } };

    it('aceita item declarado e devolve os itens contados em ci', () => {
        service.assignRandomTasks('r1', 'A');
        expect(service.incrementProgress('A', 'itens', 'a')).toMatchObject({ currentProgress: 1, ci: ['a'] });
    });

    it('MISSING_ITEM / UNKNOWN_ITEM / ALREADY_COUNTED', () => {
        service.assignRandomTasks('r1', 'A');
        expect(codigo(() => service.incrementProgress('A', 'itens'))).toBe('MISSING_ITEM');
        expect(codigo(() => service.incrementProgress('A', 'itens', 'z'))).toBe('UNKNOWN_ITEM');
        service.incrementProgress('A', 'itens', 'a');
        expect(codigo(() => service.incrementProgress('A', 'itens', 'a'))).toBe('ALREADY_COUNTED');
    });

    it('item contado por quem saiu continua contado para o próximo dono', () => {
        service.assignRandomTasks('r1', 'A');
        service.incrementProgress('A', 'itens', 'a');
        service.releasePlayer('A');
        service.assignRandomTasks('r1', 'B');
        expect(codigo(() => service.incrementProgress('B', 'itens', 'a'))).toBe('ALREADY_COUNTED');
        expect(service.incrementProgress('B', 'itens', 'b').status).toBe('completed');
    });

    it('pairs: aceita par certo, WRONG_SLOT no destino errado, UNKNOWN_ITEM fora dos pares', () => {
        service.assignRandomTasks('r1', 'A');
        concluir2(service);
        service.advanceQueue('A');
        expect(codigo(() => service.incrementProgress('A', 'pares', 'x', 's2'))).toBe('WRONG_SLOT');
        expect(codigo(() => service.incrementProgress('A', 'pares', 'q', 's1'))).toBe('UNKNOWN_ITEM');
        expect(service.incrementProgress('A', 'pares', 'x', 's1').currentProgress).toBe(1);
    });

    function concluir2(s: TaskService) {
        s.incrementProgress('A', 'itens', 'a');
        s.incrementProgress('A', 'itens', 'b');
    }

    it('TaskRejectionError expõe code e é instanceof ApiError', () => {
        const err = new TaskRejectionError('WRONG_SLOT', 'm');
        expect(err).toBeInstanceOf(ApiError);
        expect(err.code).toBe('WRONG_SLOT');
    });
});

// ────────────────────────────────────────────────────────────
// Tarefas do Financeiro (contrato usado pelo cliente)
// ────────────────────────────────────────────────────────────
describe('TaskService — tarefas do Financeiro', () => {
    let service: TaskService;

    beforeEach(() => {
        service = new TaskService();
        service.registerCatalog('r1', [
            { id: 'task-cofres-01', description: 'Cofres', type: 'open_safe_collect_money', targetCount: 2, items: ['cofre-direito', 'cofre-esquerdo'] },
            { id: 'task-enviar-email-01', description: 'E-mail', type: 'send_email_upload', targetCount: 1 },
        ]);
    });

    it('cofres: conta cada cofre uma vez e completa com os dois', () => {
        service.assignRandomTasks('r1', 'A');
        expect(service.incrementProgress('A', 'task-cofres-01', 'cofre-direito', 'cofre').status).toBe('in_progress');
        expect(() => service.incrementProgress('A', 'task-cofres-01', 'cofre-direito', 'cofre')).toThrow(TaskRejectionError);
        expect(service.incrementProgress('A', 'task-cofres-01', 'cofre-esquerdo', 'cofre'))
            .toMatchObject({ status: 'completed', ci: ['cofre-direito', 'cofre-esquerdo'] });
    });

    it('e-mail: start → complete_attempt(true) conclui a tarefa', () => {
        service.assignRandomTasks('r1', 'A');
        service.assignRandomTasks('r1', 'B'); // B pega o e-mail
        service.startTask('B', 'task-enviar-email-01');
        expect(service.resolveTask('B', 'task-enviar-email-01', true)).toMatchObject({ status: 'completed', currentProgress: 1 });
    });
});
