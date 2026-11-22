import { randomUUID } from 'crypto';
import { ApiError } from '../core/ApiError.js';
import logger from '../utils/Logger.js';

/** Máximo de jogadores distintos numa sala. */
export const MAX_PLAYERS_PER_ROOM = 2;

/** Tempo que uma sala vazia sobrevive (permite reconectar / o criador chegar). */
export const EMPTY_ROOM_TTL_MS = 60_000;

const NOME_MIN = 2;
const NOME_MAX_SALA = 24;

export interface Room {
    id: string;
    nome: string;
    createdAt: number;
    /** Jogadores conectados agora (playerIds distintos). */
    players: Set<string>;
    /** Desde quando a sala está vazia (null = tem gente). */
    emptySince: number | null;
    /** Alguém já entrou alguma vez — a partida está em andamento. */
    started: boolean;
}

/** Linha da lista de salas (GET /api/rooms). */
export interface RoomSummary {
    id: string;
    nome: string;
    jogadores: number;
    max: number;
    emAndamento: boolean;
}

export type JoinCheck = 'OK' | 'ROOM_NOT_FOUND' | 'ROOM_FULL';

/**
 * Normaliza um nome digitado pelo jogador (trim + espaços colapsados) e valida o tamanho.
 * Devolve null se inválido.
 */
export function normalizarNome(raw: unknown, max: number): string | null {
    if (typeof raw !== 'string') return null;
    const nome = raw.replace(/\s+/g, ' ').trim();
    if (nome.length < NOME_MIN || nome.length > max) return null;
    return nome;
}

/**
 * RoomRegistry: salas de jogo em memória. Fonte da verdade sobre quem está em qual
 * sala e se cabe mais um. Sem banco: uma sala existe enquanto tem gente (ou por
 * EMPTY_ROOM_TTL_MS depois de esvaziar).
 */
export class RoomRegistry {
    private rooms = new Map<string, Room>();

    public create(nomeBruto: unknown, now: number = Date.now()): Room {
        const nome = normalizarNome(nomeBruto, NOME_MAX_SALA);
        if (!nome) {
            throw ApiError.badRequest(`O nome da sala deve ter de ${NOME_MIN} a ${NOME_MAX_SALA} caracteres.`);
        }

        const id = `s-${randomUUID().slice(0, 8)}`;
        const room: Room = { id, nome, createdAt: now, players: new Set(), emptySince: now, started: false };
        this.rooms.set(id, room);
        logger.info(`RoomRegistry: sala '${nome}' (${id}) criada`, { module: 'ROOM' });
        return room;
    }

    public get(roomId: string): Room | undefined {
        return this.rooms.get(roomId);
    }

    /** Salas visíveis no menu, mais novas primeiro. */
    public list(): RoomSummary[] {
        return [...this.rooms.values()]
            .sort((a, b) => b.createdAt - a.createdAt)
            .map(r => ({
                id: r.id,
                nome: r.nome,
                jogadores: r.players.size,
                max: MAX_PLAYERS_PER_ROOM,
                emAndamento: r.started,
            }));
    }

    /** O jogador pode entrar? Quem já está na sala (reconexão) sempre pode. */
    public canJoin(roomId: string, playerId: string): JoinCheck {
        const room = this.rooms.get(roomId);
        if (!room) return 'ROOM_NOT_FOUND';
        if (room.players.has(playerId)) return 'OK';
        if (room.players.size >= MAX_PLAYERS_PER_ROOM) return 'ROOM_FULL';
        return 'OK';
    }

    public join(roomId: string, playerId: string): void {
        const room = this.rooms.get(roomId);
        if (!room) return;
        room.players.add(playerId);
        room.emptySince = null;
        room.started = true;
    }

    public leave(roomId: string, playerId: string, now: number = Date.now()): void {
        const room = this.rooms.get(roomId);
        if (!room) return;
        room.players.delete(playerId);
        if (room.players.size === 0) room.emptySince = now;
    }

    /**
     * Remove as salas vazias há mais de EMPTY_ROOM_TTL_MS e devolve os ids removidos
     * (quem chama limpa tarefas, relógio e NPCs dessas salas).
     */
    public collectExpired(now: number = Date.now()): string[] {
        const removidas: string[] = [];
        for (const room of this.rooms.values()) {
            if (room.emptySince !== null && now - room.emptySince > EMPTY_ROOM_TTL_MS) {
                removidas.push(room.id);
            }
        }
        for (const id of removidas) {
            this.rooms.delete(id);
            logger.info(`RoomRegistry: sala ${id} removida (vazia)`, { module: 'ROOM' });
        }
        return removidas;
    }
}

export default new RoomRegistry();
