---
name: backend-authorize-decorator
description: Aplicar quando precisar proteger método de controller HTTP com JWT. Decorator `@Authorize()` de `src/core/decorators/Authorize.ts` envelopa o método, valida o token, anexa `req.jogadorId`, e propaga `ApiError` via `next()`.
applies_to: backend
---

# backend-authorize-decorator — Proteger endpoints com JWT

## Quando aplicar

- Endpoint REST que exige usuário logado
- Você precisa de `req.jogadorId` (ID do usuário do JWT) no handler
- Qualquer ação que escreve no DB associada a um usuário

## Quando NÃO aplicar

- Health check (`/api/health`) — público
- Login (`/api/auth/login`) — recebe credenciais, não tem token ainda
- Cadastro (`/api/auth/register`) — idem
- UDP socket — **outro mecanismo**: handshake `CONN` com `token` no pacote, validado no `UdpSocketManager` (sem `@Authorize()`)

## Como funciona

`src/core/decorators/Authorize.ts` é um method decorator que envelopa o método original:

```ts
@Authorize()
public async create(req: AuthRequest, res: Response, next: NextFunction) {
    // req.jogadorId já está populado aqui
}
```

Por baixo:

1. Antes de chamar o método, dispara `authMiddleware.authenticate(req, res, ...)`.
2. Esse middleware valida JWT em `Authorization: Bearer <token>`.
3. Se válido → seta `req.jogadorId = decoded.id` e chama `next()` interno → executa o método original.
4. Se inválido → `next(err)` com `ApiError.unauthorized(...)` → Express handler → resposta 401.

## Pré-requisitos para o decorator funcionar

### 1. `experimentalDecorators` no tsconfig

Já está habilitado em `tsconfig.json`. Confira:

```json
{
  "compilerOptions": {
    "experimentalDecorators": true,
    "emitDecoratorMetadata": false
  }
}
```

> Sem `experimentalDecorators: true`, TS reclama `Experimental support for decorators is a feature that is subject to change in a future release`.

### 2. **Método de classe**, não arrow function

O decorator precisa do `descriptor` da `property`. Arrow functions são bound em runtime e não têm descriptor. Tem que ser:

```ts
// ✅ funciona
class FooController extends BaseController {
    @Authorize()
    public async create(req, res, next) { ... }
}

// ❌ não funciona — decorator não pega
class FooController extends BaseController {
    @Authorize()
    public create = async (req, res, next) => { ... };  // arrow + class property
}
```

### 3. Router chama via `controller.metodo(req, res, next)`

Como o decorator usa `this.sendSuccess(...)`, o método **precisa** ser chamado na instância:

```ts
router.post('/', (req, res, next) => controller.create(req as any, res, next));
```

Não passe `controller.create` desbindado — `this` se perde.

## Uso completo

```ts
import { Response, NextFunction } from 'express';
import { BaseController } from '../../core/BaseController.js';
import { Authorize } from '../../core/decorators/Authorize.js';
import { AuthRequest } from '../../types/AuthRequest.js';
import { ServiceFactory } from '../../core/factories/Service.Factory.js';

export class RoomController extends BaseController {

    @Authorize()
    public async create(req: AuthRequest, res: Response, next: NextFunction) {
        try {
            const userId = req.jogadorId!;   // ← garantido pelo decorator
            const roomService = ServiceFactory.getRoomService();
            const room = await roomService.create(req.body.name, userId);
            return this.sendCreated(res, room);
        } catch (err) {
            next(err);
        }
    }

    // Endpoint público — sem @Authorize
    public async listPublic(req: AuthRequest, res: Response, next: NextFunction) {
        try {
            const roomService = ServiceFactory.getRoomService();
            const rooms = await roomService.listPublic();
            return this.sendSuccess(res, rooms);
        } catch (err) {
            next(err);
        }
    }
}
```

### `AuthRequest`

`src/types/AuthRequest.ts` é um type extension do Express Request:

```ts
import { Request } from 'express';

export interface AuthRequest extends Request {
    jogadorId?: string;
}
```

Use esse tipo em **todo** handler que tenha `@Authorize()`. Em handlers públicos é opcional — `Request` é suficiente. Mas padronizar todos como `AuthRequest` simplifica.

## Cliente: como enviar o token

```http
POST /api/rooms HTTP/1.1
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJIUzI1NiI...

{ "name": "Sala X", "maxPlayers": 4 }
```

No Unity (`ApiClient.cs`):

```csharp
request.SetRequestHeader("Authorization", $"Bearer {token}");
```

## Respostas em erro

| Cenário                            | Status | Mensagem                          |
| :--------------------------------- | :----- | :-------------------------------- |
| Sem `Authorization`                | 401    | "Token não fornecido"             |
| Bearer mal formatado               | 401    | "Formato de token inválido"       |
| JWT assinatura inválida            | 401    | "Token inválido"                  |
| JWT expirado                       | 401    | "Token expirado"                  |

Tudo via `ApiError.unauthorized(msg)` → handler → envelope JSON.

## Padrão: validação adicional dentro do método

`@Authorize()` só valida que **alguém** logado está chamando. **Não** valida:

- Se o usuário é dono do recurso (`ownership check` vai no service)
- Se o usuário tem role específica (admin, etc.) — projeto não tem hoje, mas se vier, criar `@AuthorizeRole('ADMIN')` separado
- Rate limiting (se vier, middleware separado)

```ts
@Authorize()
public async deleteRoom(req: AuthRequest, res: Response, next: NextFunction) {
    try {
        const userId = req.jogadorId!;
        const { id } = req.params;
        const roomService = ServiceFactory.getRoomService();

        const room = await roomService.findById(id);
        if (room.ownerId !== userId) {
            throw ApiError.forbidden('Apenas o dono pode deletar a sala');
        }

        await roomService.delete(id);
        return this.sendSuccess(res, null, 'Sala deletada');
    } catch (err) {
        next(err);
    }
}
```

## Checklist

- [ ] Método é de classe (não arrow function)
- [ ] Decorator antes da assinatura: `@Authorize()`
- [ ] Tipo do `req` é `AuthRequest`
- [ ] Router invoca via `(req, res, next) => controller.metodo(...)` (bind preservado)
- [ ] Lê `req.jogadorId!` (não-null assertion OK; o decorator garante)
- [ ] Try/catch envolvendo o body; `next(err)` no catch
- [ ] Ownership/role check no service quando aplicável

## Gotchas

1. **`@Authorize` em propriedade arrow** = silenciosamente ignorado, e `req.jogadorId` vem undefined. Reviewer pega.
2. **Esquecer o `()` ao final**: `@Authorize` (sem chamar) = decorator factory passa, não decorator. Use `@Authorize()`.
3. **`req.jogadorId` é `string | undefined`** no tipo, mas em runtime depois do decorator é sempre `string`. Use `!` ou narrow:
   ```ts
   if (!req.jogadorId) throw ApiError.unauthorized();  // verboso
   const id = req.jogadorId!;                          // OK aqui
   ```
4. **Decorator não funciona com `tsx`/swc sem flag** — se algum dia migrar de `ts-node` pra outro loader, conferir `--experimental-decorators`.
5. **Combinar com outros decorators** (ex: rate limit) — ordem importa. `@Authorize()` deve ser o **mais próximo** do método (executa primeiro no envelopamento). Mas hoje só tem `@Authorize()` no projeto.

## Referências

- `hora-extra-backend/src/core/decorators/Authorize.ts` — implementação
- `hora-extra-backend/src/middleware/authMiddleware.ts` — JWT validation
- `hora-extra-backend/src/types/AuthRequest.ts` — type extension
- `hora-extra-backend/src/services/authService.ts` — `verifyToken(token)`
- `hora-extra-backend/docs/AUTHENTICATION.md` — flow completo (se existir)
- `hora-extra-backend/docs/AUTH_API.md` — endpoints `/auth/login`, `/auth/register`
- `hora-extra-backend/tsconfig.json` — confirma `experimentalDecorators: true`
