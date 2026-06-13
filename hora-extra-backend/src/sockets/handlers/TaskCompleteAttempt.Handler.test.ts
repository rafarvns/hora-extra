import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn(),
    },
}));

import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import { TaskCompleteAttemptHandler } from './TaskCompleteAttempt.Handler.js';

describe('TaskCompleteAttemptHandler', () => {
    let handler: TaskCompleteAttemptHandler;
    let mockServer: {
        getSession: ReturnType<typeof vi.fn>;
        sendTo: ReturnType<typeof vi.fn>;
        broadcastToRoom: ReturnType<typeof vi.fn>;
    };
    const rinfo = { address: '127.0.0.1', port: 5003 } as any;

    beforeEach(() => {
        handler = new TaskCompleteAttemptHandler();
        mockServer = {
            getSession: vi.fn(),
            sendTo: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
        vi.clearAllMocks();
    });

    it('rejeita payload sem taskId ou sem campo success — responde ERROR', async () => {
        // sem taskId
        await handler.handle(mockServer as any, rinfo, { success: true } as any);
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();

        vi.clearAllMocks();

        // sem success
        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001' } as any);
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('rejeita sessão sem roomId — responde ERROR', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1' }); // sem roomId
        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001', success: true });
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('chama taskService.resolveTask com playerId, taskId e success do payload', async () => {
        const mockResolveTask = vi.fn().mockReturnValue({
            id: 'task-001',
            currentProgress: 3,
            status: 'completed',
            targetCount: 3,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ resolveTask: mockResolveTask });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001', success: true });

        expect(mockResolveTask).toHaveBeenCalledWith('player-99', 'task-001', true);
    });

    it('faz broadcastToRoom task_updated com status=completed quando success=true', async () => {
        const mockResolveTask = vi.fn().mockReturnValue({
            id: 'task-001',
            currentProgress: 3,
            status: 'completed',
            targetCount: 3,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ resolveTask: mockResolveTask });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001', success: true });

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'room-2',
            'task_updated',
            {
                playerId: 'player-99',
                taskId: 'task-001',
                currentProgress: 3,
                status: 'completed',
            },
        );
        expect(mockServer.sendTo).not.toHaveBeenCalled();
    });

    it('faz broadcastToRoom task_updated com status=failed quando success=false', async () => {
        const mockResolveTask = vi.fn().mockReturnValue({
            id: 'task-001',
            currentProgress: 0,
            status: 'failed',
            targetCount: 3,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ resolveTask: mockResolveTask });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001', success: false });

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'room-2',
            'task_updated',
            {
                playerId: 'player-99',
                taskId: 'task-001',
                currentProgress: 0,
                status: 'failed',
            },
        );
        expect(mockServer.sendTo).not.toHaveBeenCalled();
    });

    it('responde ERROR quando resolveTask lança ApiError — sem broadcast', async () => {
        const mockResolveTask = vi.fn().mockImplementation(() => {
            throw new Error('Transição inválida: task não está in_progress');
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ resolveTask: mockResolveTask });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001', success: true });

        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });
});
