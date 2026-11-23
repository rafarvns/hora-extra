import jwt from 'jsonwebtoken';
import logger from '../utils/Logger.js';

const JWT_SECRET = process.env.JWT_SECRET || 'fallback_secret_for_dev_only';
const JWT_EXPIRES_IN = process.env.JWT_EXPIRES_IN || '1d';

/** Conteúdo do token de sessão: id do jogador + nome digitado. */
export interface SessionClaims {
  id: string;
  n: string;
}

/**
 * AuthService: emite e confere o token de sessão. Não há cadastro: o token só carrega
 * o id gerado pelo servidor e o nome que o jogador digitou.
 */
class AuthService {
  generateToken(playerId: string, nome: string): string {
    return jwt.sign({ id: playerId, n: nome }, JWT_SECRET, {
      expiresIn: JWT_EXPIRES_IN as any,
    });
  }

  /** Devolve as claims ou null se o token for inválido/expirado. */
  verifyToken(token: string): SessionClaims | null {
    try {
      const decoded = jwt.verify(token, JWT_SECRET) as any;
      if (!decoded || typeof decoded.id !== 'string') return null;
      return { id: decoded.id, n: typeof decoded.n === 'string' ? decoded.n : '' };
    } catch (error) {
      logger.warn('Falha na verificação de token JWT', {
        module: 'AUTH',
        error: error instanceof Error ? error.message : error
      });
      return null;
    }
  }
}

export default new AuthService();
