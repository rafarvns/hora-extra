import { Response, NextFunction } from 'express';
import { ApiError } from '../core/ApiError.js';
import { AuthRequest } from '../types/AuthRequest.js'; // Vou criar esse arquivo types
import authService from '../services/authService.js';

/**
 * Middleware para validar o token JWT nas requisições.
 */
class AuthMiddleware {
  /**
   * Verifica se o token JWT está presente e é válido.
   */
  public authenticate = (req: AuthRequest, res: Response, next: NextFunction): void => {
    const authHeader = req.headers.authorization;

    if (!authHeader) {
      throw ApiError.unauthorized('Token de autenticação não fornecido.');
    }

    // O header deve ser: Authorization: Bearer <TOKEN>
    const parts = authHeader.split(' ');

    if (parts.length !== 2) {
      throw ApiError.unauthorized('Token com formato inválido.');
    }

    const [scheme, token] = parts;

    if (!/^Bearer$/i.test(scheme)) {
      throw ApiError.unauthorized('Token mal formatado.');
    }

    const decoded = authService.verifyToken(token);

    if (!decoded) {
      throw ApiError.unauthorized('Token inválido ou expirado.');
    }

    // Anexa o ID do jogador ao request para uso posterior
    req.jogadorId = decoded.id;

    return next();
  }
}

export default new AuthMiddleware();
