---
name: cross-repo-communication-sync
description: Aplicar SEMPRE que adicionar/modificar evento de rede UDP, payload, ou chave compacta. Mantém o contrato cross-language entre `hora-extra-backend/docs/Networking/COMMUNICATION.md`, `NetworkEvents.cs` (Unity) e os tipos TypeScript (`src/sockets/types/`). Skill mais citada — outras (`backend-new-udp-handler`, `client-new-network-event`) cross-link aqui.
applies_to: both
---

# cross-repo-communication-sync — Manter o contrato de rede em lockstep

## Por que essa skill existe

Hora-Extra usa UDP datagram nativo (`dgram`, não Socket.IO) com pacotes JSON
compactos. **Três artefatos têm de descrever o mesmo evento**:

1. `hora-extra-backend/docs/Networking/COMMUNICATION.md` — fonte de verdade humana
2. `hora-extra-backend/src/sockets/handlers/<Event>.Handler.ts` — implementação servidor + tipo TS do payload (inline ou em `src/sockets/types/`)
3. `hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs` — constante string + DTO C# em `Network/Models/`

Se um divergir, **o jogo quebra silenciosamente em produção** (parsing falha, evento dropado, posição congelada). A rule `.agents/rules/communication-sync-rule.md` torna essa sincronia obrigatória.

## Quando aplicar

- Adicionar novo evento UDP (em qualquer direção: C→S, S→C, bidir)
- Renomear evento existente
- Mudar shape do payload (adicionar/remover/renomear chave; mudar tipo)
- Mudar direção de propagação (ex: era só S→C, agora também C→S)
- Mudar chave compacta (ex: `position` → `p`, `velocity` → `v`)

## Quando NÃO aplicar

- Refator interno do handler sem mudar payload na wire
- Mudança de comportamento server-side sem novo campo no pacote
- Logs/Debug que nunca saem do servidor

## Single source of truth

`E:\PUC\hora-extra\hora-extra-backend\docs\Networking\COMMUNICATION.md`

Esse arquivo tem hoje (pode estar desatualizado — sempre conferir antes de editar):

- §1 Visão Geral (protocolo, formato JSON, porta UDP 5001, tick 20Hz)
- §2 Fluxo de conexão (handshake CONN → CONN_SUCCESS / CONN_ERROR)
- §3 Tabela **Cliente → Servidor**
- §4 Tabela **Servidor → Cliente**
- §5 Schemas (Player, PlayerUpdate)
- §7 Bypass de desenvolvimento (`DEV_TEST_TOKEN`)

**Ao adicionar evento, edite a tabela correta (§3 ou §4) e atualize §5 se inventar um schema novo.**

> ⚠️ O texto atual de §1 ainda diz "Socket.io (Engine.IO v4)" e a porta `http://localhost:3000`. **Esse trecho é legado.** A stack real é UDP nativo na porta 5001. Se sua mudança tocar §1, oportunidade pra corrigir esse trecho — confirmar com o usuário antes.

## Workflow obrigatório (4 passos em ordem)

```
1. NetworkEvents.cs                — constante C# (a fonte humana lê primeiro)
2. src/sockets/types/ (TS)         — tipo TypeScript do payload + DTO C# em Network/Models/
3. COMMUNICATION.md                — tabela atualizada
4. Handler + listener no cliente   — código que CONSOME os 3 acima
```

Se você inverter a ordem, vai produzir código com strings desalinhadas porque uma das pontas vai compilar com o nome antigo.

### Passo 1 — Constante em `NetworkEvents.cs`

`hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs`:

```csharp
public static class NetworkEvents
{
    // === C → S (Cliente → Servidor) ===
    public const string JOIN_ROOM     = "join_room";
    public const string PLAYER_MOVE   = "player_move";
    public const string PLAYER_SPRINT = "player_sprint";
    public const string PING          = "ping";
    public const string PLAYER_EMOTE  = "player_emote";   // ← NOVO

    // === S → C (Servidor → Cliente) ===
    public const string CONN_SUCCESS    = "CONN_SUCCESS";
    public const string CONN_ERROR      = "CONN_ERROR";
    public const string ROOM_JOINED     = "room_joined";
    public const string STATE_UPDATE    = "state_update";
    public const string PLAYER_EMOTE_BC = "player_emote"; // mesmo string; broadcast pra outros
}
```

**Convenções:**

- Strings em `snake_case`. Eventos handshake/sistema em `UPPER_SNAKE` (`CONN`, `CONN_SUCCESS`, `ERROR`).
- Quando o evento é o mesmo string em ambas as direções (C→S envia, S→C broadcast), use 2 constantes C# com o mesmo valor mas nomes diferentes para deixar a intenção clara nos `On(...)` listeners.
- Comente direção C→S vs S→C com headers de seção.

### Passo 2a — Tipo TypeScript do payload

Padrão atual no projeto: tipo do `data` é inline no handler (`data: { s: boolean }` em `PlayerSprintHandler`). Para payloads com 3+ campos, crie tipo nomeado em `src/sockets/types/`:

```ts
// hora-extra-backend/src/sockets/types/PlayerEmote.ts
export interface PlayerEmoteRequest {
    id: string;      // emote id (1=wave, 2=dance, …)
    d?: number;      // duration ms (opcional)
}

export interface PlayerEmoteBroadcast {
    playerId: string;
    id: string;
    d: number;
}
```

> ⚠️ **ESM `.js` extension obrigatório nos imports** entre arquivos do projeto: `import { PlayerEmoteRequest } from '../types/PlayerEmote.js';` — mesmo o arquivo de disco sendo `.ts`. NodeNext resolution.

### Passo 2b — DTO C# em `Network/Models/`

`hora-extra-client/Assets/Scripts/Network/Models/PlayerEmote.cs`:

```csharp
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    public class PlayerEmoteRequest
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("d")]  public int? Duration;
    }

    public class PlayerEmoteBroadcast
    {
        [JsonProperty("playerId")] public string PlayerId;
        [JsonProperty("id")]       public string Id;
        [JsonProperty("d")]        public int Duration;
    }
}
```

**Mapeamento de chaves compactas via `[JsonProperty]` é OBRIGATÓRIO** — sem ele, Newtonsoft.Json não acha o campo (`p` no JSON ≠ `Position` no C#).

### Passo 3 — Atualizar `COMMUNICATION.md`

Adicionar linha na tabela §3 (C→S) **e** na tabela §4 (S→C):

```markdown
## 3. Eventos: Cliente -> Servidor

| Evento          | Payload                                          | Descrição                                |
| :-------------- | :----------------------------------------------- | :--------------------------------------- |
| ...             | ...                                              | ...                                      |
| `player_emote`  | `{ "id": string, "d"?: number }`                 | Jogador dispara emote (id 1=wave, 2=dance). `d` em ms (default 2000). |

## 4. Eventos: Servidor -> Cliente

| Evento          | Payload                                          | Descrição                                |
| :-------------- | :----------------------------------------------- | :--------------------------------------- |
| ...             | ...                                              | ...                                      |
| `player_emote`  | `{ "playerId": string, "id": string, "d": number }` | Broadcast do emote pros outros na sala. |
```

**Documente chaves compactas** (`p`, `r`, `v`, `s`, `d`) com nome curto e a semântica longa em parênteses no header da tabela (ou em §5 Schemas).

### Passo 4 — Implementação

- Backend: criar handler + registrar em `SocketHandlerFactory` (skill `backend-new-udp-handler`)
- Cliente: assinar via `SocketManager.On(NetworkEvents.PLAYER_EMOTE_BC, OnEmoteBroadcast)` em `OnEnable` (skill `client-new-network-event`)

## Chaves compactas: convenção

Para eventos de **alta frequência** (>1 Hz por jogador, ex: `player_move`, `state_update`, `npc_move`), use chaves de 1 letra:

| Chave longa | Compacta | Uso                                  |
| ----------- | -------- | ------------------------------------ |
| `position`  | `p`      | `number[]` `[x, y, z]`               |
| `rotation`  | `r`      | `number` (yaw em graus)              |
| `velocity`  | `v`      | `number[]` `[vx, vy, vz]`            |
| `sprint`    | `s`      | `boolean`                            |
| `tick`      | `t`      | `number` (server tick)               |
| `id`        | `id`     | mantém `id` — já é curto             |
| `roomId`    | `roomId` | só em handshake; baixa frequência    |

Para eventos de **baixa frequência** (handshake, join, chat), pode usar nomes longos (`playerName`, `roomId`, `message`). Não compacte por compacidade — só onde a economia de bytes realmente importa.

## Smoke test cross-log

Depois de implementar, valide que os 2 lados estão alinhados:

1. Rode o backend (`cd hora-extra-backend && npm run dev`).
2. Abra o Unity, entre em Play Mode com a feature acionada.
3. **Logs do backend** (Winston, `{ module: 'UDP_SOCKET' }`): deve aparecer `Evento recebido: player_emote` ou similar — confirma que o nome bateu.
4. **Console do Unity** (`[NETWORK]` prefix): deve aparecer `Recebido player_emote de <id>` — confirma que o broadcast voltou e foi parseado.

Se **um lado loga e o outro não**, é desalinhamento: o nome do evento, a forma do payload, ou o `[JsonProperty]` está errado.

## Checklist final (não pular)

- [ ] `NetworkEvents.cs` tem constante na seção correta (C→S ou S→C)
- [ ] Tipo TS criado ou inline no handler usa as mesmas chaves do JSON real
- [ ] DTO C# em `Network/Models/` com `[JsonProperty]` para CADA chave compacta
- [ ] Linha adicionada/editada em `COMMUNICATION.md` §3 e/ou §4
- [ ] Chaves compactas (1 letra) só em eventos de alta frequência
- [ ] Backend e cliente referenciam o **mesmo string literal** (via constante, nunca hardcoded no gameplay)
- [ ] Smoke test cross-log passou: backend log + Unity log no mesmo Play Mode

## Gotchas

1. **Não trate `COMMUNICATION.md` §1 como verdade absoluta hoje.** O texto fala em Socket.io/porta 3000 — é legado. UDP+5001 é o real (ver `hora-extra-backend/src/index.ts` e `UdpSocketManager.ts`).
2. **Newtonsoft.Json é case-sensitive nos `JsonProperty`** mas C# é case-sensitive nas propriedades. Garanta que `[JsonProperty("p")]` mapeia para `public float[] P;` (qualquer nome C#, desde que o atributo bata com o JSON).
3. **Esquecer o `[JsonProperty]` em chave compacta** = campo silenciosamente vira default (zero, null). Não há erro de parse — só dados errados em runtime.
4. **`NetworkEvents.cs` constante sem usar a constante** = dead code. Reviewer flagga string literal hardcoded em handler/listener.
5. **Mudar payload sem versionar** = clientes antigos + servidor novo se confundem em campo. Para mudanças breaking, considere coordenar com o usuário um window de update (matar conexões antigas no servidor).
6. **Backend usa ESM** — `import { Foo } from '../types/Foo.js'` (extensão `.js` obrigatória mesmo em `.ts` no disco). Skip isso = `ERR_MODULE_NOT_FOUND` no runtime.

## Referências no código

- `hora-extra-backend/docs/Networking/COMMUNICATION.md` — single source of truth
- `hora-extra-backend/src/sockets/handlers/PlayerSprint.Handler.ts` — exemplo de payload compacto inline (`{ s: boolean }`)
- `hora-extra-backend/src/sockets/handlers/PlayerMove.Handler.ts` — usa chaves `p`, `r`
- `hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs` — catálogo C# de constantes
- `hora-extra-client/Assets/Scripts/Network/Models/` — DTOs C# com `[JsonProperty]`
- `hora-extra-client/Assets/Scripts/Network/SocketManager.cs` — `SendEvent(name, payload)` / `On(name, callback)`
- `.agents/rules/communication-sync-rule.md` — fonte humana dessa skill
