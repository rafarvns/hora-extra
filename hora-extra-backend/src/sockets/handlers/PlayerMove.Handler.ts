import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { IPlayerMovePayload } from '../types/MovePayload.js';
import { NpcRegisterHandler } from './NpcRegister.Handler.js';
import logger from '../../utils/Logger.js';

/** Andares válidos do prédio. */
const ANDAR_MIN = 1;
const ANDAR_MAX = 3;

/**
 * PlayerMoveHandler: Trata a atualização de posição, rotação e andar de um jogador via UDP.
 */
export class PlayerMoveHandler implements ISocketHandler {
    /**
     * @param server Instância do Gerenciador UDP
     * @param rinfo Detalhes do remetente (IP/Porta)
     * @param data Payload de movimento { p, r, f? }
     */
    public async handle(server: any, rinfo: RemoteInfo, data: IPlayerMovePayload): Promise<void> {
        // 1. Validação básica do payload
        if (!data || !Array.isArray(data.p) || data.p.length !== 3 || typeof data.r !== 'number') {
            return;
        }

        // 2. Recuperar Sessão
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            return;
        }

        // 3. Atualizar estado na sessão (State cache — usado no room_state de quem entra)
        session.lastPosition = data.p;
        session.lastRotation = data.r;
        session.lastSeen = Date.now();
        session.movePacketCount++;

        const andarValido = Number.isInteger(data.f) && data.f! >= ANDAR_MIN && data.f! <= ANDAR_MAX;
        if (andarValido && data.f !== session.floor) {
            session.floor = data.f;
            NpcRegisterHandler.onPlayerFloorChanged(server, session.roomId, session.id, data.f!);
        }

        // Log não poluído: a cada 50 pacotes de movimento
        if (session.movePacketCount % 50 === 0) {
            logger.info(`[UDP_TRAFFIC] Movimento recebido de ${session.id}: POS [${data.p.map(n => n.toFixed(1))}] andar ${session.floor}`, { module: 'UDP_SOCKET' });
        }

        // 4. Broadcast para os outros da sala (o andar vai junto para filtrar a visibilidade)
        const payload = {
            id: session.id,
            p: data.p,
            r: data.r,
            f: session.floor ?? 1,
        };

        server.broadcastToRoom(session.roomId, 'player_move', payload, rinfo);
    }
}
