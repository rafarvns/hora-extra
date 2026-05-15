import prisma from '../database/prisma.js';
import logger from '../utils/Logger.js';

export const GUEST_ROOM_ID = 'guest-room';

/**
 * Garante que a sala dos guests existe no banco no startup do servidor.
 * Idempotente — pode ser chamado várias vezes sem efeitos colaterais.
 *
 * A sala persistida no MySQL é apenas pra aparecer no lobby (GET /api/rooms).
 * O estado real (sessions, NPCs) vive in-memory no UdpSocketManager.
 *
 * Retry exponencial pra tolerar o caso de Docker compose subir o `app` antes do `db`
 * estar pronto pra aceitar conexões.
 */
export async function seedGuestRoom(): Promise<void> {
    const maxAttempts = 8;
    const baseDelayMs = 2000;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
        try {
            await prisma.room.upsert({
                where: { id: GUEST_ROOM_ID },
                create: {
                    id: GUEST_ROOM_ID,
                    nome: 'Sala dos Convidados',
                    hostId: 'SYSTEM',
                    playerCount: 0,
                    maxPlayers: 99,
                    status: 'OPEN',
                },
                update: {
                    status: 'OPEN',
                },
            });
            logger.info(`Sala guest "${GUEST_ROOM_ID}" garantida no banco (tentativa ${attempt}).`, { module: 'BOOTSTRAP' });
            return;
        } catch (err) {
            const msg = err instanceof Error ? err.message : String(err);
            if (attempt < maxAttempts) {
                const delay = baseDelayMs * attempt;
                logger.warn(`Seed guest-room falhou (tentativa ${attempt}/${maxAttempts}) — retry em ${delay}ms. Erro: ${msg.split('\n')[0]}`, { module: 'BOOTSTRAP' });
                await new Promise(resolve => setTimeout(resolve, delay));
            } else {
                logger.error('Seed guest-room falhou definitivamente — lobby não listará a sala', { module: 'BOOTSTRAP', error: msg });
            }
        }
    }
}
