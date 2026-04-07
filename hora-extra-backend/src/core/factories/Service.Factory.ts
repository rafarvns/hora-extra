import authService from '../../services/authService.js';
import prisma from '../../database/prisma.js';
import { PrismaClient } from '@prisma/client';

/**
 * ServiceFactory: Centraliza a obtenção de instâncias dos serviços do sistema.
 * 
 * Este padrão permite desacoplar a chamada no controlador da implementação concreta 
 * do serviço, facilitando mocks em testes e gerência centralizada de dependências.
 */
export class ServiceFactory {
    /**
     * Retorna a instância singleton do AuthService.
     */
    public static getAuthService() {
        return authService;
    }

    /**
     * Retorna o cliente do banco de dados (Prisma).
     * Embora o Prisma já seja uma factory internamente, o ServiceFactory pode envolver 
     * acessos ao banco para padronizar logs ou auditoria se necessário.
     */
    public static getPrismaClient(): PrismaClient {
        return prisma;
    }

    /* 
     * Exemplo de novos serviços:
     * public static getUserService() { return userService; }
     */
}
