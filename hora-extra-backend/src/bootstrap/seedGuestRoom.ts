import prisma from '../database/prisma.js';
import logger from '../utils/Logger.js';

export const GUEST_ROOM_ID = 'guest-room';

/**
 * Garante que a sala dos guests existe no banco no startup do servidor.
 * Idempotente — pode ser chamado várias vezes sem efeitos colaterais.
 *
 * A sala persistida no MySQL é apenas pra aparecer no lobby (GET /api/rooms).
 * O estado real (sessions, NPCs) vive in-memory no UdpSocketManager.
 */
export async function seedGuestRoom(): Promise<void> {
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
        logger.info(`Sala guest "${GUEST_ROOM_ID}" garantida no banco.`, { module: 'BOOTSTRAP' });
    } catch (err) {
        logger.error('Falha ao seedar guest-room — lobby pode não listar a sala', {
            module: 'BOOTSTRAP',
            error: err instanceof Error ? err.message : err,
        });
    }
}
