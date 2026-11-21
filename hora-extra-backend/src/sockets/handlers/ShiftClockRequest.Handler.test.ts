import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getShiftClockService: vi.fn(),
    },
}));

import { ShiftClockRequestHandler } from './ShiftClockRequest.Handler.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

describe('ShiftClockRequestHandler', () => {
    let handler: ShiftClockRequestHandler;
    let mockServer: any;
    let mockClock: any;
    const rinfo = { address: '127.0.0.1', port: 5001 } as any;
    const snapshot = { m: 480, s: 480, e: 1080, k: 0.66, st: 'running' };

    beforeEach(() => {
        handler = new ShiftClockRequestHandler();
        mockClock = {
            startShift: vi.fn(),
            getSnapshot: vi.fn().mockReturnValue(snapshot),
        };
        mockServer = {
            getSession: vi.fn(),
            sendTo: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
        (ServiceFactory.getShiftClockService as any).mockReturnValue(mockClock);
    });

    it('rejeita quando sessão não existe', async () => {
        mockServer.getSession.mockReturnValue(undefined);

        await handler.handle(mockServer, rinfo, {});

        expect(mockClock.startShift).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.objectContaining({ message: expect.any(String) }));
    });

    it('rejeita quando sessão não tem roomId', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1' });

        await handler.handle(mockServer, rinfo, {});

        expect(mockClock.startShift).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'ERROR', expect.objectContaining({ message: expect.any(String) }));
    });

    it('quando o expediente começa agora, faz broadcast shift_clock para a sala', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        mockClock.startShift.mockReturnValue(true);

        await handler.handle(mockServer, rinfo, {});

        expect(mockClock.startShift).toHaveBeenCalledWith('r1');
        expect(mockClock.getSnapshot).toHaveBeenCalledWith('r1');
        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith('r1', 'shift_clock', snapshot);
        expect(mockServer.sendTo).not.toHaveBeenCalled();
    });

    it('quando o expediente já estava correndo, responde só ao remetente', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        mockClock.startShift.mockReturnValue(false);

        await handler.handle(mockServer, rinfo, {});

        expect(mockServer.sendTo).toHaveBeenCalledWith(rinfo, 'shift_clock', snapshot);
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });
});
