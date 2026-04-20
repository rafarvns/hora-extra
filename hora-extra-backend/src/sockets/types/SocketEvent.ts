import { RemoteInfo } from 'dgram';

/**
 * Interface obrigatória para todo Socket Handler (UDP).
 * Cada classe de handler deve implementar o método 'handle'.
 * O parâmetro 'server' é passado como any para evitar dependência circular 
 * com UdpSocketManager.
 */
export interface ISocketHandler {
    handle(
        server: any, 
        remoteInfo: RemoteInfo, 
        data: any
    ): void | Promise<void>;
}

/**
 * Tipo que representa a classe (construtor) de um handler.
 */
export type SocketHandlerConstructor = new () => ISocketHandler;
