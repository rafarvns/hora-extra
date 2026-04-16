import { SocketHandlerConstructor, ISocketHandler } from '../types/SocketEvent.js';
// Handlers (serão adicionados conforme implementados)
import { JoinRoomHandler } from '../handlers/JoinRoom.Handler.js';
import { PlayerMoveHandler } from '../handlers/PlayerMove.Handler.js';
import { NpcMoveHandler } from '../handlers/NpcMove.Handler.js';
import { NpcRegisterHandler } from '../handlers/NpcRegister.Handler.js';
import { PlayerSprintHandler } from '../handlers/PlayerSprint.Handler.js';

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
