import { Server, Socket } from 'socket.io';

/**
 * Interface obrigatória para todo Socket Handler.
 * Cada classe de handler deve implementar o método 'handle'.
 */
export interface ISocketHandler {
    handle(socket: Socket, io: Server, data?: any): void | Promise<void>;
}

/**
 * Tipo que representa a classe (construtor) de um handler.
 */
export type SocketHandlerConstructor = new () => ISocketHandler;
