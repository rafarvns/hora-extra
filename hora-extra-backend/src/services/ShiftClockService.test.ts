import { describe, it, expect, beforeEach } from 'vitest';
import { ShiftClockService } from './ShiftClockService.js';

// 10h de jogo (08:00 → 18:00 = 600 min) em 600s reais → 1 min de jogo por segundo.
const DURATION_SECONDS = 600;
const T0 = 1_000_000;

describe('ShiftClockService', () => {
    let service: ShiftClockService;

    beforeEach(() => {
        service = new ShiftClockService(DURATION_SECONDS);
    });

    it('sala sem expediente iniciado devolve snapshot idle parado no início', () => {
        const snap = service.getSnapshot('r1', T0);

        expect(snap).toEqual({ m: 480, s: 480, e: 1080, k: 0, st: 'idle' });
    });

    it('startShift devolve true só na primeira chamada (idempotente)', () => {
        expect(service.startShift('r1', T0)).toBe(true);
        expect(service.startShift('r1', T0 + 5000)).toBe(false);
    });

    it('relógio avança conforme a escala configurada', () => {
        service.startShift('r1', T0);

        const snap = service.getSnapshot('r1', T0 + 90_000); // 90s reais

        expect(snap.st).toBe('running');
        expect(snap.k).toBeCloseTo(1);
        expect(snap.m).toBeCloseTo(480 + 90);
    });

    it('segundo startShift não reinicia o relógio', () => {
        service.startShift('r1', T0);
        service.startShift('r1', T0 + 60_000);

        expect(service.getSnapshot('r1', T0 + 60_000).m).toBeCloseTo(540);
    });

    it('depois do fim, fica em ended, limitado ao minuto final e com k=0', () => {
        service.startShift('r1', T0);

        const snap = service.getSnapshot('r1', T0 + (DURATION_SECONDS + 100) * 1000);

        expect(snap).toEqual({ m: 1080, s: 480, e: 1080, k: 0, st: 'ended' });
    });

    it('salas são independentes', () => {
        service.startShift('r1', T0);

        expect(service.getSnapshot('r2', T0 + 30_000).st).toBe('idle');
    });

    it('resetRoom permite começar de novo', () => {
        service.startShift('r1', T0);
        service.resetRoom('r1');

        expect(service.getSnapshot('r1', T0 + 30_000).st).toBe('idle');
        expect(service.startShift('r1', T0 + 30_000)).toBe(true);
        expect(service.getSnapshot('r1', T0 + 30_000).m).toBeCloseTo(480);
    });

    it('duração inválida cai no padrão de 900s', () => {
        const fallback = new ShiftClockService(0);
        fallback.startShift('r1', T0);

        expect(fallback.getSnapshot('r1', T0).k).toBeCloseTo(600 / 900);
    });
});
