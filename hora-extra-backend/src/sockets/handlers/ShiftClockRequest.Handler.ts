import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import logger from '../../utils/Logger.js';

/**
 * Payload esperado: {} (sem campos — sala vem da sessão)
 *
 * ShiftClockRequestHandler: jogador pede o relógio do expediente da sala.
 *
 * Direção: C→S (evento: shift_clock_request)
 * Resposta: S→C (evento: shift_clock) — broadcast para a sala quando este pedido
 * iniciou o expediente; senão, só para o remetente (sincronização/correção de drift).
 */
export class ShiftClockRequestHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, _data: unknown): Promise<void> {
        const session = server.getSession(rinfo);
        if (!session) {
            logger.warn(`[shift_clock_request] sessão não encontrada para ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Not authenticated. Send CONN first.' });
            return;
        }

        if (!session.roomId) {
            logger.warn(`[shift_clock_request] sessão sem roomId de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            server.sendTo(rinfo, 'ERROR', { message: 'Not in a room. Send join_room first.' });
            return;
        }

        const clock = ServiceFactory.getShiftClockService();
        const startedNow = clock.startShift(session.roomId);
        const snapshot = clock.getSnapshot(session.roomId);

        if (startedNow) {
            server.broadcastToRoom(session.roomId, 'shift_clock', snapshot);
            logger.info(`[UDP_SOCKET] shift_clock: expediente iniciado na sala '${session.roomId}' por '${session.id}'`, { module: 'UDP_SOCKET' });
            return;
        }

        server.sendTo(rinfo, 'shift_clock', snapshot);
    }
}
