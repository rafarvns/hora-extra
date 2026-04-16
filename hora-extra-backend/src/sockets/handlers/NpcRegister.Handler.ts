import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { INpcRegisterPayload } from '../types/NpcMovePayload.js';
import logger from '../../utils/Logger.js';

/**
 * NpcRegisterHandler: Um cliente "registra" um NPC que existe localmente na sua cena.
 * O servidor decide se esse cliente será o "Master" (autoridade da IA) para esse NPC.
 */
export class NpcRegisterHandler implements ISocketHandler {
    // Mapa simples para rastrear quem é o mestre de cada NPC por sala
    // chave: roomId:npcId -> value: playerId
    private static npcMasters = new Map<string, string>();

    /**
     * Limpa todos os NPCs vinculados a uma sala.
     */
    public static clearRoomState(roomId: string): void {
        const keysToRemove: string[] = [];
        this.npcMasters.forEach((_, key) => {
            if (key.startsWith(`${roomId}:`)) {
                keysToRemove.push(key);
            }
        });

        keysToRemove.forEach(k => this.npcMasters.delete(k));
        logger.info(`[NPC_SYNC] Estado de NPCs limpo para a sala: ${roomId}`, { module: 'UDP_SOCKET' });
    }

    public async handle(server: any, rinfo: RemoteInfo, data: INpcRegisterPayload): Promise<void> {
        if (!data || !data.id || !data.type) return;

        const session = server.getSession(rinfo);
        if (!session || !session.roomId) return;

        const masterKey = `${session.roomId}:${data.id}`;
        
        // Se ainda não houver um mestre para este NPC na sala, este cliente assume
        if (!NpcRegisterHandler.npcMasters.has(masterKey)) {
            NpcRegisterHandler.npcMasters.set(masterKey, session.id);
            logger.info(`[NPC_SYNC] Cliente ${session.id} atribuído como MASTER do NPC '${data.id}'`, { module: 'UDP_SOCKET' });
        }

        const isMaster = NpcRegisterHandler.npcMasters.get(masterKey) === session.id;

        // 1. Notificar o cliente se ele é o mestre
        server.sendTo(rinfo, 'npc_registered', {
            ...data,
            isMaster: isMaster
        });

        // 2. Broadcast para os outros saberem que o NPC está presente
        server.broadcastToRoom(session.roomId, 'npc_registered', {
            ...data,
            isMaster: false // Para os outros, ele nunca é mestre
        }, rinfo);
    }
}

/**
 * Nota: Em um sistema real, o npcMasters precisaria ser limpo quando o jogador desconecta.
 * Para este protótipo, focaremos na estabilização do movimento.
 */
