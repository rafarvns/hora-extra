import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn(),
    },
}));

import { TaskCatalogRegisterHandler } from './TaskCatalogRegisterHandler.Handler.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

describe('TaskCatalogRegisterHandler', () => {
    let handler: TaskCatalogRegisterHandler;
    let mockServer: any;
    let mockTaskService: any;
    const rinfo = { address: '127.0.0.1', port: 5001 } as any;

    beforeEach(() => {
        handler = new TaskCatalogRegisterHandler();
        mockTaskService = {
            registerCatalog: vi.fn(),
        };
        mockServer = {
            getSession: vi.fn(),
            sendTo: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
        (ServiceFactory.getTaskService as any).mockReturnValue(mockTaskService);
    });

    it('rejeita payload sem campo tasks — responde ERROR ao remetente', async () => {
        mockServer.getSession.mockReturnValue({ id: 'player-1', roomId: 'room-1' });

        await handler.handle(mockServer, rinfo, {} as any);

        expect(mockTaskService.registerCatalog).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(
            rinfo,
            'ERROR',
            expect.objectContaining({ message: expect.any(String) }),
        );
    });

    it('rejeita sessão sem roomId — responde ERROR ao remetente', async () => {
        // Sessão existe mas sem roomId
        mockServer.getSession.mockReturnValue({ id: 'player-1' });

        await handler.handle(mockServer, rinfo, {
            tasks: [{ id: 'task-01', description: 'Teste', type: 'collect', targetCount: 1 }],
        });

        expect(mockTaskService.registerCatalog).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(
            rinfo,
            'ERROR',
            expect.objectContaining({ message: expect.any(String) }),
        );
    });

    it('chama registerCatalog com tasks e roomId corretos (sem npcId)', async () => {
        mockServer.getSession.mockReturnValue({ id: 'player-1', roomId: 'room-42' });
        const tasks = [
            { id: 'task-01', description: 'Recolher documentos', type: 'collect', targetCount: 3 },
            { id: 'task-02', description: 'Entregar relatório', type: 'deliver', targetCount: 1 },
        ];

        await handler.handle(mockServer, rinfo, { tasks });

        expect(mockTaskService.registerCatalog).toHaveBeenCalledOnce();
        expect(mockTaskService.registerCatalog).toHaveBeenCalledWith('room-42', tasks);
    });
});
