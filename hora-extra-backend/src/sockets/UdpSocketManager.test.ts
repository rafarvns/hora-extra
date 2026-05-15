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
});
