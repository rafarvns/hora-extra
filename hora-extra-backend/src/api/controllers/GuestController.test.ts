import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Request, Response, NextFunction } from 'express';

// Mock ServiceFactory so we can control guestService behavior
vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getGuestService: vi.fn(),
    },
}));

vi.mock('../../utils/Logger.js', () => ({
    default: {
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        debug: vi.fn(),
    },
}));

import { GuestController } from './GuestController.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

describe('GuestController.joinAsGuest', () => {
    let controller: GuestController;
    let req: Partial<Request>;
    let res: Partial<Response>;
    let next: NextFunction;
    let statusMock: ReturnType<typeof vi.fn>;
    let jsonMock: ReturnType<typeof vi.fn>;

    beforeEach(() => {
        vi.clearAllMocks();
        controller = new GuestController();

        jsonMock = vi.fn();
        statusMock = vi.fn().mockReturnValue({ json: jsonMock });
        res = {
            status: statusMock,
        } as any;
        req = {};
        next = vi.fn() as any;
    });

    it('retorna 201 com token, guestId e roomId quando service tem sucesso', async () => {
        const fakeResult = {
            token: 'some-jwt',
            guestId: 'guest-abc12345',
            roomId: 'guest-room',
        };

        (ServiceFactory.getGuestService as any).mockReturnValue({
            joinAsGuest: vi.fn().mockResolvedValue(fakeResult),
        });

        await controller.joinAsGuest(req as Request, res as Response, next);

        expect(statusMock).toHaveBeenCalledWith(201);
        expect(jsonMock).toHaveBeenCalledWith(
            expect.objectContaining({
                success: true,
                data: fakeResult,
            }),
        );
    });

    it('chama next(err) quando service lança exceção', async () => {
        const fakeError = new Error('Erro inesperado');
        (ServiceFactory.getGuestService as any).mockReturnValue({
            joinAsGuest: vi.fn().mockRejectedValue(fakeError),
        });

        await controller.joinAsGuest(req as Request, res as Response, next);

        expect(next).toHaveBeenCalledWith(fakeError);
        expect(statusMock).not.toHaveBeenCalled();
    });
});
