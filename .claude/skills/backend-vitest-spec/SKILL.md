---
name: backend-vitest-spec
description: Aplicar SEMPRE que escrever ou modificar teste backend. Padrão TDD Red→Green→Refactor com Vitest (não Jest), arquivos `.test.ts` side-by-side, `vi.fn()`/`vi.mock()`. Sem TestBed (Vitest puro, não NestJS).
applies_to: backend
---

# backend-vitest-spec — Testes unitários com Vitest no hora-extra-backend

## Quando aplicar

- Toda **nova funcionalidade backend** precisa de spec (`.agents/rules/backend-unit-tests.md` é TDD obrigatório)
- Refator de service/handler existente que mudou comportamento → atualizar/adicionar spec
- Fix de bug → primeiro escrever spec que reproduz, depois corrigir

## Quando NÃO aplicar

- Cliente Unity: **proibido** criar testes (ver `.agents/rules/no-unit-test-on-unity.md` e skill `client-manual-playmode-verification`)
- Scripts utilitários `scripts/db-setup.ts` (raramente — pode testar manualmente)

## Localização e nomenclatura

Arquivo de teste **side-by-side com a fonte**, sufixo `.test.ts`:

```
src/sockets/handlers/
├── PlayerSprint.Handler.ts
└── PlayerSprint.Handler.test.ts   ← teste vive AO LADO da source
```

`vitest.config.ts` inclui `src/**/*.test.ts`. `tsconfig.json` exclui isso do build (não vai pra `dist/`).

> Não usar `__tests__/` folder. Não usar `.spec.ts` (esse padrão é do Jest/NestJS — o projeto não usa Jest).

## TDD: ciclo Red → Green → Refactor

Cada **ciclo** = 1 `it(...)` novo, 1 comportamento estreito. Loop:

1. **Red**: escreve só o `it(...)` e roda. Tem que falhar (assertion error OU "cannot find module" se a impl nem existe).
2. **Green**: implementa o **mínimo** pra esse `it` passar. Não toca outros arquivos.
3. **Refactor** (opcional): limpa, renomeia, extrai — re-roda o mesmo `it`. Continua verde.
4. Próximo ciclo.

### Comando para rodar 1 teste isolado (executor usa)

```bash
cd hora-extra-backend && npx vitest run src/sockets/handlers/PlayerSprint.Handler.test.ts -t "deve atualizar isSprinting"
```

- `run` = single-pass (não watch)
- `-t "<padrão>"` = filtra pelo nome do test/describe
- `--reporter=verbose` (opcional) pra ver cada it individualmente

### Comando para suite completa (test-runner usa)

```bash
cd hora-extra-backend && npm test
```

Equivale a `vitest run`. Single pass, exit code 0/1.

## Template padrão

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerSprintHandler } from './PlayerSprint.Handler.js';

describe('PlayerSprintHandler', () => {
    let handler: PlayerSprintHandler;
    let mockServer: { getSession: ReturnType<typeof vi.fn>; broadcastToRoom: ReturnType<typeof vi.fn> };

    beforeEach(() => {
        handler = new PlayerSprintHandler();
        mockServer = {
            getSession: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
    });

    it('atualiza isSprinting=true e faz broadcast pra sala', async () => {
        const session = { id: 'p1', roomId: 'r1', isSprinting: false };
        mockServer.getSession.mockReturnValue(session);
        const rinfo = { address: '127.0.0.1', port: 5001 } as any;

        await handler.handle(mockServer as any, rinfo, { s: true });

        expect(session.isSprinting).toBe(true);
        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'r1',
            'player_sprint',
            { id: 'p1', s: true },
            rinfo,
        );
    });

    it('ignora payload sem campo s (boolean)', async () => {
        await handler.handle(mockServer as any, {} as any, {} as any);
        expect(mockServer.getSession).not.toHaveBeenCalled();
    });

    it('ignora quando sessão não existe ou não tem roomId', async () => {
        mockServer.getSession.mockReturnValue(undefined);
        await handler.handle(mockServer as any, {} as any, { s: true });
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });
});
```

## Mocks: padrões aceitos

### Mock de função simples

```ts
const fn = vi.fn();
fn.mockReturnValue(42);
fn.mockResolvedValue({ id: '1' });  // pra async
fn.mockImplementation((x) => x * 2);
```

### Mock de módulo inteiro

```ts
import authService from '../services/authService.js';

vi.mock('../services/authService.js', () => ({
    default: {
        verifyToken: vi.fn(),
    },
}));

// Em algum teste:
(authService.verifyToken as any).mockReturnValue({ id: 'user-1' });
```

> Vitest usa `vi.mock` (não `jest.mock`). Path de mock deve bater com o **import path do código sob teste** — incluindo `.js`.

### Mock de Prisma client

```ts
const mockPrisma = {
    user: {
        findUnique: vi.fn(),
        create: vi.fn(),
    },
};

vi.mock('../database/prisma.js', () => ({ default: mockPrisma }));
```

Geralmente prefere-se **mockar o service** em vez do Prisma quando o teste é de camada acima.

## Assertions úteis

```ts
expect(value).toBe(42);                              // primitivo, identidade
expect(obj).toEqual({ a: 1, b: 2 });                 // deep equality
expect(fn).toHaveBeenCalledTimes(1);
expect(fn).toHaveBeenCalledWith(arg1, arg2);
expect(arr).toHaveLength(3);
expect(promise).rejects.toThrow(SomeError);
expect(promise).resolves.toBe('ok');
expect(obj).toMatchObject({ partial: true });        // subset match
```

## Padrão: testando handler UDP

Handlers são testáveis por design — o `server: any` no `handle(server, rinfo, data)` é injeção de dependência via parâmetro. Mock `server` com `getSession`/`broadcastToRoom`/`sendTo` e pronto.

```ts
const mockServer = {
    getSession: vi.fn(),
    broadcastToRoom: vi.fn(),
    sendTo: vi.fn(),
};
```

## Padrão: testando service com Prisma

Service que usa Prisma diretamente — mocke o `prisma` import:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../database/prisma.js', () => ({
    default: {
        user: {
            findUnique: vi.fn(),
            create: vi.fn(),
        },
    },
}));

import prisma from '../database/prisma.js';
import { AuthService } from './authService.js';

describe('AuthService.register', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('rejeita email duplicado', async () => {
        (prisma.user.findUnique as any).mockResolvedValue({ id: 'existing' });
        const service = new AuthService();

        await expect(service.register('a@b.com', 'pass'))
            .rejects.toThrow('Email já cadastrado');
    });
});
```

## Cobertura: o que testar obrigatoriamente

Da rule:

- Validação de payload (strings vazias, tipos errados, out of range)
- Sanity checks (movimento: velocidade máxima; sala: limite de jogadores)
- Lógica de rooms (entrar/sair, broadcast escopo)
- Cálculos de tick / state update

O que normalmente **não** precisa testar:

- Boilerplate Express (route → controller é trivial)
- Imports e instanciação simples
- Tipos TS (compilador já valida)

## Checklist por spec

- [ ] Arquivo termina em `.test.ts` (não `.spec.ts`)
- [ ] Arquivo está side-by-side com a source que testa
- [ ] Imports usam extensão `.js` (`./PlayerSprint.Handler.js`)
- [ ] `import { describe, it, expect, vi } from 'vitest';` — não Jest
- [ ] Cada `it(...)` tem 1 comportamento estreito (não múltiplos asserts não-relacionados)
- [ ] Mocks via `vi.fn()` / `vi.mock()`
- [ ] `beforeEach` limpa mocks quando reutiliza (`vi.clearAllMocks()` ou recriação)
- [ ] Test passa em isolamento (`-t "<nome>"`) e em suite (`npm test`)

## Gotchas

1. **`.js` no import**: até em `.test.ts`. ESM/NodeNext.
2. **Não confundir Vitest com Jest**: API é parecida mas:
   - `jest.fn()` → `vi.fn()`
   - `jest.mock(...)` → `vi.mock(...)`
   - `jest.spyOn(...)` → `vi.spyOn(...)`
   - `jest.clearAllMocks()` → `vi.clearAllMocks()`
3. **Mock de módulo precisa ser **antes** do import**: Vitest hoists `vi.mock` automaticamente, mas se você usar var capturada por closure, hoist quebra. Padrão seguro:
   ```ts
   vi.mock('./path.js', () => ({ default: { method: vi.fn() } }));
   import path from './path.js';  // depois
   ```
4. **`async/await` em `it`**: sempre. Handlers retornam Promise.
5. **`expect(promise).rejects.toThrow(...)`** — não esquecer `await` se você atribuir o resultado.
6. **Tipagem do `mockServer as any`** é aceitável em spec quando o tipo real (`UdpSocketManager`) traz dependências circulares.
7. **Snapshot tests** não são usados no projeto hoje. Não introduzir sem alinhar.

## Referências no código

- `hora-extra-backend/src/sockets/handlers/PlayerSprint.Handler.test.ts` — única spec atual; copie esse padrão
- `hora-extra-backend/vitest.config.ts` — config (include pattern, env, etc.)
- `hora-extra-backend/package.json` — scripts `test`, `test:watch`, `test:coverage`
- `hora-extra-backend/docs/TESTING_GUIDE.md` — doc humana (se existir)
- `.agents/rules/backend-unit-tests.md` — TDD obrigatório
