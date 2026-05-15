---
name: backend-logger-conventions
description: Aplicar SEMPRE que adicionar logging em código backend. Proíbe `console.log`, exige `logger` de `src/utils/Logger.ts` (Winston daily-rotated) com metadata `{ module: 'NOME' }` em todos os calls.
applies_to: backend
---

# backend-logger-conventions — Logging no hora-extra-backend

## Quando aplicar

- Toda vez que precisar logar algo no backend (info, warn, error, debug)
- Quando criar handler/service/middleware novo (definir o `module` desde o primeiro log)
- Ao revisar PR — flagga qualquer `console.log` como violação

## A regra em uma frase

> **Nunca `console.log`. Sempre `logger` de `src/utils/Logger.ts`. Sempre `{ module: 'NOME' }` no metadata.**

## Import

```ts
import logger from '../utils/Logger.js';  // path relativo + .js (ESM/NodeNext)
```

> ⚠️ `.js` é literal — o arquivo no disco é `Logger.ts`. ESM com NodeNext exige extensão `.js` no import path. Sem isso = `ERR_MODULE_NOT_FOUND`.

## Níveis e quando usar

| Nível    | Quando                                                        | Exemplo                                                 |
| :------- | :------------------------------------------------------------ | :------------------------------------------------------ |
| `error`  | Falha crítica, exceção não tratada, perda de dado             | DB connection morta, JWT inválido em produção           |
| `warn`   | Comportamento inesperado, validação falhou, security event    | Payload inválido recebido, token expirado, rate limit   |
| `info`   | Eventos de ciclo de vida (1×/operação grande)                 | Server startup, sessão criada, sala fechada             |
| `debug`  | Detalhes de processamento — só em dev                         | Cada pacote UDP, cada tick, cada decode JSON            |

**Heurística**: se acontece >1×/segundo no fluxo normal → `debug`. Se é uma vez por evento de usuário → `info`. Se é uma vez por servidor → `info` no startup, `error` no crash.

## Metadata obrigatório: `{ module: 'NOME' }`

Toda chamada **precisa** de metadata com `module`. Sem isso, fica impossível filtrar no log agregado.

### Tabela de módulos canônicos

| Módulo         | Onde usar                                                  |
| :------------- | :--------------------------------------------------------- |
| `SERVER`       | `src/index.ts`, boot/shutdown                              |
| `HTTP`         | Express middleware, error handler, route logs              |
| `UDP_SOCKET`   | `UdpSocketManager`, handlers UDP                           |
| `AUTH`         | `authService`, `authMiddleware`, JWT, decorator            |
| `GAME`         | State management, tick loop, NPC, room logic               |
| `DB`           | Prisma, migrations, db setup script                        |
| `<HANDLER>`    | Handler específico — usar nome do evento (`PLAYER_MOVE`, `NPC_REGISTER`) se quiser granularidade extra. Fallback: `UDP_SOCKET`. |

Não invente módulos novos sem motivo. Se precisar, adicione aqui e em docs.

## Templates

### Info (ciclo de vida)

```ts
logger.info(`Servidor HTTP rodando na porta ${PORT}`, { module: 'SERVER' });
logger.info(`Sessão UDP iniciada: ${playerId} em ${sessionKey}`, { module: 'UDP_SOCKET' });
```

### Warn (comportamento anômalo)

```ts
logger.warn(`Token inválido no handshake de ${rinfo.address}:${rinfo.port}`, { module: 'AUTH' });
logger.warn(`Payload inválido em player_move: ${JSON.stringify(data).slice(0, 200)}`, { module: 'UDP_SOCKET' });
```

### Error (falha)

```ts
logger.error(`Erro ao processar datagrama de ${rinfo.address}`, { module: 'UDP_SOCKET', error: err });
logger.error(`Falha ao consultar User no Prisma`, { module: 'DB', error: err, userId });
```

> **`error` sempre vai como `error: err` no metadata** — o Winston extrai stack trace.

### Debug (dev verbose)

```ts
logger.debug(`[UDP_SOCKET] Jogador ${session.id} sprint: ${data.s}`, { module: 'UDP_SOCKET' });
logger.debug(`Tick ${tick} broadcast pra sala ${roomId} (${count} jogadores)`, { module: 'GAME' });
```

## O que NÃO logar

- ❌ Senhas, tokens, headers de Authorization (logue `tokenLength: token.length` se precisar saber que tinha algo, nunca o valor)
- ❌ Dump completo do payload em event de alta frequência (`player_move` 20Hz) — só em `debug` e com `.slice(0, 200)` se necessário
- ❌ `JSON.stringify(req)` ou `JSON.stringify(socket)` — vai estourar com referências circulares
- ❌ Mensagens só com emoji ou string vazia — sempre contexto + IDs relevantes

## Anti-patterns que o reviewer pega

```ts
// ❌ console.log — PROIBIDO
console.log('Sessão iniciada');

// ❌ falta module
logger.info('Sessão iniciada');

// ❌ módulo em lowercase / format errado
logger.info('Sessão iniciada', { module: 'udp_socket' });
logger.info('Sessão iniciada', { mod: 'UDP_SOCKET' });

// ❌ console.error em catch
} catch (err) { console.error(err); }

// ❌ stringify circular
logger.error('Falhou', { module: 'HTTP', req: JSON.stringify(req) });
```

```ts
// ✅ correto
logger.info(`Sessão iniciada para ${playerId}`, { module: 'UDP_SOCKET' });

// ✅ erro com stack preservado
} catch (err) {
    logger.error(`Falha ao validar token`, { module: 'AUTH', error: err });
}

// ✅ dados estruturados extras (Winston aceita N campos no meta)
logger.warn(`Rate limit excedido`, { module: 'HTTP', userId, route: req.path, ip: req.ip });
```

## Estrutura física dos logs

`Logger.ts` configura Winston com daily-rotate. Arquivos saem em (verifique o `Logger.ts` real):
- `logs/combined-YYYY-MM-DD.log`
- `logs/error-YYYY-MM-DD.log`

Console também recebe (formato colorido em dev). **Nada disso é seu problema** — só use `logger.<level>(...)` e o transport faz o resto.

## Checklist (revisor)

- [ ] Zero `console.log` / `console.error` / `console.warn` no diff
- [ ] Cada chamada `logger.<level>(...)` tem `{ module: '...' }` no 2º argumento
- [ ] Módulo está na tabela canônica (ou justificado nas notes)
- [ ] `error` calls passam o `err` como `error: err` no metadata
- [ ] Não há dump de credentials/payload sensível
- [ ] Eventos de alta frequência são `debug`, não `info`

## Gotchas

1. **Import com `.js`** — esquecer = runtime error em ESM.
2. **`module: 'NOME'` é case-sensitive** no filtro de log agregado. Use SCREAMING_SNAKE consistente.
3. **`logger.error(err)` (string-só, sem meta)** funciona mas perde o `module` — sempre passe o objeto meta.
4. **Em handlers de alta frequência**, `logger.debug` é OK em dev (NODE_ENV=development) mas considere se mesmo em debug isso vai te ajudar — 1200 msgs/seg/sala = ruído.
5. **`Logger.ts` é singleton**: `import logger from '...'` retorna a mesma instância. Não instancie novamente.

## Referências no código

- `hora-extra-backend/src/utils/Logger.ts` — implementação Winston + transports
- `hora-extra-backend/src/sockets/UdpSocketManager.ts` — exemplo canônico de uso (info, warn, error, debug)
- `hora-extra-backend/docs/LOGGING.md` — doc humana, se existir
- `.agents/rules/backend-design-pattern.md` §8 — rule humana
