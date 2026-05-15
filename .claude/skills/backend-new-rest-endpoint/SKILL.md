---
name: backend-new-rest-endpoint
description: Aplicar quando adicionar endpoint REST. Controller estende `BaseController`, responde com `sendSuccess`/`sendCreated`, joga `ApiError` via `next(error)`, protege com `@Authorize()` quando autenticado.
applies_to: backend
---

# backend-new-rest-endpoint — Criar endpoint REST no hora-extra-backend

## Quando aplicar

- Adicionar `GET`/`POST`/`PATCH`/`DELETE` em recurso existente ou novo
- Endpoint protegido (precisa de JWT) ou público
- Endpoint que retorna JSON padronizado (envelope `success/data`)

## Quando NÃO aplicar

- Real-time / streaming → vai por UDP (ver `backend-new-udp-handler`)
- Health check → já existe em `/api/health`
- WebSocket → projeto **não usa** WebSocket; tudo real-time é UDP

## Arquitetura HTTP do projeto

```
Express app (PORT=5000)
  └─ /api  (prefix global, definido em src/api/routes/index.ts)
      ├─ /health
      ├─ /auth   ← AuthRouter → AuthController
      └─ /rooms  ← RoomRouter → RoomController
```

Cada recurso tem:

```
src/api/routes/<Recurso>Router.ts          ← Express Router, mapeia path → método do controller
src/api/controllers/<Recurso>Controller.ts ← classe que estende BaseController
src/services/<recurso>Service.ts           ← lógica de negócio (já existe ou criar via skill)
```

Resposta padrão (envelope `ApiResponse`):

```json
{
  "success": true,
  "status": 200,
  "message": "OK",
  "data": { ... }
}
```

Erros caem em `middleware/errorHandler.ts` → mesmo envelope com `success: false`.

## Passo 1 — Service primeiro (TDD)

Se a lógica é nova, crie service antes (ver `backend-new-service`). O controller é fino: parse req → chama service → envelope resposta.

## Passo 2 — Controller

`hora-extra-backend/src/api/controllers/RoomController.ts`:

```ts
import { Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';
import { Authorize } from '../../core/decorators/Authorize.js';
import { ApiError } from '../../core/ApiError.js';
import { AuthRequest } from '../../types/AuthRequest.js';
import logger from '../../utils/Logger.js';

export class RoomController extends BaseController {

    @Authorize()
    public async create(req: AuthRequest, res: Response, next: NextFunction) {
        try {
            const { name, maxPlayers } = req.body;
            if (!name) {
                throw ApiError.badRequest('Campo "name" é obrigatório');
            }

            const roomService = ServiceFactory.getRoomService();
            const room = await roomService.create(name, req.jogadorId!, maxPlayers);

            logger.info(`Sala criada via HTTP: ${room.id} por ${req.jogadorId}`, { module: 'HTTP' });
            return this.sendCreated(res, room, 'Sala criada com sucesso');
        } catch (err) {
            next(err);
        }
    }

    @Authorize()
    public async listMine(req: AuthRequest, res: Response, next: NextFunction) {
        try {
            const roomService = ServiceFactory.getRoomService();
            const rooms = await roomService.listByOwner(req.jogadorId!);
            return this.sendSuccess(res, rooms);
        } catch (err) {
            next(err);
        }
    }

    @Authorize()
    public async getById(req: AuthRequest, res: Response, next: NextFunction) {
        try {
            const { id } = req.params;
            const roomService = ServiceFactory.getRoomService();
            const room = await roomService.findById(id);
            return this.sendSuccess(res, room);
        } catch (err) {
            next(err);
        }
    }
}
```

### Regras obrigatórias do controller

1. **Estende `BaseController`** — pra usar `sendSuccess`/`sendCreated`.
2. **Métodos `public async`** com signatura `(req, res, next)`.
3. **Erros via `throw ApiError.<tipo>(...)` + `next(err)`** — nunca `res.status(...).json({...})` direto pra erros.
4. **`@Authorize()`** para endpoints autenticados (anexa `req.jogadorId` via JWT).
5. **`AuthRequest`** como tipo do `req` em handlers protegidos (extends Express Request com `jogadorId?: string`).
6. **`ServiceFactory.getXxxService()`** — nunca import direto.
7. **`try/catch` em todo handler async** — sem isso, exceção async escapa do Express.

> ⚠️ O `@Authorize()` decorator **só funciona em métodos de classe** (não arrow functions). É por isso que controllers são classes, não objetos com closures.

## Passo 3 — Router

`hora-extra-backend/src/api/routes/RoomRouter.ts`:

```ts
import { Router } from 'express';
import { RoomController } from '../controllers/RoomController.js';

const router = Router();
const controller = new RoomController();

router.post('/', (req, res, next) => controller.create(req as any, res, next));
router.get('/my', (req, res, next) => controller.listMine(req as any, res, next));
router.get('/:id', (req, res, next) => controller.getById(req as any, res, next));

export default router;
```

> O cast `req as any` é por causa do `AuthRequest` (que tem `jogadorId?: string`). Express Request base não tem; o `@Authorize()` adiciona em runtime. Aceito.

> Por que `(req, res, next) => controller.create(req, res, next)` em vez de `router.post('/', controller.create)`? **Bind do `this`** — o decorator usa `this.sendCreated(...)`, então o método precisa ser chamado **na instância**. Repassar via arrow garante isso.

## Passo 4 — Registrar no index de routes

`hora-extra-backend/src/api/routes/index.ts`:

```ts
import { Router } from 'express';
import healthRouter from './HealthRouter.js';
import authRouter from './AuthRouter.js';
import roomRouter from './RoomRouter.js';   // ← novo

const router = Router();

router.use('/health', healthRouter);
router.use('/auth', authRouter);
router.use('/rooms', roomRouter);            // ← novo

export default router;
```

Esse `router` é montado em `src/index.ts` com `app.use('/api', routes)`. **Resultado**: rotas finais são `/api/rooms`, `/api/rooms/:id`, etc.

## Passo 5 — Documentação do endpoint

`hora-extra-backend/docs/Networking/REST_Rooms.md` (criar se ainda não existe):

```markdown
# REST API — Rooms

## POST /api/rooms

Cria uma sala. Requer JWT.

**Headers**: `Authorization: Bearer <token>`

**Body**:
```json
{
  "name": "Sala da Galera",
  "maxPlayers": 4
}
```

**Resposta 201**:
```json
{
  "success": true,
  "status": 201,
  "message": "Sala criada com sucesso",
  "data": { "id": "...", "name": "...", "ownerId": "...", "maxPlayers": 4 }
}
```

**Erros**: 400 (validação), 401 (token), 500 (interno).
```

## Métodos HTTP — convenções

| Método   | Quando                                | Status sucesso     |
| :------- | :------------------------------------ | :----------------- |
| `GET`    | Leitura, idempotente, sem side effect | 200                |
| `POST`   | Criação ou ação não-idempotente       | 201 (cria), 200 (ação) |
| `PATCH`  | Update parcial de recurso             | 200                |
| `PUT`    | Replace total (raro no projeto)       | 200                |
| `DELETE` | Remoção                               | 200 ou 204         |

Use `sendCreated(res, data)` para 201. `sendSuccess(res, data)` default 200.

## Checklist

- [ ] Controller estende `BaseController`
- [ ] Endpoints protegidos têm `@Authorize()` decorator
- [ ] Erros via `throw ApiError.<tipo>(...)` + `next(err)`, nunca `res.status(...).json(...)`
- [ ] Service obtido via `ServiceFactory`, não import direto
- [ ] Try/catch em **todo** handler async
- [ ] Router registrado em `src/api/routes/index.ts` com prefixo correto
- [ ] Doc em `hora-extra-backend/docs/Networking/REST_<Recurso>.md`
- [ ] Spec do service (`<recurso>Service.test.ts`) cobre validações
- [ ] Imports com `.js`

## Gotchas

1. **`@Authorize()` em arrow function** = decorator não dispara. Use método de classe.
2. **`req.jogadorId`** só existe em handler com `@Authorize()`. Em pública, é `undefined`.
3. **Esquecer `next(err)` no catch** = Express trava porque a Promise rejeitou silenciosamente.
4. **`res.json(...)` direto** sem envelope `ApiResponse` = cliente Unity quebra ao parsear (`success/data` esperado).
5. **`@Authorize()` é só validação de JWT**; não checa autorização de recurso (ex: "user é dono da sala?"). Esse checking vai no service.
6. **Cast `req as any` no router** é aceito porque AuthRequest extends Request. Não invente novo tipo só pra isso.
7. **Path com prefix `/api`** é aplicado globalmente no `index.ts`. **Não** ponha `/api/rooms` no `router.use(...)` — só `/rooms`.

## Referências

- `hora-extra-backend/src/api/controllers/AuthController.ts` — exemplo canônico
- `hora-extra-backend/src/api/routes/index.ts` — registry de routers
- `hora-extra-backend/src/core/BaseController.ts` — superclasse
- `hora-extra-backend/src/core/ApiError.ts` — factories de erro (`badRequest`, `notFound`, `unauthorized`, `forbidden`, `internal`)
- `hora-extra-backend/src/core/ApiResponse.ts` — envelope
- `hora-extra-backend/src/middleware/errorHandler.ts` — captura `ApiError` e responde
- `hora-extra-backend/docs/REST_API_GUIDE.md` — doc humana
