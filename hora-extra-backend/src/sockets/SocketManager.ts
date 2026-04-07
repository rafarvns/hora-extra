import { Server, Socket } from 'socket.io';
import { Server as HttpServer } from 'http';
import authService from '../services/authService.js';
import { SocketHandlerFactory } from './factories/SocketHandler.Factory.js';
import logger from '../utils/Logger.js';

interface SocketData {
    jogadorId: string;
}

/**
 * SocketManager: Singleton para gerenciar as conexões Socket.IO
 * Centraliza a lógica de emissão e recebimento de eventos globais e por sala.
 */
export class SocketManager {
    private static instance: SocketManager;
    private io: Server<any, any, any, SocketData>;

    private constructor(server: HttpServer) {
        this.io = new Server(server, {
            cors: {
                origin: "*", // Ajustar para o domínio real em produção
                methods: ["GET", "POST"]
            }
        });

        this.setupMiddleware();
        this.setupEventListeners();
    }

    /**
     * Middleware de Autenticação: Garante que o cliente enviou um token válido.
     */
    private setupMiddleware(): void {
        this.io.use((socket, next) => {
            // Tenta obter o token do handshake.auth (padrão v4) ou do query (fallback para clientes customizados)
            const token = socket.handshake.auth?.token || socket.handshake.query?.token;

            if (!token) {
                logger.warn(`Tentativa de conexão sem token: ${socket.id}`, { module: 'SOCKET' });
                return next(new Error('Authentication error: Token missing.'));
            }

            // --- BYPASS DE TESTE EM DESENVOLVIMENTO ---
            // Permite que o frontend use uma chave mestra para testes rápidos sem login.
            const isDev = process.env.NODE_ENV === 'development';
            const devToken = process.env.DEV_TEST_TOKEN;
            const devPlayerId = process.env.DEV_TEST_USER_ID || 'dev-test-player';

            if (isDev && devToken && token === devToken) {
                logger.info(`Sessão de TESTE iniciada: ${socket.id} (ID: ${devPlayerId})`, { module: 'SOCKET' });
                socket.data.jogadorId = devPlayerId;
                return next();
            }
            // ------------------------------------------

            const decoded = authService.verifyToken(token);

            if (!decoded) {
                logger.warn(`Token inválido para o cliente: ${socket.id}`, { module: 'SOCKET' });
                return next(new Error('Authentication error: Invalid or expired token.'));
            }

            // Anexar o ID do jogador ao dado do socket para uso posterior
            socket.data.jogadorId = decoded.id;
            next();
        });
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
            logger.info(`Cliente conectado: ${socket.id} (Jogador: ${socket.data.jogadorId})`, { 
                module: 'SOCKET',
                socketId: socket.id,
                jogadorId: socket.data.jogadorId 
            });

            // Enviar confirmação imediata
            socket.emit('connection_success', { id: socket.id, time: Date.now() });

            // Registrar todos os eventos definidos na Factory dinamicamente
            const events = SocketHandlerFactory.getRegisteredEvents();

            events.forEach(eventName => {
                socket.on(eventName, async (data: any) => {
                    const handler = SocketHandlerFactory.createHandler(eventName);
                    if (handler) {
                        try {
                            await handler.handle(socket, this.io, data);
                        } catch (error) {
                            logger.error(`Erro ao processar evento '${eventName}':`, { 
                                module: 'SOCKET', 
                                error, 
                                eventName, 
                                socketId: socket.id 
                            });
                        }
                    }
                });
            });

            // Handler: Desconexão (Mantido aqui por ser um evento lifecycle padrão)
            socket.on('disconnect', (reason) => {
                logger.info(`Cliente desconectado: ${socket.id} (Jogador: ${socket.data.jogadorId}). Motivo: ${reason}`, { 
                    module: 'SOCKET',
                    socketId: socket.id,
                    reason 
                });
                this.io.emit('player_disconnected', { id: socket.id });
            });

            // Handler: Ping de latência (Mantido aqui por ser utilitário básico)
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
