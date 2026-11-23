import { describe, it, expect, beforeEach, vi } from 'vitest';

vi.mock('../utils/Logger.js', () => ({
    default: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));

import { RoomRegistry, MAX_PLAYERS_PER_ROOM, EMPTY_ROOM_TTL_MS, normalizarNome } from './RoomRegistry.js';
import { ApiError } from '../core/ApiError.js';

describe('RoomRegistry', () => {
    let reg: RoomRegistry;
    beforeEach(() => { reg = new RoomRegistry(); });

    it('cria sala com id único e nome normalizado', () => {
        const a = reg.create('  Sala   do Rafa ');
        const b = reg.create('Outra');
        expect(a.nome).toBe('Sala do Rafa');
        expect(a.id).not.toBe(b.id);
        expect(reg.get(a.id)).toBe(a);
    });

    it('recusa nome de sala vazio, curto ou longo', () => {
        expect(() => reg.create('')).toThrow(ApiError);
        expect(() => reg.create('a')).toThrow(ApiError);
        expect(() => reg.create('x'.repeat(25))).toThrow(ApiError);
        expect(() => reg.create(42)).toThrow(ApiError);
    });

    it('limite de 2 jogadores distintos: o 3º recebe ROOM_FULL', () => {
        expect(MAX_PLAYERS_PER_ROOM).toBe(2);
        const r = reg.create('Sala');
        reg.join(r.id, 'A');
        reg.join(r.id, 'B');
        expect(reg.canJoin(r.id, 'C')).toBe('ROOM_FULL');
    });

    it('quem já está na sala (reconexão) pode entrar mesmo cheia', () => {
        const r = reg.create('Sala');
        reg.join(r.id, 'A');
        reg.join(r.id, 'B');
        expect(reg.canJoin(r.id, 'A')).toBe('OK');
    });

    it('sala inexistente → ROOM_NOT_FOUND', () => {
        expect(reg.canJoin('nada', 'A')).toBe('ROOM_NOT_FOUND');
    });

    it('a vaga abre quando alguém sai', () => {
        const r = reg.create('Sala');
        reg.join(r.id, 'A');
        reg.join(r.id, 'B');
        reg.leave(r.id, 'B');
        expect(reg.canJoin(r.id, 'C')).toBe('OK');
    });

    it('list mostra jogadores, máximo e se está em andamento', () => {
        const r = reg.create('Sala');
        expect(reg.list()).toEqual([{ id: r.id, nome: 'Sala', jogadores: 0, max: 2, emAndamento: false }]);
        reg.join(r.id, 'A');
        expect(reg.list()[0]).toMatchObject({ jogadores: 1, emAndamento: true });
    });

    it('sala vazia há mais que o TTL é removida; com gente, não', () => {
        const t0 = 1_000_000;
        const vazia = reg.create('Vazia', t0);
        const cheia = reg.create('Com gente', t0);
        reg.join(cheia.id, 'A');

        expect(reg.collectExpired(t0 + EMPTY_ROOM_TTL_MS - 1)).toEqual([]);
        expect(reg.collectExpired(t0 + EMPTY_ROOM_TTL_MS + 1)).toEqual([vazia.id]);
        expect(reg.get(vazia.id)).toBeUndefined();
        expect(reg.get(cheia.id)).toBeDefined();
    });

    it('o TTL recomeça quando o último jogador sai', () => {
        const t0 = 1_000_000;
        const r = reg.create('Sala', t0);
        reg.join(r.id, 'A');
        reg.leave(r.id, 'A', t0 + 10 * EMPTY_ROOM_TTL_MS);
        expect(reg.collectExpired(t0 + 10 * EMPTY_ROOM_TTL_MS + 1)).toEqual([]);
    });
});

describe('normalizarNome', () => {
    it('aceita 2–max caracteres e colapsa espaços', () => {
        expect(normalizarNome('  Ana  Maria ', 16)).toBe('Ana Maria');
    });

    it('recusa só espaços, curto, longo e não-string', () => {
        expect(normalizarNome('   ', 16)).toBeNull();
        expect(normalizarNome('A', 16)).toBeNull();
        expect(normalizarNome('x'.repeat(17), 16)).toBeNull();
        expect(normalizarNome(undefined, 16)).toBeNull();
    });
});
