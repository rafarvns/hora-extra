import dgram, { RemoteInfo, Socket as UdpSocket } from 'dgram';
import authService from '../services/authService.js';
import { SocketHandlerFactory } from './factories/SocketHandler.Factory.js';
import { NpcRegisterHandler } from './handlers/NpcRegister.Handler.js';
import { ServiceFactory } from '../core/factories/Service.Factory.js';
import logger from '../utils/Logger.js';

export interface PlayerSession {
    id: string; // ID do jogador (gerado em POST /api/session)
    address: string;
    port: number;
    roomId: string;
    playerName: string;
    lastPosition?: number[];
    lastRotation?: number;
    /** Andar atual (1/2/3). Todos começam no 1º. */
    floor: number;
    lastSeen: number;
    movePacketCount: number; // Contador para log não poluído
    isSprinting?: boolean;
}

/** Códigos de recusa do CONN (CONN_ERROR.code). */
export type ConnErrorCode = 'INVALID_TOKEN' | 'ROOM_NOT_FOUND' | 'ROOM_FULL';

/** Inatividade (ms) que derruba uma sessão. */
const SESSION_TIMEOUT_MS = 30_000;

/**
 * UdpSocketManager: Gerencia comunicações via UDP Datagram.
 *
 * O primeiro pacote é sempre `CONN { token, d: { roomId } }`. O servidor confere o
 * token (id + nome do jogador), a sala e a vaga (máx. 2 por sala) antes de criar a
 * sessão. Quem entra recebe `room_state` com o retrato da sala (tarefas e jogadores).
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

        // Limpeza de sessões inativas e salas vazias
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

            // 1. Autenticação/Handshake (Evento 'CONN')
            if (eventName === 'CONN') {
                this.handleConnection(rinfo, token, data);
                return;
            }

            // 2. Verificar se a sessão existe para outros eventos
            const session = this.sessions.get(this.keyOf(rinfo));
            if (!session) {
                this.sendTo(rinfo, 'ERROR', { message: 'Not authenticated. Send CONN first.' });
                return;
            }

            // Atualizar heartbeat
            session.lastSeen = Date.now();

            // 3. Roteamento para Handlers
            const handler = SocketHandlerFactory.createHandler(eventName);
            if (handler) {
                await handler.handle(this, rinfo, data);
            } else {
                logger.warn(`Evento desconhecido recebido via UDP: ${eventName}`, { module: 'UDP_SOCKET' });
            }
        } catch (error) {
            logger.error(`Erro ao processar datagrama de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET', error });
        }
    }

    private handleConnection(rinfo: RemoteInfo, token: string, data: any): void {
        const sessionKey = this.keyOf(rinfo);

        const claims = token ? authService.verifyToken(token) : null;
        if (!claims || !claims.n) {
            this.connError(rinfo, 'INVALID_TOKEN', 'Sessão inválida. Volte ao menu e entre de novo.');
            return;
        }

        const roomId = typeof data?.roomId === 'string' ? data.roomId : '';
        const registry = ServiceFactory.getRoomRegistry();
        const check = registry.canJoin(roomId, claims.id);
        if (check === 'ROOM_NOT_FOUND') {
            this.connError(rinfo, check, 'A sala não existe mais.');
            return;
        }
        if (check === 'ROOM_FULL') {
            this.connError(rinfo, check, 'A sala está cheia.');
            return;
        }

        // Reconexão: a mesma pessoa (mesmo playerId) substitui a sessão antiga sem ocupar
        // outra vaga e sem devolver a tarefa dela. Se a sessão antiga era de outra sala,
        // é uma saída normal daquela sala.
        let reconexao = false;
        for (const [key, s] of [...this.sessions.entries()]) {
            if (s.id !== claims.id) continue;
            if (s.roomId === roomId) {
                this.sessions.delete(key);
                reconexao = true;
            } else {
                this.removeSession(key, 'trocou de sala');
            }
        }
        // Outro jogador usando o mesmo endereço:porta (cliente reiniciado) sai antes.
        const ocupante = this.sessions.get(sessionKey);
        if (ocupante) this.removeSession(sessionKey, 'endereço reutilizado');

        const session: PlayerSession = {
            id: claims.id,
            address: rinfo.address,
            port: rinfo.port,
            roomId,
            playerName: claims.n,
            floor: 1,
            lastSeen: Date.now(),
            movePacketCount: 0,
        };
        this.sessions.set(sessionKey, session);
        registry.join(roomId, claims.id);

        logger.info(`Sessão UDP iniciada: ${claims.id} (${claims.n}) em ${sessionKey} → sala ${roomId}${reconexao ? ' [reconexão]' : ''}`, { module: 'UDP_SOCKET' });

        this.sendTo(rinfo, 'CONN_SUCCESS', { id: claims.id, sessionKey, roomId, n: claims.n });
        this.sendTo(rinfo, 'room_state', this.buildRoomState(roomId, claims.id));

        if (!reconexao) {
            this.broadcastToRoom(roomId, 'player_joined', { id: claims.id, name: claims.n }, rinfo);
        }
        this.broadcastRoster(roomId);
    }

    private connError(rinfo: RemoteInfo, code: ConnErrorCode, message: string): void {
        logger.info(`CONN recusado (${code}) para ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
        this.sendTo(rinfo, 'CONN_ERROR', { code, message });
    }

    /**
     * Retrato da sala para quem acabou de entrar: estado de todas as tarefas e os
     * outros jogadores com a última posição e andar conhecidos.
     */
    public buildRoomState(roomId: string, exceptPlayerId?: string) {
        const t = ServiceFactory.getTaskService().getRoomSnapshot(roomId);
        const pl = this.getRoomSessions(roomId)
            .filter(s => s.id !== exceptPlayerId)
            .map(s => ({
                id: s.id,
                n: s.playerName,
                p: s.lastPosition ?? null,
                r: s.lastRotation ?? 0,
                f: s.floor,
            }));
        return { t, pl };
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
    public broadcastToRoom(roomId: string, eventName: string, data: any, exceptRinfo?: { address: string; port: number }): void {
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
    public getSession(rinfo: { address: string; port: number }): PlayerSession | undefined {
        return this.sessions.get(this.keyOf(rinfo));
    }

    /** Sessões ativas de uma sala. */
    public getRoomSessions(roomId: string): PlayerSession[] {
        return [...this.sessions.values()].filter(s => s.roomId === roomId);
    }

    /**
     * Retorna o número de sessões ativas em uma sala.
     */
    public getRoomSessionCount(roomId: string): number {
        return this.getRoomSessions(roomId).length;
    }

    /**
     * Lista de jogadores de uma sala, no formato compacto do evento room_roster.
     */
    public getRoomRoster(roomId: string): Array<{ id: string; n: string }> {
        return this.getRoomSessions(roomId).map(s => ({ id: s.id, n: s.playerName }));
    }

    /**
     * Envia o roster atual para todos da sala (room_roster). Chamado sempre que a
     * composição muda: entrada (CONN) e saída (leave_room / expiração).
     */
    public broadcastRoster(roomId: string): void {
        const roster = this.getRoomRoster(roomId);
        if (roster.length === 0) return;
        this.broadcastToRoom(roomId, 'room_roster', { p: roster });
    }

    /** Saída pedida pelo próprio cliente (leave_room). */
    public removeSessionAt(rinfo: { address: string; port: number }, motivo: string): void {
        this.removeSession(this.keyOf(rinfo), motivo);
    }

    /**
     * Único caminho de saída de um jogador: avisa quem ficou, devolve a tarefa aberta
     * ao pool (e a entrega a quem estiver sem tarefa), troca o mestre dos NPCs e libera
     * a vaga na sala.
     */
    private removeSession(key: string, motivo: string): void {
        const session = this.sessions.get(key);
        if (!session) return;
        this.sessions.delete(key);

        const { roomId, id } = session;
        logger.info(`Jogador ${id} saiu da sala ${roomId} (${motivo})`, { module: 'UDP_SOCKET' });

        // A mesma pessoa pode ter outra sessão viva (reconectou por outra porta).
        const aindaNaSala = this.getRoomSessions(roomId).some(s => s.id === id);
        if (aindaNaSala) return;

        ServiceFactory.getRoomRegistry().leave(roomId, id);
        this.broadcastToRoom(roomId, 'player_disconnected', { id });
        this.broadcastRoster(roomId);

        NpcRegisterHandler.onPlayerGone(this, roomId, id);

        const taskService = ServiceFactory.getTaskService();
        const devolvidas = taskService.releasePlayer(id);
        if (devolvidas.length > 0) this.giveTasksToIdlePlayers(roomId);
    }

    /** Entrega uma tarefa livre a cada jogador da sala que está sem tarefa aberta. */
    private giveTasksToIdlePlayers(roomId: string): void {
        const taskService = ServiceFactory.getTaskService();
        for (const s of this.getRoomSessions(roomId)) {
            if (taskService.hasOpenTask(s.id)) continue;
            const proxima = taskService.advanceQueue(s.id);
            if (!proxima) continue;
            this.broadcastToRoom(roomId, 'task_assigned', {
                playerId: s.id,
                tasks: [proxima],
                totalTasks: taskService.getTotalTaskCount(s.id),
            });
            logger.info(`[UDP_SOCKET] task_assigned (devolvida): '${proxima.id}' para '${s.id}'`, { module: 'UDP_SOCKET' });
        }
    }

    private cleanupSessions(now: number = Date.now()): void {
        for (const [key, session] of [...this.sessions.entries()]) {
            if (now - session.lastSeen > SESSION_TIMEOUT_MS) {
                this.removeSession(key, 'inatividade');
            }
        }

        // Salas vazias há mais que o TTL morrem junto com o estado delas.
        for (const roomId of ServiceFactory.getRoomRegistry().collectExpired(now)) {
            this.clearRoomState(roomId);
        }
    }

    /** Descarta tarefas, relógio e NPCs de uma sala que deixou de existir. */
    private clearRoomState(roomId: string): void {
        NpcRegisterHandler.clearRoomState(roomId);
        ServiceFactory.getTaskService().clearRoom(roomId);
        ServiceFactory.getShiftClockService().resetRoom(roomId);
        logger.info(`[UDP_SOCKET] Estado da sala '${roomId}' descartado.`, { module: 'UDP_SOCKET' });
    }

    private keyOf(rinfo: { address: string; port: number }): string {
        return `${rinfo.address}:${rinfo.port}`;
    }

    /**
     * Fecha o servidor UDP.
     */
    public close(): void {
        this.server.close();
    }
}
