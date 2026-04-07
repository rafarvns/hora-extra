import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import logger from '../../utils/Logger.js';

interface JoinRoomData {
    roomId: string;
    playerName: string;
}

/**
 * JoinRoomHandler: Evento 'join_room' via UDP.
 */
export class JoinRoomHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: JoinRoomData): Promise<void> {
        const { roomId, playerName } = data;

        if (!roomId || !playerName) {
            logger.warn(`Dados inválidos no JoinRoom de ${rinfo.address}`, { module: 'UDP_SOCKET' });
            return;
        }

        const session = server.getSession(rinfo);
        if (!session) {
            return;
        }

        // Atualizar sessão com sala e nome
        session.roomId = roomId;
        session.playerName = playerName;
        session.lastSeen = Date.now();

        logger.info(`Jogador ${playerName} entrou na sala: ${roomId} (UDP)`, { 
            module: 'UDP_SOCKET',
            playerId: session.id,
            roomId 
        });

        // Notificar outros jogadores na sala (Broadcast)
        server.broadcastToRoom(roomId, 'player_joined', {
            id: session.id,
            name: playerName
        }, rinfo);

        // Confirmar entrada para o remetente
        server.sendTo(rinfo, 'room_joined', {
            roomId,
            playerId: session.id,
            message: `Bem-vindo à sala ${roomId} (UDP Protocol)`
        });
    }
}
