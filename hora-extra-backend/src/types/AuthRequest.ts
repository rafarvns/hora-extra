import { Request } from 'express';

/**
 * Interface estendida do Request para incluir o jogador autenticado.
 */
export interface AuthRequest extends Request {
  jogadorId?: string;
}
