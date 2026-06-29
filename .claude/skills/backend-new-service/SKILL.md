---
name: backend-new-service
description: Aplicar quando criar service novo no backend. Service deve ser registrado em `ServiceFactory` (`src/core/factories/Service.Factory.ts`) — nunca importado diretamente pelos controllers/handlers.
applies_to: backend
---

# backend-new-service — Criar service e registrar no ServiceFactory

## Quando aplicar

- Nova lógica de domínio que orquestra Prisma, regras de negócio, integrações
- Refator que tira código duplicado de controller/handler pra um lugar comum
- Wrap em torno de Prisma com auditoria/log/cache

## Quando NÃO aplicar

- Lógica que vive 100% num controller específico — mantenha no controller
- Handler UDP — esse tem o próprio padrão (ver `backend-new-udp-handler`)
- Helpers puros sem state — vai em `src/utils/`

## A regra principal

> **Controllers e handlers obtêm services via `ServiceFactory.getXxxService()`**. **Nunca** via `import xxxService from '../services/xxxService.js'` direto dentro de um controller.

Isso permite:
- Testes substituem o método estático por mock
- Refactor de construtor do service afeta só a factory
- Logs centralizados (a factory pode envelopar)

## Ordem de criação

```
1. src/services/<feature>Service.ts        ← classe + export default da instância
2. src/core/factories/Service.Factory.ts   ← adicionar método getter
3. Controller/handler chama Factory        ← consumo
4. src/services/<feature>Service.test.ts   ← spec lado-a-lado (TDD)
```

## Passo 1 — Implementar o service

`hora-extra-backend/src/services/roomService.ts`:

```ts
import prisma from '../database/prisma.js';
import logger from '../utils/Logger.js';
import { ApiError } from '../core/ApiError.js';

/**
 * RoomService: gerencia rooms persistidas e seu lifecycle.
 */
export class RoomService {

    public async create(name: string, ownerId: string, maxPlayers: number = 4) {
        if (!name || name.trim().length === 0) {
            throw ApiError.badRequest('Nome da sala é obrigatório');
        }
        if (maxPlayers < 2 || maxPlayers > 8) {
            throw ApiError.badRequest('maxPlayers deve estar entre 2 e 8');
        }

        const room = await prisma.room.create({
            data: { name: name.trim(), ownerId, maxPlayers },
        });

        logger.info(`Sala criada: ${room.id} por ${ownerId}`, { module: 'GAME' });
        return room;
    }

    public async findById(id: string) {
        const room = await prisma.room.findUnique({ where: { id } });
        if (!room) {
            throw ApiError.notFound(`Sala ${id} não encontrada`);
        }
        return room;
    }

    public async listByOwner(ownerId: string) {
        return prisma.room.findMany({
            where: { ownerId },
            orderBy: { createdAt: 'desc' },
        });
    }
}

// Singleton: 1 instância pra toda a app
export default new RoomService();
```

### Convenções

- **Export `default` da instância** (singleton). Imports consumidores recebem a mesma referência.
- **Métodos públicos** com `public` explícito (mesmo sendo default em TS).
- **`async`/`await`** em tudo que toca DB ou rede.
- **Erros** via `ApiError.<tipo>(msg)` — nunca `throw new Error(...)`.
- **Logs** via `logger` com `module: 'GAME'` ou `'AUTH'` ou o que fizer sentido.
- **Validações de entrada** no início do método. Falha rápido = melhor erro pro cliente.
- **Imports** com `.js`.

### Quando o service precisa de outro service

```ts
import authService from './authService.js';  // OK aqui — é dentro de services/

export class RoomService {
    public async createForUser(userId: string, name: string) {
        const user = await authService.findById(userId);  // service chama service direto
        return this.create(name, user.id);
    }
}
```

> Dentro de `src/services/`, services **podem** se importar diretamente. A regra do ServiceFactory é para **controllers e handlers** consumindo `services/`.

## Passo 2 — Registrar no ServiceFactory

`hora-extra-backend/src/core/factories/Service.Factory.ts`:

```ts
import authService from '../../services/authService.js';
import roomService from '../../services/roomService.js';   // ← novo
import prisma from '../../database/prisma.js';
import { PrismaClient } from '@prisma/client';

export class ServiceFactory {
    public static getAuthService() {
        return authService;
    }

    public static getRoomService() {              // ← novo getter
        return roomService;
    }

    public static getPrismaClient(): PrismaClient {
        return prisma;
    }
}
```

> Mantenha o padrão de naming: `get<NomeDoService>()`.

## Passo 3 — Consumir do controller/handler

`hora-extra-backend/src/api/controllers/RoomController.ts`:

```ts
import { Request, Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import { Authorize } from '../../core/decorators/Authorize.js';
import { AuthRequest } from '../../types/AuthRequest.js';

export class RoomController extends BaseController {

    @Authorize()
    public async create(req: AuthRequest, res: Response, next: NextFunction) {
        try {
            const roomService = ServiceFactory.getRoomService();  // ← via factory
            const { name, maxPlayers } = req.body;
            const room = await roomService.create(name, req.jogadorId!, maxPlayers);
            this.sendCreated(res, room, 'Sala criada com sucesso');
        } catch (err) {
            next(err);
        }
    }
}
```

> Nunca:
> ```ts
> import roomService from '../../services/roomService.js';  // ❌ pular factory
> ```

## Passo 4 — Spec do service (TDD)

`src/services/roomService.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../database/prisma.js', () => ({
    default: {
        room: {
            create: vi.fn(),
            findUnique: vi.fn(),
            findMany: vi.fn(),
        },
    },
}));

import prisma from '../database/prisma.js';
import roomService, { RoomService } from './roomService.js';
import { ApiError } from '../core/ApiError.js';

describe('RoomService.create', () => {
    beforeEach(() => vi.clearAllMocks());

    it('rejeita nome vazio com 400', async () => {
        const service = new RoomService();
        await expect(service.create('   ', 'u1')).rejects.toThrow(ApiError);
    });

    it('rejeita maxPlayers fora do range [2, 8]', async () => {
        const service = new RoomService();
        await expect(service.create('Sala X', 'u1', 1)).rejects.toThrow();
        await expect(service.create('Sala X', 'u1', 9)).rejects.toThrow();
    });

    it('persiste sala válida e retorna o registro', async () => {
        (prisma.room.create as any).mockResolvedValue({
            id: 'r1',
            name: 'Sala X',
            ownerId: 'u1',
            maxPlayers: 4,
        });
        const service = new RoomService();

        const result = await service.create('Sala X', 'u1');

        expect(prisma.room.create).toHaveBeenCalledWith({
            data: { name: 'Sala X', ownerId: 'u1', maxPlayers: 4 },
        });
        expect(result.id).toBe('r1');
    });
});
```

Roda isoladamente:

```bash
cd hora-extra-backend && npx vitest run src/services/roomService.test.ts -t "rejeita nome vazio"
```

## Checklist

- [ ] Service em `src/services/<feature>Service.ts`
- [ ] Classe nomeada (`RoomService`) + `export default new ...()`
- [ ] Métodos `public async` com validação de entrada
- [ ] Erros via `ApiError.<tipo>` (nunca raw `throw`)
- [ ] Logs com `logger` + `{ module: '...' }`
- [ ] Registrado em `ServiceFactory` com método `getXxxService()`
- [ ] Controller/handler consome via `ServiceFactory.getXxxService()`
- [ ] Spec `.test.ts` side-by-side cobrindo happy path + validações de entrada
- [ ] Imports todos com `.js`

## Gotchas

1. **Esquecer de registrar na Factory** = controller importa direto e quebra o padrão. Reviewer flagga.
2. **`new RoomService()` em vários lugares** = perde singleton. Sempre `export default new RoomService()` e consumir via Factory.
3. **Service estático (`public static create(...)`)** funciona mas não é o padrão. Use instância.
4. **Service que chama HTTP externo** — cuide de timeout/retry. Não bloqueie um tick por 30s.
5. **Service com state mutável** (cache, queue) — documente o lifecycle. Se é volátil, OK; se precisa persistir, vai pra DB.

## Referências

- `hora-extra-backend/src/services/authService.ts` — exemplo canônico
- `hora-extra-backend/src/core/factories/Service.Factory.ts` — registry
- `.agents/rules/backend-factory-pattern.md` — rule humana
- `hora-extra-backend/docs/PATTERN_FACTORY.md` — doc do padrão
