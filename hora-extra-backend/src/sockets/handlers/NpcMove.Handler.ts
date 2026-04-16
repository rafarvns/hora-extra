import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { INpcMovePayload } from '../types/NpcMovePayload.js';
import logger from '../../utils/Logger.js';

/**
 * NpcMoveHandler: Trata a atualização de posição de NPCs/Bosses vindas de um cliente.
 * O cliente solicita o movimento (`npc_move_request`) e o servidor replica (`npc_move`).
 */
export class NpcMoveHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: INpcMovePayload): Promise<void> {
        // 1. Validação básica
        if (!data || !data.id || !Array.isArray(data.p) || data.p.length !== 3 || typeof data.r !== 'number') {
            return;
        }

        // 2. Recuperar Sessão do Remetente (O cliente que "controla" o NPC)
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            return;
        }

        // Log de debug para cada pacote (visível se LOG_LEVEL=debug)
        logger.debug(`[NPC_MOVE_REQ] NPC '${data.id}' movendo para [${data.p.map(n => n.toFixed(2))}] do cliente ${session.id}`, { module: 'UDP_SOCKET' });

        // 3. Broadcast para todos na sala (Incluindo o emissor, pois ele só se move no retorno)
        const payload = {
            id: data.id,
            p: data.p,
            r: data.r
        };

        server.broadcastToRoom(session.roomId, 'npc_move', payload);
        
        // Incrementar contador de pacotes
        session.movePacketCount++;

        // Log periódico para NPCs (mais frequente para facilitar debug de sync)
        if (session.movePacketCount % 20 === 0) {
            logger.info(`[NPC_SYNC] Replicando movimento do NPC '${data.id}' na sala '${session.roomId}' (Pos: ${data.p.map(n => n.toFixed(1))})`, { module: 'UDP_SOCKET' });
        }
    }
}
