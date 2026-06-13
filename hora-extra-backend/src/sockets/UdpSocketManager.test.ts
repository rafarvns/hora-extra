import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Capture the message handler registered via socket.on('message', ...)
// Use an object so the vi.mock factory (hoisted) can mutate it safely.
const state = {
    capturedMessageHandler: null as ((msg: Buffer, rinfo: any) => void) | null,
};

vi.mock('dgram', () => {
    const mockSocket = {
        on: vi.fn((event: string, handler: any) => {
            if (event === 'message') state.capturedMessageHandler = handler;
            if (event === 'listening') handler(); // fire 'listening' immediately
        }),
        bind: vi.fn(),
        send: vi.fn(),
        close: vi.fn(),
        address: vi.fn().mockReturnValue({ address: '0.0.0.0', port: 5001 }),
    };
    return {
        default: {
            createSocket: vi.fn().mockReturnValue(mockSocket),
        },
        createSocket: vi.fn().mockReturnValue(mockSocket),
    };
});

vi.mock('../services/authService.js', () => ({
    default: {
        verifyToken: vi.fn(),
        generateToken: vi.fn(),
    },
}));

vi.mock('./factories/SocketHandler.Factory.js', () => ({
    SocketHandlerFactory: {
        createHandler: vi.fn().mockReturnValue(null),
    },
}));

vi.mock('./handlers/NpcRegister.Handler.js', () => ({
    NpcRegisterHandler: {
        clearRoomState: vi.fn(),
    },
}));

vi.mock('../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn().mockReturnValue({
            clearRoom: vi.fn(),
            assignRandomTasks: vi.fn(),
            getAssignedTasks: vi.fn(),
        }),
    },
}));

vi.mock('../utils/Logger.js', () => ({
    default: {
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        debug: vi.fn(),
    },
}));

import authService from '../services/authService.js';
import { NpcRegisterHandler } from './handlers/NpcRegister.Handler.js';
import { ServiceFactory } from '../core/factories/Service.Factory.js';
import { UdpSocketManager } from './UdpSocketManager.js';

// Helper: simulate a CONN packet arriving from a remote client
async function simulateCONN(
    _manager: UdpSocketManager,
    token: string,
    address: string,
    port: number,
    extraData: Record<string, unknown> = {},
) {
    const packet = JSON.stringify({ e: 'CONN', token, d: extraData });
    const rinfo = { address, port, family: 'IPv4', size: 0 };
    await state.capturedMessageHandler!(Buffer.from(packet), rinfo);
}

describe('UdpSocketManager — guest path', () => {
    let manager: UdpSocketManager;

    beforeEach(() => {
        // Reset singleton so each test gets a fresh manager
        (UdpSocketManager as any).instance = undefined;
        state.capturedMessageHandler = null;
        vi.clearAllMocks();

        manager = UdpSocketManager.initialize(9998);
    });

    afterEach(() => {
        manager.close();
        (UdpSocketManager as any).instance = undefined;
    });

    it('getRoomSessionCount retorna 0 quando não há sessions na sala', () => {
        const count = manager.getRoomSessionCount('guest-room');
        expect(count).toBe(0);
    });

    it('handshake com JWT guest cria session com roomId = "guest-room"', async () => {
        (authService.verifyToken as any).mockReturnValue({ id: 'guest-ab1c2d3e' });

        await simulateCONN(manager, 'valid-guest-jwt', '10.0.0.1', 4000);

        const session = manager.getSession({ address: '10.0.0.1', port: 4000, family: 'IPv4', size: 0 });

        expect(session).toBeDefined();
        expect(session!.id).toBe('guest-ab1c2d3e');
        expect(session!.roomId).toBe('guest-room');
    });

    it('lazy reset: CONN guest em sala vazia chama NpcRegisterHandler.clearRoomState("guest-room")', async () => {
        (authService.verifyToken as any).mockReturnValue({ id: 'guest-newguest1' });

        // Sala vazia (nenhuma session em guest-room) → deve chamar clearRoomState
        expect(manager.getRoomSessionCount('guest-room')).toBe(0);

        await simulateCONN(manager, 'jwt-for-new-guest', '10.0.0.2', 4001);

        expect(NpcRegisterHandler.clearRoomState).toHaveBeenCalledWith('guest-room');
    });

    it('cleanupSessions limpa NPC state de guest-room quando a sala fica vazia após expiração', async () => {
        (authService.verifyToken as any).mockReturnValue({ id: 'guest-expiring1' });

        // Guest conecta
        await simulateCONN(manager, 'expiring-jwt', '10.0.0.3', 4002);
        expect(manager.getRoomSessionCount('guest-room')).toBe(1);

        vi.clearAllMocks(); // reset call count after lazy-reset from CONN

        // Simular session expirada: forçar lastSeen > 30s atrás
        const session = manager.getSession({ address: '10.0.0.3', port: 4002, family: 'IPv4', size: 0 });
        expect(session).toBeDefined();
        session!.lastSeen = Date.now() - 35000;

        // Invocar cleanupSessions via o método público exposto (vamos chamá-lo via cast)
        (manager as any).cleanupSessions();

        expect(manager.getRoomSessionCount('guest-room')).toBe(0);
        expect(NpcRegisterHandler.clearRoomState).toHaveBeenCalledWith('guest-room');
    });

    it('resetRoom chama TaskService.clearRoom com playerIds da sala', async () => {
        // Setup: dois jogadores dev entram em dev-room via dev bypass
        process.env.NODE_ENV = 'development';
        process.env.DEV_TEST_TOKEN = 'dev-token';
        process.env.DEV_TEST_USER_ID = 'dev-player-A';

        await simulateCONN(manager, 'dev-token', '10.0.2.1', 4200);
        // Second dev CONN from different address (different session) — but DEV_TEST_USER_ID is the same
        // So we need two different player IDs: use JWT sessions for them instead
        (authService.verifyToken as any).mockReturnValueOnce({ id: 'player-B' });
        await simulateCONN(manager, 'jwt-b', '10.0.2.2', 4201);

        // player-B is NOT in dev-room (no auto-join), so manually set roomId
        const sessionB = manager.getSession({ address: '10.0.2.2', port: 4201, family: 'IPv4', size: 0 });
        if (sessionB) sessionB.roomId = 'dev-room';

        expect(manager.getRoomSessionCount('dev-room')).toBeGreaterThanOrEqual(1);

        // Re-mock taskService to capture call
        const mockTaskService = { clearRoom: vi.fn(), assignRandomTasks: vi.fn() };
        (ServiceFactory.getTaskService as any).mockReturnValue(mockTaskService);

        // Trigger resetRoomState via dev CONN with resetRoom:true from a NEW address
        await simulateCONN(manager, 'dev-token', '10.0.2.3', 4202, { resetRoom: true });

        expect(mockTaskService.clearRoom).toHaveBeenCalled();
        // clearRoom was called with an array (may include dev-player-A and/or player-B)
        const [calledWith] = mockTaskService.clearRoom.mock.calls[0] as [string[]];
        expect(Array.isArray(calledWith)).toBe(true);
    });

    it('resetRoom limpa sessões da sala após clearRoom', async () => {
        // Dev player connects → auto-join dev-room
        process.env.NODE_ENV = 'development';
        process.env.DEV_TEST_TOKEN = 'dev-tok';
        process.env.DEV_TEST_USER_ID = 'dev-player-C';

        await simulateCONN(manager, 'dev-tok', '10.0.3.1', 4300);
        expect(manager.getRoomSessionCount('dev-room')).toBe(1);

        // Re-mock taskService to capture calls
        const mockTaskService = { clearRoom: vi.fn(), assignRandomTasks: vi.fn() };
        (ServiceFactory.getTaskService as any).mockReturnValue(mockTaskService);

        // Trigger reset from a new address
        await simulateCONN(manager, 'dev-tok', '10.0.3.2', 4301, { resetRoom: true });

        // The previous session (10.0.3.1) should have been removed
        const removedSession = manager.getSession({ address: '10.0.3.1', port: 4300, family: 'IPv4', size: 0 });
        expect(removedSession).toBeUndefined();
    });
});
