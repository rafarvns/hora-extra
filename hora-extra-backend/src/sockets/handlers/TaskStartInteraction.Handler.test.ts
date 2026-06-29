import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn(),
    },
}));

import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import { TaskStartInteractionHandler } from './TaskStartInteraction.Handler.js';

describe('TaskStartInteractionHandler', () => {
    let handler: TaskStartInteractionHandler;
    let mockServer: {
        getSession: ReturnType<typeof vi.fn>;
        sendTo: ReturnType<typeof vi.fn>;
        broadcastToRoom: ReturnType<typeof vi.fn>;
    };
    const rinfo = { address: '127.0.0.1', port: 5002 } as any;

    beforeEach(() => {
        handler = new TaskStartInteractionHandler();
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
        // Sessão existe mas sem roomId
        mockServer.getSession.mockReturnValue({ id: 'p1' });
        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001' });
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('chama taskService.startTask com playerId da sessão e taskId do payload', async () => {
        const mockStartTask = vi.fn().mockReturnValue({
            id: 'task-001',
            currentProgress: 0,
            status: 'in_progress',
            targetCount: 3,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ startTask: mockStartTask });
        mockServer.getSession.mockReturnValue({ id: 'player-42', roomId: 'room-1' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001' });

        expect(mockStartTask).toHaveBeenCalledWith('player-42', 'task-001');
    });

    it('faz broadcastToRoom task_updated com { playerId, taskId, currentProgress, status } em sucesso', async () => {
        const mockStartTask = vi.fn().mockReturnValue({
            id: 'task-001',
            currentProgress: 0,
            status: 'in_progress',
            targetCount: 3,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ startTask: mockStartTask });
        mockServer.getSession.mockReturnValue({ id: 'player-42', roomId: 'room-1' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001' });

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'room-1',
            'task_updated',
            {
                playerId: 'player-42',
                taskId: 'task-001',
                currentProgress: 0,
                status: 'in_progress',
            },
        );
        expect(mockServer.sendTo).not.toHaveBeenCalled();
    });

    it('responde ERROR ao remetente quando startTask lança ApiError — sem broadcast', async () => {
        const mockStartTask = vi.fn().mockImplementation(() => {
            throw new Error('Transição inválida: task já está in_progress');
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ startTask: mockStartTask });
        mockServer.getSession.mockReturnValue({ id: 'player-42', roomId: 'room-1' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'task-001' });

        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });
});
