import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../../core/factories/Service.Factory.js', () => ({
    ServiceFactory: {
        getTaskService: vi.fn(),
    },
}));

import { TaskCatalogRegisterHandler } from './TaskCatalogRegisterHandler.Handler.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

describe('TaskCatalogRegisterHandler', () => {
    let handler: TaskCatalogRegisterHandler;
    let mockServer: any;
    let mockTaskService: any;
    const rinfo = { address: '127.0.0.1', port: 5001 } as any;

    beforeEach(() => {
        handler = new TaskCatalogRegisterHandler();
        mockTaskService = {
            registerCatalog: vi.fn(),
        };
        mockServer = {
            getSession: vi.fn(),
            sendTo: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
        (ServiceFactory.getTaskService as any).mockReturnValue(mockTaskService);
    });

    it('rejeita payload sem campo tasks — responde ERROR ao remetente', async () => {
        mockServer.getSession.mockReturnValue({ id: 'player-1', roomId: 'room-1' });

        await handler.handle(mockServer, rinfo, {} as any);

        expect(mockTaskService.registerCatalog).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(
            rinfo,
            'ERROR',
            expect.objectContaining({ message: expect.any(String) }),
        );
    });

    it('rejeita sessão sem roomId — responde ERROR ao remetente', async () => {
        // Sessão existe mas sem roomId
        mockServer.getSession.mockReturnValue({ id: 'player-1' });

        await handler.handle(mockServer, rinfo, {
            tasks: [{ id: 'task-01', description: 'Teste', type: 'collect', targetCount: 1 }],
        });

        expect(mockTaskService.registerCatalog).not.toHaveBeenCalled();
        expect(mockServer.sendTo).toHaveBeenCalledWith(
            rinfo,
            'ERROR',
            expect.objectContaining({ message: expect.any(String) }),
        );
    });

    it('chama registerCatalog com tasks e roomId corretos (sem npcId)', async () => {
        mockServer.getSession.mockReturnValue({ id: 'player-1', roomId: 'room-42' });
        const tasks = [
            { id: 'task-01', description: 'Recolher documentos', type: 'collect', targetCount: 3 },
            { id: 'task-02', description: 'Entregar relatório', type: 'deliver', targetCount: 1 },
        ];

        await handler.handle(mockServer, rinfo, { tasks });

        expect(mockTaskService.registerCatalog).toHaveBeenCalledOnce();
        expect(mockTaskService.registerCatalog).toHaveBeenCalledWith('room-42', tasks);
    });

    // ────────────────────────────────────────────────────────
    // items / pairs (plano 0004)
    //
    // Critério: campo malformado é DESCARTADO com warn; o resto do catálogo é
    // registrado normalmente. Derrubar o registro inteiro da sala por um campo
    // torto deixaria o jogador sem nenhuma tarefa.
    // ────────────────────────────────────────────────────────

    /** Atalho: registra um catálogo de uma task só e devolve a entrada que chegou ao service. */
    async function registerOne(task: Record<string, unknown>) {
        mockServer.getSession.mockReturnValue({ id: 'player-1', roomId: 'room-42' });
        await handler.handle(mockServer, rinfo, { tasks: [task] } as any);
        return mockTaskService.registerCatalog.mock.calls[0][1][0];
    }

    // Cycle 20
    it('repassa items e pairs ao registerCatalog quando presentes', async () => {
        const entry = await registerOne({
            id: 'coleta-1',
            description: 'Guarde os papéis',
            type: 'collect',
            targetCount: 2,
            items: ['papel-01', 'papel-02'],
            pairs: [{ itemId: 'papel-01', slotId: 'caixa' }],
        });

        expect(entry.items).toEqual(['papel-01', 'papel-02']);
        expect(entry.pairs).toEqual([{ itemId: 'papel-01', slotId: 'caixa' }]);
    });

    it('não inventa items/pairs em entradas que não os declaram', async () => {
        const entry = await registerOne({
            id: 'cafe-1',
            description: 'Faça o café',
            type: 'coffee_maker',
            targetCount: 1,
        });

        expect(entry.items).toBeUndefined();
        expect(entry.pairs).toBeUndefined();
    });

    // Cycle 21
    it('descarta items malformado (não-array de string) com warn e registra o resto do catálogo', async () => {
        const entry = await registerOne({
            id: 'coleta-1',
            description: 'Guarde os papéis',
            type: 'collect',
            targetCount: 2,
            items: 'papel-01', // deveria ser array
        });

        expect(mockTaskService.registerCatalog).toHaveBeenCalledOnce();
        expect(entry.id).toBe('coleta-1');
        expect(entry.targetCount).toBe(2);
        expect(entry.items).toBeUndefined();
    });

    it('filtra elementos não-string de items, mantendo os válidos', async () => {
        const entry = await registerOne({
            id: 'coleta-1',
            description: 'Guarde os papéis',
            type: 'collect',
            targetCount: 2,
            items: ['papel-01', 42, null, 'papel-02'],
        });

        expect(entry.items).toEqual(['papel-01', 'papel-02']);
    });

    // Cycle 22
    it('descarta pairs com entrada sem itemId ou sem slotId', async () => {
        const entry = await registerOne({
            id: 'encomendas-1',
            description: 'Organize as encomendas',
            type: 'sort_parcels',
            targetCount: 2,
            pairs: [
                { itemId: 'caixa-ti', slotId: 'estante-ti' },
                { itemId: 'caixa-rh' },                      // sem slotId
                { slotId: 'estante-fin' },                   // sem itemId
                { itemId: 'caixa-com', slotId: 'estante-com' },
            ],
        });

        // Entradas válidas sobrevivem; as tortas somem (falha fechada: item
        // desconhecido é recusado, em vez de passar sem validação).
        expect(entry.pairs).toEqual([
            { itemId: 'caixa-ti', slotId: 'estante-ti' },
            { itemId: 'caixa-com', slotId: 'estante-com' },
        ]);
    });

    it('descarta o campo pairs inteiro quando nenhuma entrada é válida', async () => {
        const entry = await registerOne({
            id: 'encomendas-1',
            description: 'Organize as encomendas',
            type: 'sort_parcels',
            targetCount: 2,
            pairs: [{ itemId: 'caixa-rh' }, 'lixo'],
        });

        // Um pairs vazio significaria "nenhum item é válido" e travaria a task;
        // é indistinguível de um campo ausente, então é descartado.
        expect(entry.pairs).toBeUndefined();
    });
});
