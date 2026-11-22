import { randomUUID } from 'crypto';
import authService from './authService.js';
import { normalizarNome } from './RoomRegistry.js';
import { ApiError } from '../core/ApiError.js';
import logger from '../utils/Logger.js';

export const NOME_JOGADOR_MAX = 16;

export interface SessionResult {
    token: string;
    playerId: string;
    nome: string;
}

/**
 * SessionService: o jogador só digita o nome. O servidor gera um id e devolve um token
 * com id + nome, usado no CONN do UDP. Sem cadastro e sem banco.
 */
export class SessionService {
    public create(nomeBruto: unknown): SessionResult {
        const nome = normalizarNome(nomeBruto, NOME_JOGADOR_MAX);
        if (!nome) {
            throw ApiError.badRequest(`Digite um nome de 2 a ${NOME_JOGADOR_MAX} caracteres.`);
        }

        const playerId = `p-${randomUUID().slice(0, 12)}`;
        const token = authService.generateToken(playerId, nome);
        logger.info(`Sessão criada: ${playerId} (${nome})`, { module: 'SESSION' });
        return { token, playerId, nome };
    }
}

export default new SessionService();
