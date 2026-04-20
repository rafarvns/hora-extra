import { describe, it, expect, vi } from 'vitest';
import { PlayerSprintHandler } from './PlayerSprint.Handler.js';

describe('PlayerSprintHandler', () => {
    it('deve atualizar o estado isSprinting na sessão e fazer o broadcast para a sala', async () => {
        const handler = new PlayerSprintHandler();
        
        // Mock do servidor UDP
        const mockSession = {
            id: 'player-123',
            roomId: 'room-1',
            isSprinting: false
        };
        
        const mockServer = {
            getSession: vi.fn().mockReturnValue(mockSession),
            broadcastToRoom: vi.fn()
        };
        
        const mockRinfo = {
            address: '127.0.0.1',
            port: 5001
        };
        
        const data = { s: true };
        
        // Executar handler
        await handler.handle(mockServer as any, mockRinfo as any, data);
        
        // Verificações
        expect(mockSession.isSprinting).toBe(true);
        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'room-1',
            'player_sprint',
            { id: 'player-123', s: true },
            mockRinfo
        );
    });

    it('deve ignorar payloads inválidos', async () => {
        const handler = new PlayerSprintHandler();
        const mockServer = {
            getSession: vi.fn(),
            broadcastToRoom: vi.fn()
        };
        
        // Payload sem o booleano 's'
        await handler.handle(mockServer as any, {} as any, {} as any);
        
        expect(mockServer.getSession).not.toHaveBeenCalled();
    });
});
