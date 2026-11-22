import { describe, it, expect, vi } from 'vitest';

vi.mock('../utils/Logger.js', () => ({
    default: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));

import { SessionService } from './sessionService.js';
import authService from './authService.js';
import { ApiError } from '../core/ApiError.js';

describe('SessionService', () => {
    const service = new SessionService();

    it('cria sessão com id novo e token que carrega o nome', () => {
        const a = service.create('  Ana ');
        const b = service.create('Ana');
        expect(a.nome).toBe('Ana');
        expect(a.playerId).toMatch(/^p-/);
        expect(a.playerId).not.toBe(b.playerId);
        expect(authService.verifyToken(a.token)).toEqual({ id: a.playerId, n: 'Ana' });
    });

    it('recusa nome vazio, só espaços ou longo demais', () => {
        expect(() => service.create('')).toThrow(ApiError);
        expect(() => service.create('   ')).toThrow(ApiError);
        expect(() => service.create('x'.repeat(17))).toThrow(ApiError);
        expect(() => service.create(undefined)).toThrow(ApiError);
    });
});
