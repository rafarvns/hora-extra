import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { INpcRegisterPayload } from '../types/NpcMovePayload.js';
import logger from '../../utils/Logger.js';

/** NPC conhecido numa sala: quem é o mestre (autoridade da IA) e em que andar ele vive. */
interface NpcInfo {
    id: string;
    type: string;
    floor: number;
    master: string | null;
}

/**
 * NpcRegisterHandler: um cliente "registra" um NPC que existe na cena dele.
 *
 * O servidor escolhe UM mestre por NPC: um jogador que esteja no mesmo andar do NPC
 * (é na cena dele que a IA roda). Quando o mestre sai da sala ou troca de andar, o
 * mestre passa para outro jogador daquele andar (npc_registered com isMaster:true só
 * para ele); se não houver ninguém, o próximo que registrar assume.
 */
export class NpcRegisterHandler implements ISocketHandler {
    /** chave `roomId:npcId` → info do NPC. */
    private static npcs = new Map<string, NpcInfo>();

    /** Limpa todos os NPCs vinculados a uma sala. */
    public static clearRoomState(roomId: string): void {
        for (const key of [...this.npcs.keys()]) {
            if (key.startsWith(`${roomId}:`)) this.npcs.delete(key);
        }
        logger.info(`[NPC_SYNC] Estado de NPCs limpo para a sala: ${roomId}`, { module: 'UDP_SOCKET' });
    }

    /** Só para testes. */
    public static getMaster(roomId: string, npcId: string): string | null {
        return this.npcs.get(`${roomId}:${npcId}`)?.master ?? null;
    }

    public async handle(server: any, rinfo: RemoteInfo, data: INpcRegisterPayload): Promise<void> {
        if (!data || !data.id || !data.type) return;

        const session = server.getSession(rinfo);
        if (!session || !session.roomId) return;

        const floor = typeof data.f === 'number' ? data.f : (session.floor ?? 1);
        const key = `${session.roomId}:${data.id}`;
        let npc = NpcRegisterHandler.npcs.get(key);
        if (!npc) {
            npc = { id: data.id, type: data.type, floor, master: null };
            NpcRegisterHandler.npcs.set(key, npc);
        }

        // O mestre atual só continua se ainda estiver na sala e no andar do NPC.
        if (npc.master && npc.master !== session.id) {
            const atual = NpcRegisterHandler.sessionsIn(server, session.roomId).find((s: any) => s.id === npc!.master);
            if (!atual || (atual.floor ?? 1) !== npc.floor) npc.master = null;
        }

        if (!npc.master) {
            npc.master = session.id;
            logger.info(`[NPC_SYNC] Cliente ${session.id} atribuído como MASTER do NPC '${data.id}'`, { module: 'UDP_SOCKET' });
        }

        const isMaster = npc.master === session.id;

        // Só quem registrou recebe a resposta. Avisar os outros (como antes) rebaixava o
        // mestre atual (isMaster:false) e puxava o NPC dele de volta à posição inicial
        // de quem acabou de registrar.
        server.sendTo(rinfo, 'npc_registered', { ...data, isMaster });
    }

    /** O jogador saiu da sala: os NPCs de que ele era mestre passam para outro. */
    public static onPlayerGone(server: any, roomId: string, playerId: string): void {
        for (const [key, npc] of this.npcs) {
            if (key.startsWith(`${roomId}:`) && npc.master === playerId) {
                this.reassign(server, roomId, npc, playerId);
            }
        }
    }

    /** O jogador trocou de andar: deixa de ser mestre dos NPCs do andar antigo. */
    public static onPlayerFloorChanged(server: any, roomId: string, playerId: string, novoAndar: number): void {
        for (const [key, npc] of this.npcs) {
            if (key.startsWith(`${roomId}:`) && npc.master === playerId && npc.floor !== novoAndar) {
                this.reassign(server, roomId, npc, playerId);
            }
        }
    }

    private static reassign(server: any, roomId: string, npc: NpcInfo, excluir: string): void {
        const candidato = this.sessionsIn(server, roomId)
            .find((s: any) => s.id !== excluir && (s.floor ?? 1) === npc.floor);

        npc.master = candidato ? candidato.id : null;
        if (!candidato) {
            logger.info(`[NPC_SYNC] NPC '${npc.id}' sem mestre (ninguém no andar ${npc.floor})`, { module: 'UDP_SOCKET' });
            return;
        }

        server.sendTo({ address: candidato.address, port: candidato.port }, 'npc_registered', {
            id: npc.id,
            type: npc.type,
            isMaster: true,
        });
        logger.info(`[NPC_SYNC] MASTER do NPC '${npc.id}' passou para ${candidato.id}`, { module: 'UDP_SOCKET' });
    }

    private static sessionsIn(server: any, roomId: string): any[] {
        return typeof server.getRoomSessions === 'function' ? server.getRoomSessions(roomId) : [];
    }
}
