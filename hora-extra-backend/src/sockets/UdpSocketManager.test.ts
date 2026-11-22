import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Socket mockado + serviços reais trocados a cada teste (via ServiceFactory mockada).
const state = vi.hoisted(() => ({
    capturedMessageHandler: null as ((msg: Buffer, rinfo: any) => Promise<void> | void) | null,
    send: null as any,
    registry: null as any,
    tasks: null as any,
    clock: null as any,
}));

vi.mock('dgram', () => {
    const mockSocket = {
        on: vi.fn((event: string, handler: any) => {
            if (event === 'message') state.capturedMessageHandler = handler;
            if (event === 'listening') handler();
        }),
        bind: vi.fn(),
        send: (state.send = vi.fn()),
        close: vi.fn(),
        address: vi.fn().mockReturnValue({ address: '0.0.0.0', port: 5001 }),
    };
    return {
        default: { createSocket: vi.fn().mockReturnValue(mockSocket) },
        createSocket: vi.fn().mockReturnValue(mockSocket),
    };
});

vi.mock('../services/authService.js', () => ({
    default: { verifyToken: vi.fn(), generateToken: vi.fn() },
}));

vi.mock('../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getRoomRegistry: () => state.registry,
        getTaskService: () => state.tasks,
        getShiftClockService: () => state.clock,
    },
}));

vi.mock('../utils/Logger.js', () => ({
    default: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));

import authService from '../services/authService.js';
import { RoomRegistry, EMPTY_ROOM_TTL_MS } from '../services/RoomRegistry.js';
import { TaskService } from '../services/TaskService.js';
import { NpcRegisterHandler } from './handlers/NpcRegister.Handler.js';
import { UdpSocketManager } from './UdpSocketManager.js';

// ────────────────────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────────────────────

const rinfoOf = (port: number, address = '10.0.0.1') => ({ address, port, family: 'IPv4', size: 0 }) as any;

/** Tokens do teste: "tok:<id>:<nome>" → claims. */
function mockTokens() {
    (authService.verifyToken as any).mockImplementation((token: string) => {
        const [prefix, id, n] = token.split(':');
        return prefix === 'tok' ? { id, n } : null;
    });
}

async function send(port: number, e: string, d: any = {}, token?: string) {
    const packet = JSON.stringify({ e, d, token });
    await state.capturedMessageHandler!(Buffer.from(packet), rinfoOf(port));
}

const conn = (port: number, id: string, nome: string, roomId: string) =>
    send(port, 'CONN', { roomId }, `tok:${id}:${nome}`);

/** Pacotes enviados pelo socket mockado: { e, d, port }. */
function sent(eventName?: string, port?: number): Array<{ e: string; d: any; port: number }> {
    return state.send.mock.calls
        .map((c: any[]) => ({ ...JSON.parse(c[0].toString()), port: c[1] }))
        .filter((p: any) => (!eventName || p.e === eventName) && (port === undefined || p.port === port));
}

const CATALOGO = [
    { id: 't1', description: 'T1', type: 'collect', targetCount: 2, items: ['a', 'b'] },
    { id: 't2', description: 'T2', type: 'collect', targetCount: 1 },
    { id: 't3', description: 'T3', type: 'collect', targetCount: 1 },
];

describe('UdpSocketManager — salas', () => {
    let manager: UdpSocketManager;
    let roomId: string;

    beforeEach(() => {
        (UdpSocketManager as any).instance = undefined;
        state.capturedMessageHandler = null;
        vi.clearAllMocks();
        mockTokens();
        state.registry = new RoomRegistry();
        state.tasks = new TaskService();
        state.clock = { resetRoom: vi.fn() };
        manager = UdpSocketManager.initialize(9998);
        roomId = state.registry.create('Sala do A').id;
    });

    afterEach(() => {
        manager.close();
        (UdpSocketManager as any).instance = undefined;
    });

    // ── CONN ───────────────────────────────────────────────

    it('CONN válido cria sessão na sala com o nome do token', async () => {
        await conn(4000, 'A', 'Ana', roomId);

        const s = manager.getSession(rinfoOf(4000))!;
        expect(s).toMatchObject({ id: 'A', playerName: 'Ana', roomId, floor: 1 });
        expect(sent('CONN_SUCCESS', 4000)[0].d).toMatchObject({ id: 'A', roomId, n: 'Ana' });
    });

    it('o nome vem do token, não do payload', async () => {
        await send(4000, 'CONN', { roomId, playerName: 'Impostor' }, 'tok:A:Ana');
        expect(manager.getSession(rinfoOf(4000))!.playerName).toBe('Ana');
    });

    it('token inválido → CONN_ERROR INVALID_TOKEN', async () => {
        await send(4000, 'CONN', { roomId }, 'lixo');
        expect(sent('CONN_ERROR', 4000)[0].d.code).toBe('INVALID_TOKEN');
        expect(manager.getSession(rinfoOf(4000))).toBeUndefined();
    });

    it('sala inexistente → CONN_ERROR ROOM_NOT_FOUND', async () => {
        await conn(4000, 'A', 'Ana', 'nao-existe');
        expect(sent('CONN_ERROR', 4000)[0].d.code).toBe('ROOM_NOT_FOUND');
    });

    it('3º jogador → CONN_ERROR ROOM_FULL', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);
        await conn(4002, 'C', 'Caio', roomId);

        expect(sent('CONN_ERROR', 4002)[0].d.code).toBe('ROOM_FULL');
        expect(manager.getRoomSessionCount(roomId)).toBe(2);
    });

    it('reconexão do mesmo jogador (outra porta) não ocupa vaga nem anuncia de novo', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);
        state.send.mockClear();

        await conn(4005, 'A', 'Ana', roomId);

        expect(sent('CONN_SUCCESS', 4005)).toHaveLength(1);
        expect(manager.getRoomSessionCount(roomId)).toBe(2);
        expect(manager.getSession(rinfoOf(4000))).toBeUndefined();
        expect(sent('player_joined')).toHaveLength(0);
        expect(sent('player_disconnected')).toHaveLength(0);
    });

    it('quem entra recebe player_joined dos outros e o roster vai para todos', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);

        expect(sent('player_joined', 4000)[0].d).toEqual({ id: 'B', name: 'Bia' });
        expect(sent('player_joined', 4001)).toHaveLength(0);
        const rosterA = sent('room_roster', 4000).pop()!;
        expect(rosterA.d).toEqual({ p: [{ id: 'A', n: 'Ana' }, { id: 'B', n: 'Bia' }] });
    });

    // ── room_state ─────────────────────────────────────────

    it('room_state traz os outros jogadores com posição e andar', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await send(4000, 'player_move', { p: [1, 2, 3], r: 90, f: 2 });
        await conn(4001, 'B', 'Bia', roomId);

        const rs = sent('room_state', 4001)[0].d;
        expect(rs.pl).toEqual([{ id: 'A', n: 'Ana', p: [1, 2, 3], r: 90, f: 2 }]);
    });

    it('room_state traz o estado das tarefas da sala (dono, progresso e itens)', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await send(4000, 'task_catalog_register', { tasks: CATALOGO });
        await send(4000, 'task_assign_request');
        await send(4000, 'task_progress', { taskId: 't1', itemId: 'a' });

        await conn(4001, 'B', 'Bia', roomId);
        const t1 = sent('room_state', 4001)[0].d.t.find((t: any) => t.id === 't1');
        expect(t1).toEqual({ id: 't1', s: 'in_progress', p: 1, ci: ['a'], h: 'A' });
    });

    // ── Tarefas compartilhadas ─────────────────────────────

    it('o 2º jogador não sobrescreve o catálogo e recebe outra tarefa', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await send(4000, 'task_catalog_register', { tasks: CATALOGO });
        await send(4000, 'task_assign_request');

        await conn(4001, 'B', 'Bia', roomId);
        await send(4001, 'task_catalog_register', { tasks: [{ id: 'outra', description: 'X', type: 'collect', targetCount: 1 }] });
        await send(4001, 'task_assign_request');

        const deA = sent('task_assigned').find(p => p.d.playerId === 'A')!.d.tasks[0].id;
        const deB = sent('task_assigned').find(p => p.d.playerId === 'B')!.d.tasks[0].id;
        expect(deA).toBe('t1');
        expect(['t2', 't3']).toContain(deB);
    });

    it('task_updated leva os itens contados (ci)', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await send(4000, 'task_catalog_register', { tasks: CATALOGO });
        await send(4000, 'task_assign_request');
        await send(4000, 'task_progress', { taskId: 't1', itemId: 'a' });

        expect(sent('task_updated', 4000)[0].d).toMatchObject({ taskId: 't1', ci: ['a'] });
    });

    // ── Saída ──────────────────────────────────────────────

    it('leave_room libera a vaga na hora e avisa quem ficou', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);
        state.send.mockClear();

        await send(4001, 'leave_room');

        expect(manager.getSession(rinfoOf(4001))).toBeUndefined();
        expect(sent('player_disconnected', 4000)[0].d).toEqual({ id: 'B' });
        expect(sent('room_roster', 4000)[0].d).toEqual({ p: [{ id: 'A', n: 'Ana' }] });
        expect(state.registry.canJoin(roomId, 'C')).toBe('OK');
    });

    it('a tarefa de quem saiu vai para o jogador que estava sem tarefa', async () => {
        state.registry = new RoomRegistry();
        roomId = state.registry.create('Sala').id;
        await conn(4000, 'A', 'Ana', roomId);
        await send(4000, 'task_catalog_register', { tasks: [CATALOGO[0]] }); // só 1 tarefa
        await send(4000, 'task_assign_request');
        await send(4000, 'task_progress', { taskId: 't1', itemId: 'a' });
        await conn(4001, 'B', 'Bia', roomId);
        await send(4001, 'task_assign_request'); // nada livre
        expect(sent('task_assigned').filter(p => p.d.playerId === 'B')).toHaveLength(0);

        await send(4000, 'leave_room');

        const paraB = sent('task_assigned', 4001).filter(p => p.d.playerId === 'B');
        expect(paraB).toHaveLength(1);
        expect(paraB[0].d.tasks[0]).toMatchObject({ id: 't1', currentProgress: 1, ci: ['a'] });
    });

    it('sessão inativa é removida como uma saída normal', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);
        state.send.mockClear();

        manager.getSession(rinfoOf(4001))!.lastSeen = Date.now() - 35_000;
        (manager as any).cleanupSessions();

        expect(manager.getRoomSessionCount(roomId)).toBe(1);
        expect(sent('player_disconnected', 4000)[0].d).toEqual({ id: 'B' });
    });

    it('sala vazia além do TTL some e o estado dela é descartado', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await send(4000, 'task_catalog_register', { tasks: CATALOGO });
        await send(4000, 'leave_room');

        (manager as any).cleanupSessions(Date.now() + EMPTY_ROOM_TTL_MS + 1000);

        expect(state.registry.get(roomId)).toBeUndefined();
        expect(state.tasks.getRoomSnapshot(roomId)).toEqual([]);
        expect(state.clock.resetRoom).toHaveBeenCalledWith(roomId);
    });

    // ── Andar e NPC ────────────────────────────────────────

    it('player_move repassa o andar para os outros', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);
        await send(4000, 'player_move', { p: [0, 0, 0], r: 0, f: 3 });

        expect(sent('player_move', 4001)[0].d).toEqual({ id: 'A', p: [0, 0, 0], r: 0, f: 3 });
    });

    it('mestre do NPC passa para quem ficou no andar quando o mestre sai', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);
        await send(4000, 'npc_register', { id: 'chefe', type: 'boss', p: [0, 0, 0], r: 0, f: 1 });
        await send(4001, 'npc_register', { id: 'chefe', type: 'boss', p: [0, 0, 0], r: 0, f: 1 });
        expect(NpcRegisterHandler.getMaster(roomId, 'chefe')).toBe('A');
        state.send.mockClear();

        await send(4000, 'leave_room');

        expect(NpcRegisterHandler.getMaster(roomId, 'chefe')).toBe('B');
        expect(sent('npc_registered', 4001)[0].d).toMatchObject({ id: 'chefe', isMaster: true });
    });

    it('mestre do NPC que sobe de andar passa a vez para quem ficou', async () => {
        await conn(4000, 'A', 'Ana', roomId);
        await conn(4001, 'B', 'Bia', roomId);
        await send(4000, 'npc_register', { id: 'chefe', type: 'boss', p: [0, 0, 0], r: 0, f: 1 });

        await send(4000, 'player_move', { p: [0, 0, 0], r: 0, f: 2 });

        expect(NpcRegisterHandler.getMaster(roomId, 'chefe')).toBe('B');
    });
});
