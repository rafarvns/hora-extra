import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn(),
    },
}));

import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import { TaskProgressHandler } from './TaskProgress.Handler.js';
import { TaskRejectionError } from '../../services/TaskService.js';

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
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement, advanceQueue: vi.fn().mockReturnValue(null) });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'collect-1' });

        expect(mockIncrement).toHaveBeenCalledWith('player-99', 'collect-1', undefined, undefined);
    });

    it('faz broadcastToRoom task_updated com progresso parcial (in_progress)', async () => {
        const mockIncrement = vi.fn().mockReturnValue({
            id: 'collect-1',
            currentProgress: 2,
            status: 'in_progress',
            targetCount: 4,
        });
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement, advanceQueue: vi.fn().mockReturnValue(null) });
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
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement, advanceQueue: vi.fn().mockReturnValue(null) });
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
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: mockIncrement, advanceQueue: vi.fn().mockReturnValue(null) });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'collect-1' });

        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    // ────────────────────────────────────────────────────────
    // Identidade de item e canal de recusa (plano 0004)
    // ────────────────────────────────────────────────────────

    /** Mocka o service com um incrementProgress controlável e devolve o spy. */
    function mockService(impl: (...args: any[]) => any) {
        const spy = vi.fn(impl);
        (ServiceFactory.getTaskService as any).mockReturnValue({ incrementProgress: spy, advanceQueue: vi.fn().mockReturnValue(null) });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });
        return spy;
    }

    const okTask = { id: 'coleta-1', currentProgress: 1, status: 'in_progress', targetCount: 4 };

    // ────────────────────────────────────────────────────────
    // Fila sequencial de tarefas
    // ────────────────────────────────────────────────────────

    it('ao concluir, libera a próxima da fila via task_assigned', async () => {
        const proxima = { id: 'coleta-2', type: 'collect', targetCount: 3, currentProgress: 0, status: 'pending' };
        (ServiceFactory.getTaskService as any).mockReturnValue({
            incrementProgress: vi.fn().mockReturnValue({ id: 'coleta-1', currentProgress: 4, status: 'completed', targetCount: 4 }),
            advanceQueue: vi.fn().mockReturnValue(proxima),
        });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'coleta-1' });

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'room-2', 'task_assigned', { playerId: 'player-99', tasks: [proxima] },
        );
    });

    it('não anuncia task_assigned quando a fila não libera nada', async () => {
        const advance = vi.fn().mockReturnValue(null);
        (ServiceFactory.getTaskService as any).mockReturnValue({
            incrementProgress: vi.fn().mockReturnValue({ id: 'coleta-1', currentProgress: 1, status: 'in_progress', targetCount: 4 }),
            advanceQueue: advance,
        });
        mockServer.getSession.mockReturnValue({ id: 'player-99', roomId: 'room-2' });

        await handler.handle(mockServer as any, rinfo, { taskId: 'coleta-1' });

        expect(advance).toHaveBeenCalledWith('player-99');
        const eventos = mockServer.broadcastToRoom.mock.calls.map((c: any[]) => c[1]);
        expect(eventos).not.toContain('task_assigned');
    });

    // Cycle 13
    it('repassa itemId e slotId do payload para incrementProgress', async () => {
        const spy = mockService(() => okTask);

        await handler.handle(mockServer as any, rinfo, {
            taskId: 'coleta-1',
            itemId: 'papel-01',
            slotId: 'caixa-papeis',
        } as any);

        expect(spy).toHaveBeenCalledWith('player-99', 'coleta-1', 'papel-01', 'caixa-papeis');
    });

    // Cycle 14
    it('aceita payload sem itemId/slotId — retrocompatibilidade', async () => {
        const spy = mockService(() => okTask);

        await handler.handle(mockServer as any, rinfo, { taskId: 'coleta-1' });

        expect(spy).toHaveBeenCalledWith('player-99', 'coleta-1', undefined, undefined);
        expect(mockServer.broadcastToRoom).toHaveBeenCalled();
    });

    // Cycle 15
    it('rejeita itemId de tipo não-string com ERROR genérico', async () => {
        const spy = mockService(() => okTask);

        await handler.handle(mockServer as any, rinfo, { taskId: 'coleta-1', itemId: 42 } as any);

        // Payload malformado é falha de protocolo, não de gameplay
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(spy).not.toHaveBeenCalled();
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('rejeita slotId de tipo não-string com ERROR genérico', async () => {
        const spy = mockService(() => okTask);

        await handler.handle(mockServer as any, rinfo, { taskId: 'coleta-1', slotId: {} } as any);

        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(spy).not.toHaveBeenCalled();
    });

    // Cycle 16
    it('responde task_rejected com code e message quando o service lança TaskRejectionError', async () => {
        mockService(() => {
            throw new TaskRejectionError('WRONG_SLOT', 'Item não pertence a este lugar');
        });

        await handler.handle(mockServer as any, rinfo, {
            taskId: 'coleta-1',
            itemId: 'caixa-ti',
            slotId: 'estante-rh',
        } as any);

        expect(mockServer.sendTo).toHaveBeenCalledWith(
            rinfo,
            'task_rejected',
            expect.objectContaining({
                taskId: 'coleta-1',
                code: 'WRONG_SLOT',
                message: 'Item não pertence a este lugar',
            }),
        );
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    // Cycle 17
    it('inclui o itemId original no payload de task_rejected', async () => {
        mockService(() => {
            throw new TaskRejectionError('ALREADY_COUNTED', 'Item já contabilizado');
        });

        await handler.handle(mockServer as any, rinfo, {
            taskId: 'coleta-1',
            itemId: 'papel-01',
        } as any);

        expect(mockServer.sendTo).toHaveBeenCalledWith(
            rinfo,
            'task_rejected',
            expect.objectContaining({ itemId: 'papel-01' }),
        );
    });

    // Cycle 18
    it('responde ERROR genérico quando o erro não é TaskRejectionError', async () => {
        mockService(() => {
            throw new Error('falha inesperada de infraestrutura');
        });

        await handler.handle(mockServer as any, rinfo, { taskId: 'coleta-1', itemId: 'papel-01' } as any);

        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.any(Object));
        expect(mockServer.sendTo).not.toHaveBeenCalledWith(rinfo, 'task_rejected', expect.anything());
    });

    // Cycle 19
    it('faz broadcastToRoom task_updated em sucesso com itemId presente, sem vazar itemId/slotId', async () => {
        mockService(() => okTask);

        await handler.handle(mockServer as any, rinfo, {
            taskId: 'coleta-1',
            itemId: 'papel-01',
            slotId: 'caixa-papeis',
        } as any);

        // O payload de broadcast é construído pelo servidor e não ecoa dado cru do cliente
        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith('room-2', 'task_updated', {
            playerId: 'player-99',
            taskId: 'coleta-1',
            currentProgress: 1,
            status: 'in_progress',
        });
    });
});
