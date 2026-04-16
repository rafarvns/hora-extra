import { describe, it, expect, vi } from 'vitest';
import authService from './authService.js';
import jwt from 'jsonwebtoken';

describe('AuthService', () => {
    describe('hashPassword', () => {
        it('deve gerar um hash diferente da senha original', async () => {
            const senha = 'minha_senha_123';
            const hash = await authService.hashPassword(senha);
            
            expect(hash).not.toBe(senha);
            expect(hash.length).toBeGreaterThan(20);
        });
    });

    describe('comparePasswords', () => {
        it('deve retornar true para a senha correta e seu hash', async () => {
            const senha = 'minha_senha_123';
            const hash = await authService.hashPassword(senha);
            const result = await authService.comparePasswords(senha, hash);
            
            expect(result).toBe(true);
        });

        it('deve retornar false para a senha incorreta', async () => {
            const senha = 'minha_senha_123';
            const hash = await authService.hashPassword(senha);
            const result = await authService.comparePasswords('outra_senha', hash);
            
            expect(result).toBe(false);
        });
    });

    describe('generateToken e verifyToken', () => {
        it('deve gerar um token e conseguir verificá-lo', () => {
            const userId = 'user-123';
            const token = authService.generateToken(userId);
            const decoded = authService.verifyToken(token);
            
            expect(decoded).toBeDefined();
            expect(decoded.id).toBe(userId);
        });

        it('deve retornar null para um token inválido', () => {
            const result = authService.verifyToken('token-invalido');
            expect(result).toBe(null);
        });
    });
});
