import authService from '../../services/authService.js';
import sessionService from '../../services/sessionService.js';
import roomRegistry from '../../services/RoomRegistry.js';
import taskService from '../../services/TaskService.js';
import shiftClockService from '../../services/ShiftClockService.js';
import prisma from '../../database/prisma.js';
import { PrismaClient } from '@prisma/client';

/**
 * ServiceFactory: Centraliza a obtenção de instâncias dos serviços do sistema.
 *
 * Este padrão permite desacoplar a chamada no controlador da implementação concreta
 * do serviço, facilitando mocks em testes e gerência centralizada de dependências.
 */
export class ServiceFactory {
    /** Emissão/verificação do token de sessão. */
    public static getAuthService() {
        return authService;
    }

    /** Entrada no jogo só com o nome. */
    public static getSessionService() {
        return sessionService;
    }

    /** Salas de jogo em memória (limite de jogadores, lista, expiração). */
    public static getRoomRegistry() {
        return roomRegistry;
    }

    /** Tarefas compartilhadas da sala. */
    public static getTaskService() {
        return taskService;
    }

    /** Relógio do expediente. */
    public static getShiftClockService() {
        return shiftClockService;
    }

    /**
     * Retorna o cliente do banco de dados (Prisma). Hoje sem uso pelo jogo (salas e
     * sessões vivem em memória); mantido para usos futuros.
     */
    public static getPrismaClient(): PrismaClient {
        return prisma;
    }
}
