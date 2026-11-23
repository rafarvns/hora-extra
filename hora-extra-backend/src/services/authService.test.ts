import { describe, it, expect, vi } from 'vitest';

vi.mock('../utils/Logger.js', () => ({
    default: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));

import authService from './authService.js';

describe('AuthService', () => {
    it('gera um token com id e nome e consegue verificá-lo', () => {
        const token = authService.generateToken('p-123', 'Ana');
        expect(authService.verifyToken(token)).toEqual({ id: 'p-123', n: 'Ana' });
    });

    it('retorna null para um token inválido', () => {
        expect(authService.verifyToken('token-invalido')).toBeNull();
    });
});
