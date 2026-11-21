import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../utils/Logger.js', () => ({
    default: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));

import { JoinRoomHandler } from './JoinRoom.Handler.js';

describe('JoinRoomHandler', () => {
    let handler: JoinRoomHandler;
    let mockServer: any;
    const rinfo = { address: '127.0.0.1', port: 5001 } as any;

    beforeEach(() => {
        handler = new JoinRoomHandler();
        mockServer = {
            getSession: vi.fn(),
            sendTo: vi.fn(),
            broadcastToRoom: vi.fn(),
            broadcastRoster: vi.fn(),
        };
    });

    it('ignora payload sem roomId/playerName', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1' });

        await handler.handle(mockServer, rinfo, { roomId: '', playerName: '' });

        expect(mockServer.broadcastRoster).not.toHaveBeenCalled();
        expect(mockServer.sendTo).not.toHaveBeenCalled();
    });

    it('atualiza a sessão, confirma ao remetente e faz broadcast do roster da sala', async () => {
        const session: any = { id: 'p1' };
        mockServer.getSession.mockReturnValue(session);

        await handler.handle(mockServer, rinfo, { roomId: 'r1', playerName: 'Ana' });

        expect(session.roomId).toBe('r1');
        expect(session.playerName).toBe('Ana');
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'room_joined', expect.objectContaining({ roomId: 'r1', playerId: 'p1' }));
        expect(mockServer.broadcastRoster).toHaveBeenCalledWith('r1');
    });

    it('ao trocar de sala, também atualiza o roster da sala antiga', async () => {
        const session: any = { id: 'p1', roomId: 'antiga' };
        mockServer.getSession.mockReturnValue(session);

        await handler.handle(mockServer, rinfo, { roomId: 'nova', playerName: 'Ana' });

        expect(mockServer.broadcastRoster).toHaveBeenCalledWith('antiga');
        expect(mockServer.broadcastRoster).toHaveBeenCalledWith('nova');
    });
});
