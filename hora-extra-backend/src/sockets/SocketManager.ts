import { Server, Socket } from 'socket.io';
import { Server as HttpServer } from 'http';

/**
 * SocketManager: Singleton para gerenciar as conexões Socket.IO
 * Centraliza a lógica de emissão e recebimento de eventos globais e por sala.
 */
export class SocketManager {
    private static instance: SocketManager;
    private io: Server;

    private constructor(server: HttpServer) {
        this.io = new Server(server, {
            cors: {
                origin: "*", // Ajustar para o domínio real em produção
                methods: ["GET", "POST"]
            }
        });

        this.setupEventListeners();
    }

    public static initialize(server: HttpServer): SocketManager {
        if (!SocketManager.instance) {
            SocketManager.instance = new SocketManager(server);
        }
        return SocketManager.instance;
    }

    public static getInstance(): SocketManager {
        if (!SocketManager.instance) {
            throw new Error("SocketManager must be initialized with a server before access.");
        }
        return SocketManager.instance;
    }

    private setupEventListeners(): void {
        this.io.on('connection', (socket: Socket) => {
            console.log(`[SOCKET] Novo cliente conectado: ${socket.id}`);

            // Enviar confirmação imediata
            socket.emit('connection_success', { id: socket.id, time: Date.now() });

            // Handler: Entrar em uma sala
            socket.on('join_room', (data: { roomId: string, playerName: string }) => {
                const { roomId, playerName } = data;
                socket.join(roomId);
                console.log(`[SOCKET] Jogador ${playerName} (${socket.id}) entrou na sala: ${roomId}`);

                // Notificar outros jogadores na sala
                socket.to(roomId).emit('player_joined', { id: socket.id, name: playerName });

                // Confirmar entrada para o remetente
                socket.emit('room_joined', {
                    roomId,
                    playerId: socket.id,
                    message: `Bem-vindo à sala ${roomId}`
                });
            });

            // Handler: Desconexão
            socket.on('disconnect', (reason) => {
                console.log(`[SOCKET] Cliente desconectado (${socket.id}). Motivo: ${reason}`);
                this.io.emit('player_disconnected', { id: socket.id });
            });

            // Handler: Ping de latência
            socket.on('ping', (data: { timestamp: number }) => {
                socket.emit('pong', { timestamp: data.timestamp });
            });
        });
    }

    /**
     * Helper para emitir eventos para uma sala específica
     */
    public emitToRoom(roomId: string, event: string, payload: any): void {
        this.io.to(roomId).emit(event, payload);
    }
}
