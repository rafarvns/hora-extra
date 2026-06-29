---
name: backend-new-udp-handler
description: Aplicar SEMPRE que adicionar evento UDP de gameplay. Implementa `ISocketHandler` em `src/sockets/handlers/<Evento>.Handler.ts`, registra em `SocketHandlerFactory`, valida payload server-authoritative, broadcast via `server.broadcastToRoom`. Cross-link obrigatório com `cross-repo-communication-sync`.
applies_to: backend
---

# backend-new-udp-handler — Criar handler UDP de gameplay

A skill mais densa do backend. Real-time gameplay vive aqui.

## Quando aplicar

- Adicionar evento de jogo enviado pelo cliente (movimento, ação, interação)
- Adicionar broadcast periódico ou em resposta (NPC, state update, eventos de sala)
- Refator que tira lógica do `UdpSocketManager` para handler dedicado

## Quando NÃO aplicar

- Operação CRUD (cria sala, lista salas, login) → REST, não UDP (ver `backend-new-rest-endpoint`)
- Handshake de conexão (`CONN`) — já tratado direto no `UdpSocketManager.handleConnection`
- Health check do servidor — REST

## Arquitetura UDP no projeto

```
UdpSocketManager (singleton, src/sockets/UdpSocketManager.ts)
  │
  ├─ socket.on('message')  → handleMessage(buffer, rinfo)
  │     │
  │     ├─ JSON.parse → { e: eventName, d: data, token? }
  │     │
  │     ├─ e === 'CONN'   → handleConnection (cria sessão)
  │     ├─ session ausente → ERROR (não autenticado)
  │     │
  │     └─ SocketHandlerFactory.createHandler(e)
  │           └─ handler.handle(server, rinfo, data)
  │
  ├─ broadcastToRoom(roomId, event, data, exceptRinfo?)
  ├─ sendTo(rinfo, event, data)
  ├─ getSession(rinfo)
  └─ cleanupSessions()   ← intervalo 10s, expira sessões >30s sem heartbeat
```

**Sessão** (`PlayerSession`) é em memória, keyed por `address:port`:

```ts
interface PlayerSession {
    id: string;            // userId (do JWT ou dev bypass)
    address: string;
    port: number;
    roomId?: string;
    playerName?: string;
    lastPosition?: number[];
    lastRotation?: number;
    lastSeen: number;
    movePacketCount: number;
    isSprinting?: boolean;
}
```

## Packet shape on the wire

```json
{ "e": "<event_name>", "d": { ... }, "token": "..." }
```

- `e`: nome do evento (string, snake_case ou UPPER_SNAKE)
- `d`: payload — pode ser qualquer JSON
- `token`: só em `CONN`. Demais eventos identificam o player pela **sessão** (via `address:port`).

Respostas saem com o mesmo shape sem `token`. Chaves dentro de `d` usam **convenção compacta para alta frequência** — ver `cross-repo-communication-sync`.

## Ordem de criação

```
1. cross-repo-communication-sync §workflow         ← faz constante C# + DTO + tabela MD ANTES
2. src/sockets/handlers/<Evento>.Handler.ts        ← classe implementa ISocketHandler
3. src/sockets/handlers/<Evento>.Handler.test.ts   ← spec lado-a-lado (TDD)
4. src/sockets/factories/SocketHandler.Factory.ts  ← registra no static block
5. hora-extra-backend/docs/Networking/<Evento>.md  ← doc curta da feature (skill cross-repo-docs-discipline)
```

Você sempre faz o passo 1 PRIMEIRO. Sem ele, o cliente nem sabe a string do evento — e você não tem como testar end-to-end.

## Passo 2 — Implementar o handler

`hora-extra-backend/src/sockets/handlers/PlayerEmote.Handler.ts`:

```ts
import { RemoteInfo } from 'dgram';
import { ISocketHandler } from '../types/SocketEvent.js';
import logger from '../../utils/Logger.js';

/**
 * Payload esperado: { id: string, d?: number }
 * - id: emote id ("wave", "dance", ...)
 * - d: duração em ms (opcional; default 2000)
 */
interface PlayerEmoteData {
    id: string;
    d?: number;
}

const VALID_EMOTES = new Set(['wave', 'dance', 'point', 'cheer']);
const DEFAULT_DURATION_MS = 2000;
const MAX_DURATION_MS = 5000;

export class PlayerEmoteHandler implements ISocketHandler {
    public async handle(server: any, rinfo: RemoteInfo, data: PlayerEmoteData): Promise<void> {
        // 1. Validação de payload (server-authoritative — nunca confie no cliente)
        if (!data || typeof data.id !== 'string' || !VALID_EMOTES.has(data.id)) {
            logger.warn(`Emote inválido recebido de ${rinfo.address}:${rinfo.port}`, { module: 'UDP_SOCKET' });
            return;
        }

        const duration = Math.min(
            Math.max(data.d ?? DEFAULT_DURATION_MS, 0),
            MAX_DURATION_MS,
        );

        // 2. Sessão
        const session = server.getSession(rinfo);
        if (!session || !session.roomId) {
            return;
        }

        // 3. Broadcast pros outros na sala (inclusive o próprio se quiser feedback)
        const payload = {
            playerId: session.id,
            id: data.id,
            d: duration,
        };

        server.broadcastToRoom(session.roomId, 'player_emote', payload, rinfo);

        logger.debug(`[UDP_SOCKET] Emote '${data.id}' de ${session.id} em ${session.roomId}`, { module: 'UDP_SOCKET' });
    }
}
```

### Convenções obrigatórias

1. **`implements ISocketHandler`** — interface de `src/sockets/types/SocketEvent.js`.
2. **`async handle(server: any, rinfo: RemoteInfo, data: any): Promise<void>`** — assinatura fixa. `server: any` evita dep circular com `UdpSocketManager`.
3. **Tipo do `data` é específico** quando shape é conhecido (`data: PlayerEmoteData`).
4. **Validação primeiro, sempre.** Server-authoritative: nunca propague payload sem validar tipos, ranges, whitelists.
5. **Retorno silencioso (`return`) em payload inválido.** Não throw, não broadcast. Apenas log warn.
6. **Acesso à sessão via `server.getSession(rinfo)`** — não invente outro mecanismo.
7. **Broadcast via `server.broadcastToRoom(roomId, eventName, payload, exceptRinfo?)`** — `exceptRinfo` exclui o remetente (típico em high-frequency pra economizar banda).
8. **Send direto via `server.sendTo(rinfo, eventName, payload)`** quando responde só a quem mandou.
9. **Logs**: `logger.warn` em validações falhas, `logger.debug` em eventos de alta frequência, `logger.info` só em ciclo de vida (raro em handler).
10. **Imports com `.js`** (NodeNext).

## Padrões por categoria de handler

### A. Update de estado próprio (cliente → broadcast)

Ex: `PlayerSprintHandler`, `PlayerMoveHandler`, `PlayerEmoteHandler`.

```ts
// 1. valida payload
// 2. atualiza session.<campo>
// 3. broadcast pra sala (exceto remetente em high-frequency)
```

### B. Request/response (cliente → servidor → cliente)

Ex: `PingHandler` — recebe ping, devolve pong só pra quem perguntou.

```ts
public async handle(server: any, rinfo: RemoteInfo, data: { t: number }): Promise<void> {
    server.sendTo(rinfo, 'pong', { t: data.t, server: Date.now() });
}
```

### C. Comando que afeta estado da sala (NPC, item, sala)

Ex: `NpcRegisterHandler` (gerencia state map de NPCs por sala — estático).

```ts
// 1. valida
// 2. atualiza state global (Map<roomId, NpcState[]>)
// 3. broadcast 'npc_state' pra sala
```

> Se o handler **mantém state global** (Map estático), exponha método de limpeza `static clearRoomState(roomId)` pra `UdpSocketManager.resetRoomState()` chamar.

### D. Entrada em sala (`join_room`)

Já existe `JoinRoomHandler`. Padrão: setar `session.roomId`, broadcast `room_joined` pra própria sessão (snapshot inicial) + `player_joined` pros outros.

## Passo 3 — Spec do handler

`PlayerEmote.Handler.test.ts` (mesma pasta):

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerEmoteHandler } from './PlayerEmote.Handler.js';

describe('PlayerEmoteHandler', () => {
    let handler: PlayerEmoteHandler;
    let mockServer: any;
    const rinfo = { address: '127.0.0.1', port: 5001 } as any;

    beforeEach(() => {
        handler = new PlayerEmoteHandler();
        mockServer = {
            getSession: vi.fn(),
            broadcastToRoom: vi.fn(),
        };
    });

    it('broadcast emote válido para a sala (exceto remetente)', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });

        await handler.handle(mockServer, rinfo, { id: 'wave' });

        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'r1',
            'player_emote',
            { playerId: 'p1', id: 'wave', d: 2000 },
            rinfo,
        );
    });

    it('ignora emote não whitelist', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        await handler.handle(mockServer, rinfo, { id: 'cheat-emote' });
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });

    it('ignora payload sem id', async () => {
        await handler.handle(mockServer, rinfo, {} as any);
        expect(mockServer.getSession).not.toHaveBeenCalled();
    });

    it('clampa duração ao MAX_DURATION_MS', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1', roomId: 'r1' });
        await handler.handle(mockServer, rinfo, { id: 'wave', d: 999999 });
        expect(mockServer.broadcastToRoom).toHaveBeenCalledWith(
            'r1', 'player_emote',
            expect.objectContaining({ d: 5000 }),
            rinfo,
        );
    });

    it('não broadcast se sessão não tem roomId', async () => {
        mockServer.getSession.mockReturnValue({ id: 'p1' });  // sem roomId
        await handler.handle(mockServer, rinfo, { id: 'wave' });
        expect(mockServer.broadcastToRoom).not.toHaveBeenCalled();
    });
});
```

## Passo 4 — Registrar no SocketHandlerFactory

`hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts`:

```ts
import { JoinRoomHandler } from '../handlers/JoinRoom.Handler.js';
import { PlayerMoveHandler } from '../handlers/PlayerMove.Handler.js';
import { NpcMoveHandler } from '../handlers/NpcMove.Handler.js';
import { NpcRegisterHandler } from '../handlers/NpcRegister.Handler.js';
import { PlayerSprintHandler } from '../handlers/PlayerSprint.Handler.js';
import { PingHandler } from '../handlers/Ping.Handler.js';
import { PlayerEmoteHandler } from '../handlers/PlayerEmote.Handler.js';   // ← novo

export class SocketHandlerFactory {
    private static handlers = new Map<string, SocketHandlerConstructor>();

    static {
        this.handlers.set('join_room', JoinRoomHandler);
        this.handlers.set('player_move', PlayerMoveHandler);
        this.handlers.set('npc_move_request', NpcMoveHandler);
        this.handlers.set('npc_register', NpcRegisterHandler);
        this.handlers.set('player_sprint', PlayerSprintHandler);
        this.handlers.set('ping', PingHandler);
        this.handlers.set('player_emote', PlayerEmoteHandler);             // ← novo
    }

    // createHandler, getRegisteredEvents...
}
```

> **Esqueceu de registrar?** O `UdpSocketManager` loga `warn` "Evento desconhecido recebido via UDP: player_emote". Cliente bate, ninguém responde, jogador acha que o servidor sumiu.

## Anti-cheat / Server-authoritative — exemplos práticos

### Movimento (PlayerMoveHandler)

Cliente reporta `{ p: [x, y, z], r: yaw }`. Servidor valida:

- **Velocidade máxima**: distância entre `session.lastPosition` e novo `p` ÷ delta tempo. Se >maxSpeed → ignora ou clampa.
- **Bounding box**: a sala tem limites; `x` e `z` precisam estar dentro.
- **Sprint flag coerente**: se `session.isSprinting === false`, maxSpeed é menor; se `true`, maior.

```ts
const dt = (Date.now() - session.lastSeen) / 1000;
const distance = Math.hypot(
    data.p[0] - (session.lastPosition?.[0] ?? data.p[0]),
    data.p[2] - (session.lastPosition?.[2] ?? data.p[2]),
);
const maxSpeed = session.isSprinting ? SPRINT_SPEED : WALK_SPEED;
if (distance / dt > maxSpeed * 1.2) {  // 20% tolerância pra jitter
    logger.warn(`Speed cheat de ${session.id}: ${distance / dt} > ${maxSpeed}`, { module: 'UDP_SOCKET' });
    return;  // dropa o pacote; cliente vai reconciliar na próxima
}
session.lastPosition = data.p;
session.lastRotation = data.r;
```

### Sala (JoinRoomHandler)

- **Limite de jogadores**: conta sessões na sala antes de aceitar.
- **Sala existe**: rooms persistidas no DB; valida via `ServiceFactory.getRoomService().findById(roomId)`.
- **Senha de sala** (se vier): hash compare.

### Emote (PlayerEmoteHandler)

- **Whitelist de emote ids** — evita o cliente broadcast emote arbitrário.
- **Duração clampada** — evita "stuck dance" abusivo.
- **Rate limit por sessão** (opcional, pra spam): `session.lastEmoteAt`.

## Tick rate 20Hz — quando aplicar

`backend-design-pattern.md §4` exige loop de 20Hz (50ms) pra `state_update` agregado. Hoje (verifique no código), broadcasts são **on-demand por evento** (player_move broadcast individual). Tem espaço pra um `StateBroadcaster` centralizado:

- `setInterval(() => broadcastSnapshotAllRooms(), 50)`
- Vai em `src/sockets/services/StateBroadcaster.ts` (criar) + registrar startup em `index.ts`.

> **Não introduza o broadcaster centralizado sem o usuário pedir.** O design atual é per-event. Mudar pra snapshot agregado é decisão arquitetural.

## Checklist por handler

- [ ] Skill `cross-repo-communication-sync` consultada: constante C#, DTO, tabela MD prontos ANTES
- [ ] Handler em `src/sockets/handlers/<Evento>.Handler.ts`
- [ ] Classe `<Evento>Handler` implementa `ISocketHandler`
- [ ] Tipo do `data` é específico (interface no topo do arquivo)
- [ ] Validação **antes** de tocar sessão / broadcast
- [ ] Whitelists / ranges / sanity checks para dados de gameplay
- [ ] `server.getSession(rinfo)` checa `session && session.roomId` antes de prosseguir
- [ ] Logs: `debug` pra alta frequência, `warn` pra payload inválido / cheat, `error` só pra exceção
- [ ] **Registrado** em `SocketHandlerFactory` static block
- [ ] Spec `.test.ts` lado-a-lado cobrindo happy + 2-3 cenários inválidos
- [ ] Chaves compactas em alta frequência (`p`, `r`, `v`)
- [ ] Imports com `.js`
- [ ] Doc em `hora-extra-backend/docs/Networking/<Evento>.md`

## Gotchas

1. **Esquecer de registrar na Factory** = evento "desconhecido", silently dropped. Cliente fica olhando pro server sem resposta.
2. **`server: any`** é proposital — não tipar como `UdpSocketManager` pra evitar circular.
3. **`broadcastToRoom` com `exceptRinfo`** exclui APENAS quem mandou. Pra incluir o remetente (ex: confirmação), omita esse arg.
4. **Sessão sem `roomId`** = jogador conectado mas não entrou em sala. Maioria dos eventos deve `return` cedo se faltar.
5. **`session.lastSeen` é atualizado AUTOMATICAMENTE** pelo `UdpSocketManager` antes de chamar o handler. Não precisa setar de novo.
6. **NPC handlers compartilham state** via `static` map no próprio handler — funciona em single-instance. Não escalável horizontalmente sem mover pra Redis (fora de escopo TCC).
7. **Throws no handler escapam** pro try/catch do `UdpSocketManager.handleMessage` — loga `error`. **Prefira retornar** em vez de throw em validação (semantica: "dropa pacote", não "explodiu").
8. **`broadcastToRoom` é O(N) sobre todas as sessões** — para salas grandes, ineficiente. Hoje OK (4–8 jogadores). Não otimizar prematuramente.
9. **Dev bypass**: token `DEV_TEST_TOKEN` em dev → sessão criada com `playerId = DEV_TEST_USER_ID` e auto-join "dev-room". Logs ficam diferentes — tudo bem.
10. **`data` pode ser `undefined`** se cliente mandar pacote mal formado. Primeira linha do `if (!data || ...)` cobre.

## Referências no código

- `hora-extra-backend/src/sockets/UdpSocketManager.ts` — onde tudo orquestra (`handleMessage`, `broadcastToRoom`, `sendTo`, `getSession`)
- `hora-extra-backend/src/sockets/types/SocketEvent.ts` — `ISocketHandler` interface
- `hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts` — registry
- `hora-extra-backend/src/sockets/handlers/PlayerSprint.Handler.ts` — exemplo simples e canônico
- `hora-extra-backend/src/sockets/handlers/PlayerSprint.Handler.test.ts` — template de teste
- `hora-extra-backend/src/sockets/handlers/PlayerMove.Handler.ts` — exemplo com validação de velocidade
- `hora-extra-backend/src/sockets/handlers/NpcRegister.Handler.ts` — exemplo com state estático compartilhado
- `hora-extra-backend/docs/Networking/COMMUNICATION.md` — contrato cross-language
- `.agents/rules/backend-design-pattern.md` — rule humana
- `.agents/rules/backend-factory-pattern.md` — rule da factory
