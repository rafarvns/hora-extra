import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock logger to avoid side effects
vi.mock('../utils/Logger.js', () => ({
    default: {
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        debug: vi.fn(),
    },
}));

// Mock authService so we control token generation
vi.mock('./authService.js', () => ({
    default: {
        generateToken: vi.fn().mockReturnValue('mocked-jwt-token'),
        verifyToken: vi.fn(),
    },
}));

import { GuestService } from './guestService.js';
import authService from './authService.js';

describe('GuestService.joinAsGuest', () => {
    let service: GuestService;

    beforeEach(() => {
        vi.clearAllMocks();
        service = new GuestService();
    });

    it('retorna guestId que começa com "guest-"', async () => {
        const result = await service.joinAsGuest();
        expect(result.guestId).toMatch(/^guest-/);
    });

    it('retorna token string e roomId igual a "guest-room"', async () => {
        const result = await service.joinAsGuest();
        expect(typeof result.token).toBe('string');
        expect(result.token.length).toBeGreaterThan(0);
        expect(result.roomId).toBe('guest-room');
    });

    it('chama authService.generateToken com o guestId gerado', async () => {
        const result = await service.joinAsGuest();
        expect(authService.generateToken).toHaveBeenCalledWith(result.guestId);
    });
});
