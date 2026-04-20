import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import { UdpSocketManager } from '../UdpSocketManager.js';

/**
 * PingHandler: Responde a eventos de ping do cliente com um pong immediately.
 * Usado para cálculo de latência (RTT) no cliente.
 */
export class PingHandler implements ISocketHandler {
    public async handle(manager: UdpSocketManager, rinfo: RemoteInfo, data: any): Promise<void> {
        // Apenas devolve os dados (geralmente contém um timestamp 't')
        // O evento de resposta deve ser 'pong'
        manager.sendTo(rinfo, 'pong', data);
    }
}
