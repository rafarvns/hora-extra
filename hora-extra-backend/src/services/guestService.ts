import { randomUUID } from 'crypto';
import authService from './authService.js';
import logger from '../utils/Logger.js';
import { ApiError } from '../core/ApiError.js';

export interface GuestJoinResult {
    token: string;
    guestId: string;
    roomId: string;
}

/**
 * GuestService: gera identidade anônima e JWT para acesso guest sem cadastro.
 * A sala guest é sempre "guest-room" (in-memory, sem persistência Prisma).
 */
export class GuestService {

    public async joinAsGuest(): Promise<GuestJoinResult> {
        try {
            const shortId = randomUUID().slice(0, 8);
            const guestId = `guest-${shortId}`;
            const roomId = 'guest-room';

            const token = authService.generateToken(guestId);

            logger.info(`Guest criado: ${guestId} → sala ${roomId}`, { module: 'GUEST_AUTH' });

            return { token, guestId, roomId };
        } catch (err) {
            logger.error('Falha ao criar guest', { module: 'GUEST_AUTH', error: err });
            throw ApiError.internal('Não foi possível criar sessão guest');
        }
    }
}

export default new GuestService();
