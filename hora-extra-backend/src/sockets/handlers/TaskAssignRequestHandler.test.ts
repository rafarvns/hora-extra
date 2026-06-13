import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn(),
    },
}));

import { TaskAssignRequestHandler } from './TaskAssignRequestHandler.Handler.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

describe('TaskAssignRequestHandler', () => {
    let handler: TaskAssignRequestHandler;
    let mockServer: any;
    let mockTaskService: any;
    const rinfo = { address: '127.0.0.1', port: 5001 } as any;

    beforeEach(() => {
        handler = new TaskAssignRequestHandler();
        mockTaskService = {
            assignRandomTasks: vi.fn(),
            getAssignedTasks: vi.fn(),
        };
        mockServer = {
            getSession: vi.fn(),
            sendTo: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
        (ServiceFactory.getTaskService as any).mockReturnValue(mockTaskService);
    });

    it('rejeita quando sessão não existe', async () => {
        mockServer.getSession.mockReturnValue(undefined);

        await handler.handle(mockServer, rinfo, {});

        expect(mockTaskService.assignRandomTasks).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.objectContaining({ message: expect.any(String) }));
    });

    it('rejeita quando sessão não tem roomId', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1' }); // sem roomId

        await handler.handle(mockServer, rinfo, {});

        expect(mockTaskService.assignRandomTasks).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.objectContaining({ message: expect.any(String) }));
    });

    it('chama assignRandomTasks com o playerId da sessão', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p42', roomId: 'r1' });
        mockTaskService.assignRandomTasks.mockReturnValue([]);

        await handler.handle(mockServer, rinfo, {});

        expect(mockTaskService.assignRandomTasks).toHaveBeenCalledWith('r1', 'p42');
    });

    it('NÃO faz broadcast se assignRandomTasks retorna [] — idempotência', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        mockTaskService.assignRandomTasks.mockReturnValue([]); // idempotência: retorna vazio

        await handler.handle(mockServer, rinfo, {});

        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('captura ApiError de catálogo vazio e responde ERROR ao remetente', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        mockTaskService.assignRandomTasks.mockImplementation(() => {
            throw new Error('Catálogo da sala vazio');
        });

        await handler.handle(mockServer, rinfo, {});

        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(
            rinfo,
            'ERROR',
            expect.objectContaining({ message: expect.any(String) }),
        );
    });

    it('faz broadcast task_assigned com playerId e tasks', async () => {
        const tasks = [
            { id: 't1', description: 'Desc1', type: 'collect', targetCount: 3, currentProgress: 0, status: 'pending' },
            { id: 't2', description: 'Desc2', type: 'deliver', targetCount: 1, currentProgress: 0, status: 'pending' },
            { id: 't3', description: 'Desc3', type: 'repair',  targetCount: 1, currentProgress: 0, status: 'pending' },
        ];
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        mockTaskService.assignRandomTasks.mockReturnValue(tasks);

        await handler.handle(mockServer, rinfo, {});

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'r1',
            'task_assigned',
            { playerId: 'p1', tasks },
        );
    });

    it('payload broadcast tem shape correto (todos os campos de AssignedTask presentes)', async () => {
        const tasks = [
            { id: 't1', description: 'Desc1', type: 'collect', targetCount: 3, currentProgress: 0, status: 'pending' as const },
        ];
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        mockTaskService.assignRandomTasks.mockReturnValue(tasks);

        await handler.handle(mockServer, rinfo, {});

        const [, , payload] = mockServer.broadcastToRoom.mock.calls[0];
        expect(payload).toMatchObject({
            playerId: 'p1',
            tasks: [
                expect.objectContaining({
                    id: expect.any(String),
                    description: expect.any(String),
                    type: expect.any(String),
                    targetCount: expect.any(Number),
                    currentProgress: expect.any(Number),
                    status: expect.stringMatching(/^(pending|in_progress|completed)$/),
                }),
            ],
        });
    });
});
