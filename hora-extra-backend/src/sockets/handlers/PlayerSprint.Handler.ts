import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import logger from '../../utils/Logger.js';

/**
 * PlayerSprintHandler: Trata a atualização do estado de corrida (Sprint) de um jogador.
 */
export class PlayerSprintHandler implements ISocketHandler {
    /**
     * @param server Instância do Gerenciador UDP
     * @param rinfo Detalhes do remetente (IP/Porta)
     * @param data Payload { s: boolean }
     */
    public async handle(server: any, rinfo: RemoteInfo, data: { s: boolean }): Promise<void> {
        // 1. Validação básica do payload
        if (!data || typeof data.s !== 'boolean') {
            return;
        }

        // 2. Recuperar Sessão
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            return;
        }

        // 3. Atualizar estado na sessão
        session.isSprinting = data.s;

        // 4. Broadcast para TODOS na mesma sala (exceto o próprio para economizar banda, 
        // já que o cliente local já sabe que está correndo)
        const payload = {
            id: session.id,
            s: data.s
        };

        server.broadcastToRoom(session.roomId, 'player_sprint', payload, rinfo);
        
        logger.debug(`[UDP_SOCKET] Jogador ${session.id} sprint: ${data.s ? 'ATIVO' : 'INATIVO'}`);
    }
}
