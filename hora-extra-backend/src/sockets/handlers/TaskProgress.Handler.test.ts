import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn(),
    },
}));

import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import { TaskProgressHandler } from './TaskProgress.Handler.js';

describe('TaskProgressHandler', () => {
    let handler: TaskProgressHandler;
    let mockServer: {
        getSession: ReturnType<typeof vi.fn>;
        sendTo: ReturnType<typeof vi.fn>;
        broadcastToRoom: ReturnType<typeof vi.fn>;
    };
    const rinfo = { address: '127.0.0.1', port: 5004 } as any;

    beforeEach(() => {
        handler = new TaskProgressHandler();
        mockServer = {
            getSession: vi.fn(),
            sendTo: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
        vi.clearAllMocks();
    });

    it('rejeita payload sem taskId — responde ERROR', async () => {
        await handler.handle(mockServer as any, rinfo, {} as any);
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('rejeita sessão sem roomId — responde ERROR', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1' }); // sem roomId
        await handler.handle(mockServer as any, rinfo, { taskId: 'collect-1' });
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('chama taskService.incrementProgress com playerId e taskId da sessão/payload', async () => {
        const mockIncrement = vi.fn().mockReturnValue({
            id: 'collect-1',
            currentProgress: 1,
            status: 'in_progress',
            targetCount: 4,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'collect-1' });

        expect(mockIncrement).toHaveBeenCalledWith('player-99', 'collect-1');
    });

    it('faz broadcastToRoom task_updated com progresso parcial (in_progress)', async () => {
        const mockIncrement = vi.fn().mockReturnValue({
            id: 'collect-1',
            currentProgress: 2,
            status: 'in_progress',
            targetCount: 4,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'collect-1' });

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'room-2',
            'task_updated',
            {
                playerId: 'player-99',
                taskId: 'collect-1',
                currentProgress: 2,
                status: 'in_progress',
            },
        );
        expect(mockServer.sendTo).not.toHaveBeenCalled();
    });

    it('faz broadcastToRoom task_updated com status=completed ao atingir o alvo', async () => {
        const mockIncrement = vi.fn().mockReturnValue({
            id: 'collect-1',
            currentProgress: 4,
            status: 'completed',
            targetCount: 4,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'collect-1' });

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'room-2',
            'task_updated',
            {
                playerId: 'player-99',
                taskId: 'collect-1',
                currentProgress: 4,
                status: 'completed',
            },
        );
    });

    it('responde ERROR quando incrementProgress lança — sem broadcast', async () => {
        const mockIncrement = vi.fn().mockImplementation(() => {
            throw new Error('Transição inválida: task já está completed');
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'collect-1' });

        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });
});
