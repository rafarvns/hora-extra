import { Server, Socket } from 'socket.io';
import { ISocketHandler } from '../types/SocketEvent.js';

interface JoinRoomData {
    roomId: string;
    playerName: string;
}

/**
 * Handle: Evento 'join_room'
 * Permite que um jogador entre em uma sala específica.
 */
export class JoinRoomHandler implements ISocketHandler {
    public async handle(socket: Socket, io: Server, data: JoinRoomData): Promise<void> {
        const { roomId, playerName } = data;

        if (!roomId || !playerName) {
            console.warn(`[SOCKET] Dados inválidos recebidos no JoinRoom do cliente: ${socket.id}`);
            return;
        }

        // Entrar na sala
        socket.join(roomId);
        console.log(`[SOCKET] Jogador ${playerName} (${socket.id}) entrou na sala: ${roomId} [VIA FACTORY-HANDLER]`);

        // Notificar outros jogadores na sala
        socket.to(roomId).emit('player_joined', {
            id: socket.id,
            name: playerName
        });

        // Confirmar entrada para o remetente
        socket.emit('room_joined', {
            roomId,
            playerId: socket.id,
            message: `Bem-vindo à sala ${roomId}`
        });
    }
}
