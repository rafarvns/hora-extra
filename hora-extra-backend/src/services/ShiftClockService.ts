import logger from '../utils/Logger.js';

/** Estado do expediente de uma sala. */
export type ShiftStatus = 'idle' | 'running' | 'ended';

/**
 * Snapshot enviado no evento `shift_clock` (chaves compactas).
 *
 * O servidor manda o minuto de jogo JÁ CALCULADO (`m`), e não timestamps: assim o
 * cliente não depende do próprio relógio estar sincronizado com o do servidor — ele
 * só extrapola `m + k · Δt` desde o recebimento.
 */
export interface ShiftClockSnapshot {
    /** Minuto de jogo atual (minutos desde 00:00, fracionário). */
    m: number;
    /** Minuto de início do expediente (480 = 08:00). */
    s: number;
    /** Minuto de fim do expediente (1080 = 18:00). */
    e: number;
    /** Minutos de jogo por segundo real (0 quando parado). */
    k: number;
    st: ShiftStatus;
}

const SHIFT_START_MINUTE = 8 * 60;
const SHIFT_END_MINUTE = 18 * 60;
const DEFAULT_DURATION_SECONDS = 900;

/**
 * ShiftClockService: relógio do expediente, autoritativo e por sala (in-memory).
 *
 * O expediente começa quando o primeiro jogador da sala pede o relógio
 * (`shift_clock_request`, enviado pelo cliente ao fim da intro) e corre em tempo
 * acelerado até 18:00. Ao chegar no fim fica em `ended` — consequências de
 * gameplay (hora extra, penalidades) ficam para planos futuros.
 */
export class ShiftClockService {
    /** roomId → instante real (ms) em que o expediente começou. */
    private startedAt = new Map<string, number>();

    /**
     * Duração real do expediente em segundos. Sem valor explícito, lê
     * `SHIFT_DURATION_SECONDS` no momento do uso (não na importação do módulo, que
     * pode acontecer antes do .env ser carregado).
     */
    constructor(private readonly durationSeconds?: number) {}

    private get gameMinutesPerSecond(): number {
        const raw = this.durationSeconds ?? Number(process.env.SHIFT_DURATION_SECONDS);
        const duration = Number.isFinite(raw) && raw > 0 ? raw : DEFAULT_DURATION_SECONDS;
        return (SHIFT_END_MINUTE - SHIFT_START_MINUTE) / duration;
    }

    /**
     * Inicia o expediente da sala. Idempotente: devolve `true` só quando de fato
     * iniciou agora (o handler usa isso para decidir entre broadcast e resposta direta).
     */
    public startShift(roomId: string, now: number = Date.now()): boolean {
        if (this.startedAt.has(roomId)) return false;

        this.startedAt.set(roomId, now);
        logger.info(`ShiftClockService.startShift: expediente iniciado na sala '${roomId}'`, { module: 'GAME' });
        return true;
    }

    public getSnapshot(roomId: string, now: number = Date.now()): ShiftClockSnapshot {
        const started = this.startedAt.get(roomId);
        if (started === undefined) {
            return { m: SHIFT_START_MINUTE, s: SHIFT_START_MINUTE, e: SHIFT_END_MINUTE, k: 0, st: 'idle' };
        }

        const elapsedSeconds = Math.max(0, now - started) / 1000;
        const minute = SHIFT_START_MINUTE + elapsedSeconds * this.gameMinutesPerSecond;

        if (minute >= SHIFT_END_MINUTE) {
            return { m: SHIFT_END_MINUTE, s: SHIFT_START_MINUTE, e: SHIFT_END_MINUTE, k: 0, st: 'ended' };
        }

        return { m: minute, s: SHIFT_START_MINUTE, e: SHIFT_END_MINUTE, k: this.gameMinutesPerSecond, st: 'running' };
    }

    /** Descarta o expediente da sala. Chamado em reset de sala e quando a guest-room esvazia. */
    public resetRoom(roomId: string): void {
        if (this.startedAt.delete(roomId)) {
            logger.info(`ShiftClockService.resetRoom: expediente da sala '${roomId}' descartado`, { module: 'GAME' });
        }
    }
}

export default new ShiftClockService();
