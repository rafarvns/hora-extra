# Protocolo de Comunicação em Tempo Real — Hora-Extra

Esta documentação define o contrato de comunicação entre o cliente Unity e o servidor Node.js.
É a fonte de verdade para qualquer mudança de payload UDP — alterar aqui, alterar no handler TS **e** no DTO C# no mesmo commit.

---

## 1. Visão Geral

| Campo         | Valor                                            |
| :------------ | :----------------------------------------------- |
| Protocolo     | **UDP Datagram nativo** (`dgram`, não Socket.IO) |
| Porta UDP     | `5001` (configurável via `UDP_PORT` no `.env`)   |
| Porta REST    | `5000` (configurável via `PORT` no `.env`)       |
| Formato       | JSON compacto: `{ "e": "<evento>", "d": <payload>, "token"?: "<jwt>" }` |
| Tick de sync  | 20 Hz (a cada 50 ms) — broadcastado pelo servidor |
| Alta frequência | chaves de 1 letra (`p`, `r`, `v`, `s`, `t`) pra economizar banda |

> **Chaves compactas** — usadas apenas em eventos emitidos mais de 1 vez por segundo por jogador:
> `p` = position `[x,y,z]`, `r` = rotation (yaw graus), `v` = velocity `[vx,vy,vz]`,
> `s` = isSprinting `boolean`, `t` = server tick `number`.

---

## 2. Entrada numa sala (sessão → sala → CONN)

Não há cadastro nem login: o jogador só digita o nome. Salas vivem em memória
(`RoomRegistry`) com **no máximo 2 jogadores**.

```
Cliente                                  Servidor
  |── POST /api/session { nome } ─────────►|  valida nome (2–16) → gera playerId "p-…"
  |◄── 201 { token, playerId, nome } ──────|  JWT { id, n }
  |── POST /api/rooms { nome } ───────────►|  (só quem cria) sala vazia, reservada 60 s
  |◄── 201 { id, nome } ───────────────────|
  |── CONN { token, d: { roomId } } ──────►|  valida token, sala e vaga
  |◄── CONN_SUCCESS { id, sessionKey, roomId, n }
  |◄── room_state { t, pl } ───────────────|  retrato da sala (quem entra no meio)
  |    (os outros recebem player_joined + room_roster)
  |── leave_room ─────────────────────────►|  ao sair: libera a vaga na hora
```

| REST | Entrada | Saída |
| :--- | :------ | :---- |
| `POST /api/session` | `{ nome }` (2–16 caracteres, espaços colapsados) | `{ token, playerId, nome }` — 400 se o nome for inválido |
| `GET /api/rooms` | — | `[{ id, nome, jogadores, max: 2, emAndamento }]`, mais novas primeiro |
| `POST /api/rooms` | `{ nome }` (2–24) | `{ id, nome }` |

Regras:
- O **primeiro** pacote UDP deve ser `CONN`. Pacotes sem sessão recebem `ERROR`.
- O **nome vem do token**; o servidor ignora qualquer nome no payload.
- `CONN_ERROR { code, message }` com `code`: `INVALID_TOKEN`, `ROOM_NOT_FOUND`, `ROOM_FULL`.
- O limite conta **playerIds distintos**: o mesmo jogador reconectando (outra porta) substitui a
  sessão antiga, sem ocupar vaga e sem anunciar entrada de novo.
- Saída = `leave_room` ou 30 s sem pacotes. Na saída o servidor avisa `player_disconnected` +
  `room_roster`, devolve a tarefa aberta do jogador ao pool (com o progresso que tinha), a
  entrega a quem estiver sem tarefa (`task_assigned` sem pedido) e passa o mestre dos NPCs.
- Sala vazia há mais de 60 s é apagada junto com tarefas, relógio e NPCs dela.
- Todos começam no 1º andar, inclusive quem entra no meio da partida.

---

## 3. Eventos: Cliente → Servidor

| Evento              | Payload                                                                 | Descrição                                                          |
| :------------------ | :---------------------------------------------------------------------- | :----------------------------------------------------------------- |
| `CONN`              | `{ e: "CONN", token: string, d: { roomId: string } }`                   | Handshake: entra na sala `roomId` (ver §2). O cliente reenvia até 3× sem resposta. |
| `leave_room`        | `{}`                                                                    | Saiu da partida (menu ou fechou o jogo). Libera a vaga na hora.    |
| `player_move`       | `{ p: number[], r: number, f?: number }`                                | Posição `[x,y,z]`, rotação (yaw) e andar `f` (1/2/3). Enviado também ao chegar num andar, mesmo parado. |
| `player_sprint`     | `{ s: boolean }`                                                        | Ativar/desativar corrida.                                          |
| `npc_register`      | `{ id: string, type: string, p, r, f?: number }`                        | Registrar NPC presente na cena; `f` = andar do NPC. Servidor decide mastership (§6). |
| `npc_move_request`  | `{ id: string, p: number[], r: number }`                                | Master do NPC envia nova posição autoritativa.                     |
| `ping`              | `{ timestamp: number }`                                                 | Mede latência RTT.                                                 |
| `task_catalog_register` | `{ tasks: TaskEntry[] }` — ver schema na §5 | Registra o catálogo de tarefas da sala. **Só o primeiro registro da sala vale**: quem entra depois manda o mesmo catálogo e ele é ignorado (não zera o progresso). `npcId` removido — catálogo é da sala, não vinculado a NPC. Cada entrada aceita `items?` e `pairs?` (opcionais); campo malformado é descartado com `warn` e o resto do catálogo é registrado. |
| `task_assign_request`   | `{}` (sem campos)                                                   | Pede uma tarefa. As tarefas são **da sala**: cada uma é feita uma vez, por quem pegar. O jogador recebe UMA tarefa livre (não concluída e sem dono; a 1ª do catálogo tem prioridade, o resto é sorteado). Com tarefa aberta, ou sem nenhuma livre, não há resposta. `playerId` vem da sessão. |
| `task_start_interaction` | `{ taskId: string }`                                               | Jogador inicia interação com objeto de task. Servidor valida posse e transição `pending → in_progress`. |
| `task_complete_attempt`  | `{ taskId: string, success: boolean }`                             | Jogador reporta resultado do minigame QTE. Servidor determina status final autoritativamente. |
| `task_progress`          | `{ taskId: string, itemId?: string, slotId?: string }`             | Jogador entregou +1 item de uma task incremental (ex: `collect`). Servidor soma +1, faz `pending → in_progress` no primeiro incremento e `completed` ao atingir `targetCount`. Nunca confia em contagem do cliente. `itemId` identifica **qual** item e `slotId` **onde** foi entregue; ambos são opcionais e só validados quando a entrada de catálogo declara `items`/`pairs`. Recusa de gameplay responde `task_rejected`; `itemId`/`slotId` de tipo errado respondem `ERROR` (falha de protocolo). |
| `shift_clock_request`    | `{}` (sem campos)                                                  | Pede o relógio do expediente da sala. O **primeiro** pedido da sala inicia o expediente (o cliente envia ao fim da intro). Pedidos seguintes só sincronizam (o cliente reenvia a cada ~30 s para corrigir drift). Resposta: `shift_clock` (§4). |

---

## 4. Eventos: Servidor → Cliente

| Evento              | Payload                                                                 | Descrição                                                          |
| :------------------ | :---------------------------------------------------------------------- | :----------------------------------------------------------------- |
| `CONN_SUCCESS`      | `{ id: string, sessionKey: string, roomId: string, n: string }`         | Handshake aceito. `id` = playerId, `n` = nome.                     |
| `CONN_ERROR`        | `{ code: "INVALID_TOKEN" \| "ROOM_NOT_FOUND" \| "ROOM_FULL", message: string }` | Handshake recusado.                                   |
| `room_state`        | `{ t: RoomTaskSnapshot[], pl: Array<{ id, n, p: number[] \| null, r, f }> }` | **Unicast** a quem acabou de entrar (logo após `CONN_SUCCESS`): estado de todas as tarefas da sala (§5) e os outros jogadores com última posição e andar. É assim que quem entra no meio vê o mundo como está. |
| `player_joined`     | `{ id: string, name: string }`                                          | Para os outros da sala: novo jogador entrou (não é enviado em reconexão). |
| `player_disconnected` | `{ id: string }`                                                      | Para os que ficaram: o jogador saiu (`leave_room` ou timeout).     |
| `room_roster`       | `{ p: Array<{ id: string, n: string }> }`                               | Broadcast para a sala com a lista **completa** de jogadores (`id` + nome `n`), a cada entrada e saída. O cliente **substitui** a lista inteira. |
| `shift_clock`       | `{ m: number, s: number, e: number, k: number, st: "idle" \| "running" \| "ended" }` | Relógio do expediente da sala (autoritativo). `m` = minuto de jogo atual (minutos desde 00:00, fracionário), `s`/`e` = início/fim do expediente (480 = 08:00, 1080 = 18:00), `k` = minutos de jogo por segundo real (0 quando parado). O cliente extrapola `m + k·Δt` desde o recebimento — **sem timestamps**, para não depender do relógio local. **Broadcast** quando o pedido iniciou o expediente; **unicast** nas sincronizações. Escala: `SHIFT_DURATION_SECONDS` (padrão 900 s reais para 08:00→18:00). Descartado quando a sala morre (vazia por 60 s). |
| `player_move`       | `{ id: string, p: number[], r: number, f: number }`                     | Broadcast da posição de outro jogador; `f` = andar. O cliente só mostra quem está no mesmo andar. |
| `player_sprint`     | `{ id: string, s: boolean }`                                            | Broadcast do estado de sprint de outro jogador.                    |
| `npc_registered`    | `{ id: string, type: string, isMaster: boolean }`                       | Unicast: resposta ao `npc_register`, ou aviso de que este cliente **virou mestre** (o anterior saiu ou trocou de andar; nesse caso sem `p`). `isMaster=true` = este cliente controla a IA. |
| `npc_move`          | `{ id: string, p: number[], r: number }`                                | Broadcast da posição autoritativa do NPC (emitido pelo master).    |
| `pong`              | `{ timestamp: number }`                                                 | Resposta ao `ping` para cálculo de latência.                       |
| `ERROR`             | `{ message: string }`                                                   | Erro genérico (ex.: pacote recebido sem sessão ativa).             |
| `task_assigned`     | `{ playerId: string, tasks: AssignedTask[], totalTasks: number }`       | Broadcast para a sala: a tarefa entregue ao jogador (sempre **uma**). Emitido (a) na resposta ao `task_assign_request`, (b) a cada tarefa concluída, com a próxima livre, e (c) **sem pedido**, quando a tarefa de quem saiu volta ao pool e este jogador estava sem tarefa. Pode vir com progresso e `ci` (continuação de quem saiu). `totalTasks` = tamanho do catálogo da sala. O cliente **mescla** pelo `id`. |
| `task_updated`      | `{ playerId: string, taskId: string, currentProgress: number, status: string, ci: string[] }` | Broadcast para todos na sala quando o status **ou o progresso** de uma task muda (via `task_start_interaction`, `task_complete_attempt` ou cada `task_progress`). Status: `in_progress`, `completed`, `failed`. `ci` = itens já contados na tarefa (nível sala): o mundo dos outros jogadores usa isso para refletir (mancha limpa some, cofre esvaziado). Construído pelo servidor. |
| `task_rejected`     | `{ taskId: string, code: TaskRejectionCode, message: string, itemId?: string }` | **Unicast ao remetente** (não broadcast): o servidor recusou um `task_progress` por regra de gameplay. `message` é texto em pt-BR pronto para exibir. `itemId` volta quando veio no pedido, para o cliente saber qual item devolver à mão. Ver `TaskRejectionCode` na §5. |

---

## 5. Schemas de dados

### TaskEntry (shape em `task_catalog_register`)

```ts
interface TaskItemPair {
    itemId: string;
    slotId: string;
}

interface TaskEntry {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    items?: string[];        // itemIds que contam progresso nesta task
    pairs?: TaskItemPair[];  // destino obrigatório por item (tarefas de pareamento)
}
```

`items` e `pairs` são **opcionais e retrocompatíveis**: entrada que não declara nenhum dos dois
mantém a contagem cega de +1 por `task_progress` (comportamento anterior ao plano 0004).

Quando **um dos dois** está presente, `task_progress` passa a exigir `itemId`, e o servidor
valida pertencimento, pareamento e não-repetição. Os dois podem coexistir na mesma entrada:
`items` restringe quais itens contam e `pairs` amarra cada item ao seu destino.

Validação de shape no registro (tolerante por desenho — catálogo recusado deixaria o jogador
sem nenhuma tarefa):

| Situação | Comportamento |
| :------- | :------------ |
| `items` não é array | campo descartado + `warn` |
| `items` com elementos não-string | elementos inválidos filtrados + `warn` |
| `pairs` com entrada sem `itemId` ou sem `slotId` | entrada filtrada + `warn` |
| filtragem esvazia o campo | campo inteiro descartado + `warn` (lista vazia é indistinguível de ausente) |

#### Exemplos: tarefas do Financeiro (3º andar)

O servidor não restringe os valores de `type`; eles só precisam bater com o cliente
(`TaskPresentation`). As duas tarefas do Financeiro usam os eventos já existentes, sem payload novo:

| `id` | `type` | `targetCount` | `items` | Fluxo |
| :--- | :----- | :------------ | :------ | :---- |
| `task-cofres-01` | `open_safe_collect_money` | 2 | `cofre-direito`, `cofre-esquerdo` | um `task_progress { taskId, itemId, slotId: "cofre" }` ao fim de cada recolhimento (~10 s) |
| `task-enviar-email-01` | `send_email_upload` | 1 | — | `task_start_interaction` ao abrir o computador (se `pending`) → `task_complete_attempt { success: true }` ao fim da barra de envio |

Limitação conhecida e aceita: a **senha do cofre** (1512 / 2803) e os **10 s de recolhimento**
são conferidos só no cliente. O servidor garante apenas que cada cofre conta uma vez
(`ALREADY_COUNTED`) e que só cofres declarados contam (`UNKNOWN_ITEM`).

### TaskRejectionCode (campo `code` em `task_rejected`)

```ts
type TaskRejectionCode =
    | 'NOT_ASSIGNED'      // o jogador não é o dono da task
    | 'INVALID_STATUS'    // task já completed/failed
    | 'MISSING_ITEM'      // catálogo exige itemId e o payload não mandou
    | 'UNKNOWN_ITEM'      // itemId não pertence a esta task
    | 'WRONG_SLOT'        // par (itemId, slotId) não confere com o catálogo
    | 'ALREADY_COUNTED';  // itemId já contabilizado nesta task (por qualquer dono)
```

Ordem de avaliação em `incrementProgress` (a primeira violação encerra):
`NOT_ASSIGNED` → `INVALID_STATUS` → `MISSING_ITEM` → `UNKNOWN_ITEM` → `WRONG_SLOT` →
`ALREADY_COUNTED`.

**Recusa de gameplay ≠ falha de protocolo.** `task_rejected` significa "a regra do jogo não
permitiu"; o cliente deve devolver o item à mão e mostrar `message`. `ERROR` significa "o pacote
está malformado" e indica bug de cliente. Os dois usam canais distintos de propósito.

### AssignedTask (shape em `task_assigned`)

```ts
interface AssignedTask {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    currentProgress: number; // pode ser > 0 quando a tarefa veio de quem saiu
    status: "pending" | "in_progress" | "completed" | "failed"; // "failed" é terminal
    ci: string[];            // itens já contados (nível sala)
}
```

### RoomTaskSnapshot (linhas de `room_state.t`)

```ts
interface RoomTaskSnapshot {
    id: string;
    s: "pending" | "in_progress" | "completed" | "failed";
    p: number;          // progresso
    ci: string[];       // itens contados
    h: string | null;   // dono atual (aberta) ou quem encerrou; null = livre
}
```

### PlayerSession (servidor, in-memory)

```ts
interface PlayerSession {
    id: string;           // playerId ("p-…", gerado em POST /api/session)
    address: string;
    port: number;
    roomId: string;
    playerName: string;   // vem do token
    lastPosition?: number[];
    lastRotation?: number;
    floor: number;        // 1/2/3 — todos começam no 1º
    lastSeen: number;     // timestamp ms — base do timeout de 30s
    movePacketCount: number;
    isSprinting?: boolean;
}
```

### Pacote UDP (wire format)

```json
{ "e": "player_move", "d": { "p": [1.0, 0.0, 3.5], "r": 90.0 } }
```

---

## 6. NPC Mastership

Cada NPC tem exatamente um "master" por sala: um jogador que está **no mesmo andar** do NPC
(é na cena dele que a IA roda).

- O master executa a IA localmente e envia `npc_move_request`; os outros interpolam `npc_move`.
- O primeiro a registrar (`npc_register`) vira master. Só quem registrou recebe a resposta.
- Se o master sai da sala ou troca de andar, o servidor passa o NPC para outro jogador naquele
  andar (`npc_registered { isMaster: true }` só para ele). Sem ninguém no andar, o próximo que
  registrar assume.

Mapa interno: `roomId:npcId → { master, floor, type }`. Limpo quando a sala morre.

---

## 7. Referências de implementação

| Componente | Arquivo |
| :--------- | :------ |
| Gerenciador UDP | `src/sockets/UdpSocketManager.ts` |
| Factory de handlers | `src/sockets/factories/SocketHandler.Factory.ts` |
| Handler NPC register | `src/sockets/handlers/NpcRegister.Handler.ts` |
| Relógio do expediente | `src/services/ShiftClockService.ts`, `src/sockets/handlers/ShiftClockRequest.Handler.ts` |
| Roster da sala | `UdpSocketManager.getRoomRoster` / `broadcastRoster` |
| Salas (memória, limite 2) | `src/services/RoomRegistry.ts`, `src/api/controllers/RoomController.ts` |
| Sessão só com nome | `src/services/sessionService.ts`, `src/api/controllers/SessionController.ts` |
| Tarefas da sala | `src/services/TaskService.ts` |
| Constantes C# (cliente) | `Assets/Scripts/Network/NetworkEvents.cs` |
| DTOs C# (cliente) | `Assets/Scripts/Network/Models/` |
