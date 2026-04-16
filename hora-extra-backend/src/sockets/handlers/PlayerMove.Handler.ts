import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { IPlayerMovePayload } from '../types/MovePayload.js';
import logger from '../../utils/Logger.js';

/**
 * PlayerMoveHandler: Trata a atualização de posição e rotação de um jogador via UDP.
 */
export class PlayerMoveHandler implements ISocketHandler {
    /**
     * @param server Instância do Gerenciador UDP
     * @param rinfo Detalhes do remetente (IP/Porta)
     * @param data Payload de movimento { p, r }
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

        // 3. Atualizar estado na sessão (State cache)
        session.lastPosition = data.p;
        session.lastRotation = data.r;
        session.lastSeen = Date.now();
        session.movePacketCount++;

        // Log não poluído: a cada 50 pacotes de movimento
        if (session.movePacketCount % 50 === 0) {
            logger.info(`[UDP_TRAFFIC] Movimento recebido de ${session.id}: POS [${data.p.map(n => n.toFixed(1))}]`, { module: 'UDP_SOCKET' });
        }

        // 4. Broadcast para TODOS na mesma sala (Incluindo o emissor se quiser sincronização absoluta)
        const payload = {
            id: session.id,
            p: data.p,
            r: data.r
        };

        server.broadcastToRoom(session.roomId, 'player_move', payload, rinfo);
    }
}
