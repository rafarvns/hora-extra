import { SocketHandlerConstructor, ISocketHandler } from '../types/SocketEvent.js';
// Handlers (serão adicionados conforme implementados)
import { JoinRoomHandler } from '../handlers/JoinRoom.Handler.js';
import { PlayerMoveHandler } from '../handlers/PlayerMove.Handler.js';
import { NpcMoveHandler } from '../handlers/NpcMove.Handler.js';
import { NpcRegisterHandler } from '../handlers/NpcRegister.Handler.js';
import { PlayerSprintHandler } from '../handlers/PlayerSprint.Handler.js';
import { PingHandler } from '../handlers/Ping.Handler.js';
import { TaskCatalogRegisterHandler } from '../handlers/TaskCatalogRegisterHandler.Handler.js';
import { TaskAssignRequestHandler } from '../handlers/TaskAssignRequestHandler.Handler.js';
import { TaskStartInteractionHandler } from '../handlers/TaskStartInteraction.Handler.js';
import { TaskCompleteAttemptHandler } from '../handlers/TaskCompleteAttempt.Handler.js';
import { TaskProgressHandler } from '../handlers/TaskProgress.Handler.js';

/**
 * SocketHandlerFactory: Mapeia nomes de eventos socket para suas implementações concretas (Handlers).
 * 
 * Este padrão permite adicionar novos eventos apenas vinculando uma string de evento 
 * a uma classe Handler, mantendo o SocketManager limpo e modular.
 */
export class SocketHandlerFactory {
    private static handlers = new Map<string, SocketHandlerConstructor>();

    /**
     * Registra o mapeamento estático de eventos.
     */
    static {
        this.handlers.set('join_room', JoinRoomHandler);
        this.handlers.set('player_move', PlayerMoveHandler);
        this.handlers.set('npc_move_request', NpcMoveHandler);
        this.handlers.set('npc_register', NpcRegisterHandler);
        this.handlers.set('player_sprint', PlayerSprintHandler);
        this.handlers.set('ping', PingHandler);
        this.handlers.set('task_catalog_register', TaskCatalogRegisterHandler);
        this.handlers.set('task_assign_request', TaskAssignRequestHandler);
        this.handlers.set('task_start_interaction', TaskStartInteractionHandler);
        this.handlers.set('task_complete_attempt', TaskCompleteAttemptHandler);
        this.handlers.set('task_progress', TaskProgressHandler);
    }

    /**
     * Cria e retorna uma nova instância do handler para o evento solicitado.
     */
    public static createHandler(event: string): ISocketHandler | null {
        const HandlerConstructor = this.handlers.get(event);
        if (!HandlerConstructor) {
            return null;
        }
        return new HandlerConstructor();
    }

    /**
     * Retorna a lista de todos os eventos registrados nesta factory.
     */
    public static getRegisteredEvents(): string[] {
        return Array.from(this.handlers.keys());
    }
}
