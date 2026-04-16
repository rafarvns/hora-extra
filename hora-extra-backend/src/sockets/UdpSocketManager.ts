import dgram, { RemoteInfo, Socket as UdpSocket } from 'dgram';
import authService from '../services/authService.js';
import { SocketHandlerFactory } from './factories/SocketHandler.Factory.js';
import logger from '../utils/Logger.js';

interface PlayerSession {
    id: string; // ID do jogador (do banco de dados)
    address: string;
    port: number;
    roomId?: string;
    playerName?: string;
    lastPosition?: number[];
    lastRotation?: number;
    lastSeen: number;
    movePacketCount: number; // Contador para log não poluído
}

/**
 * UdpSocketManager: Gerencia comunicações via UDP Datagram.
 * Substitui o Socket.IO para maior performance em sincronização de movimento.
 */
export class UdpSocketManager {
    private static instance: UdpSocketManager;
    private server: UdpSocket;
    private sessions: Map<string, PlayerSession> = new Map(); // Key: "address:port"

    private constructor(port: number = 5001) {
        this.server = dgram.createSocket('udp4');

        this.server.on('error', (err) => {
            logger.error(`Erro no Servidor UDP: ${err.message}`, { module: 'UDP_SOCKET', error: err });
            this.server.close();
        });

        this.server.on('message', (msg, rinfo) => {
            this.handleMessage(msg, rinfo);
        });

        this.server.on('listening', () => {
            const address = this.server.address();
            logger.info(`Servidor UDP ouvindo em ${address.address}:${address.port}`, { module: 'UDP_SOCKET' });
        });

        this.server.bind(port);
        
        // Limpeza de sessões inativas (Timeout de 30 segundos)
        setInterval(() => this.cleanupSessions(), 10000);
    }

    public static initialize(port: number = 5001): UdpSocketManager {
        if (!UdpSocketManager.instance) {
            UdpSocketManager.instance = new UdpSocketManager(port);
        }
        return UdpSocketManager.instance;
    }

    public static getInstance(): UdpSocketManager {
        if (!UdpSocketManager.instance) {
            throw new Error("UdpSocketManager must be initialized before access.");
        }
        return UdpSocketManager.instance;
    }

    private async handleMessage(buffer: Buffer, rinfo: RemoteInfo): Promise<void> {
        try {
            const rawData = buffer.toString();
            const packet = JSON.parse(rawData);
            const { e: eventName, d: data, token } = packet;

            const sessionKey = `${rinfo.address}:${rinfo.port}`;
            let session = this.sessions.get(sessionKey);

            // 1. Autenticação/Handshake (Evento 'CONN')
            if (eventName === 'CONN') {
                await this.handleConnection(rinfo, token, data);
                return;
            }

            // 2. Verificar se a sessão existe para outros eventos
            if (!session) {
                // Se não houver sessão, enviar erro de não autenticado
                this.sendTo(rinfo, 'ERROR', { message: 'Not authenticated. Send CONN first.' });
                return;
            }

            // Atualizar heartbeat
            session.lastSeen = Date.now();

            // 3. Roteamento para Handlers
            const handler = SocketHandlerFactory.createHandler(eventName);
            if (handler) {
                if (eventName === 'player_move' && !session.roomId) {
                    // Log silencioso ou esporádico para evitar flood se estiver enviando muito rápido sem sala
                    if (session.movePacketCount % 100 === 0) {
                        logger.warn(`Movimento ignorado: Jogador ${session.id} ainda não entrou em uma sala.`, { module: 'UDP_SOCKET' });
                    }
                }
                await handler.handle(this, rinfo, data);
            } else {
                logger.warn(`Evento desconhecido recebido via UDP: ${eventName}`, { module: 'UDP_SOCKET' });
            }
        } catch (error) {
            logger.error(`Erro ao processar datagrama de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET', error });
        }
    }

    private async handleConnection(rinfo: RemoteInfo, token: string, data: any): Promise<void> {
        const sessionKey = `${rinfo.address}:${rinfo.port}`;

        if (!token) {
            this.sendTo(rinfo, 'CONN_ERROR', { message: 'Token missing.' });
            return;
        }

        // --- BYPASS DE TESTE ---
        const isDev = process.env.NODE_ENV === 'development';
        const devToken = process.env.DEV_TEST_TOKEN;
        const devPlayerId = process.env.DEV_TEST_USER_ID || 'dev-test-player';

        let playerId: string;

        if (isDev && devToken && token === devToken) {
            playerId = devPlayerId;
        } else {
            const decoded = authService.verifyToken(token);
            if (!decoded) {
                this.sendTo(rinfo, 'CONN_ERROR', { message: 'Invalid token.' });
                return;
            }
            playerId = decoded.id;
        }

        // Criar ou atualizar sessão
        const session: PlayerSession = {
            id: playerId,
            address: rinfo.address,
            port: rinfo.port,
            playerName: data?.playerName || (isDev && token === devToken ? "DevPlayer" : "Player"),
            lastSeen: Date.now(),
            movePacketCount: 0,
            // AUTO-JOIN em sala de teste se for sessão de desenvolvimento
            roomId: (isDev && token === devToken) ? "dev-room" : undefined
        };

        this.sessions.set(sessionKey, session);

        logger.info(`Sessão UDP iniciada: ${playerId} em ${sessionKey} ${session.roomId ? `(Auto-Join: ${session.roomId})` : ''}`, { module: 'UDP_SOCKET' });

        this.sendTo(rinfo, 'CONN_SUCCESS', { id: playerId, sessionKey });
        
        // Se for auto-join, confirmar para o cliente também
        if (session.roomId) {
            this.sendTo(rinfo, 'room_joined', {
                roomId: session.roomId,
                playerId: session.id,
                message: "Auto-joined dev room."
            });
        }
    }

    /**
     * Envia um datagrama para um destino específico.
     */
    public sendTo(rinfo: { address: string; port: number }, eventName: string, data: any): void {
        const packet = JSON.stringify({ e: eventName, d: data });
        const message = Buffer.from(packet);
        this.server.send(message, rinfo.port, rinfo.address, (err) => {
            if (err) logger.error(`Erro ao enviar UDP para ${rinfo.address}: ${err.message}`);
        });
    }

    /**
     * Broadcast para todos os jogadores em uma sala específica.
     */
    public broadcastToRoom(roomId: string, eventName: string, data: any, exceptRinfo?: RemoteInfo): void {
        this.sessions.forEach(session => {
            if (session.roomId === roomId) {
                if (exceptRinfo && session.address === exceptRinfo.address && session.port === exceptRinfo.port) {
                    return;
                }
                this.sendTo({ address: session.address, port: session.port }, eventName, data);
            }
        });
    }

    /**
     * Retorna a sessão associada a um endereço remoto.
     */
    public getSession(rinfo: RemoteInfo): PlayerSession | undefined {
        return this.sessions.get(`${rinfo.address}:${rinfo.port}`);
    }

    private cleanupSessions(): void {
        const now = Date.now();
        const timeout = 30000; // 30 segundos de inatividade

        this.sessions.forEach((session, key) => {
            if (now - session.lastSeen > timeout) {
                logger.info(`Sessão expirada: ${session.id} (${key})`, { module: 'UDP_SOCKET' });
                this.sessions.delete(key);
            }
        });
    }

    /**
     * Fecha o servidor UDP.
     */
    public close(): void {
        this.server.close();
    }
}
