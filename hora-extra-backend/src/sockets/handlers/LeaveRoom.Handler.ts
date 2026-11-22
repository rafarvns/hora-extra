import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';

/**
 * Payload esperado: nenhum.
 *
 * LeaveRoomHandler: o jogador saiu (voltou ao menu ou fechou o jogo). Libera a vaga
 * na hora, sem esperar o timeout de inatividade.
 *
 * Direção: C→S (evento: leave_room)
 * Efeito: player_disconnected + room_roster para quem ficou; tarefa aberta volta ao pool.
 */
export class LeaveRoomHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, _data: unknown): Promise<void> {
        server.removeSessionAt(rinfo, 'leave_room');
    }
}
