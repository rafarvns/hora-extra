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

## 2. Fluxo de Conexão (Handshake)

```
Cliente                         Servidor
  |                                |
  |── CONN { token: "<jwt>" } ───► |  (1º pacote obrigatório)
  |                                |  valida JWT → cria PlayerSession
  |◄── CONN_SUCCESS { id, sessionKey } ──|
  |◄── room_joined { roomId, playerId }  |  (se auto-join ocorreu)
  |                                |
  | (a partir daqui: eventos normais)
```

- O **primeiro** pacote de qualquer cliente deve ser `e: "CONN"` com `token`.
- Pacotes subsequentes sem sessão ativa recebem `e: "ERROR"`.
- Sessões inativas por >30 s são removidas automaticamente (tick de limpeza a cada 10 s).

### 2.1 Bypass de desenvolvimento

Quando `NODE_ENV=development` e o cliente envia `token === DEV_TEST_TOKEN` (valor em `.env`):
- Sessão criada com `playerId = DEV_TEST_USER_ID`.
- Auto-join na sala `"dev-room"`.
- Campo opcional `data.resetRoom: true` apaga todas as sessões e NPCs da dev-room — útil ao reiniciar o Play Mode no editor.

### 2.2 Guest Mode (acesso anônimo)

Ver §7 — Guest Mode para o fluxo completo.

---

## 3. Eventos: Cliente → Servidor

| Evento              | Payload                                                                 | Descrição                                                          |
| :------------------ | :---------------------------------------------------------------------- | :----------------------------------------------------------------- |
| `CONN`              | `{ token: string, playerName?: string, resetRoom?: boolean }`           | Handshake inicial. `resetRoom` só válido no bypass de dev.         |
| `join_room`         | `{ roomId: string, playerName?: string }`                               | Entrar em uma sala (jogadores autenticados com conta real).        |
| `player_move`       | `{ p: number[], r: number }`                                            | Posição `[x,y,z]` e rotação (yaw) do jogador local.               |
| `player_sprint`     | `{ s: boolean }`                                                        | Ativar/desativar corrida.                                          |
| `npc_register`      | `{ id: string, type: string }`                                          | Registrar NPC presente na cena. Servidor decide mastership.        |
| `npc_move_request`  | `{ id: string, p: number[], r: number }`                                | Master do NPC envia nova posição autoritativa.                     |
| `ping`              | `{ timestamp: number }`                                                 | Mede latência RTT.                                                 |
| `task_catalog_register` | `{ tasks: TaskEntry[] }` — ver schema na §5 | Registra o catálogo de tarefas disponíveis para a sala. Sobrescreve a pool anterior. `npcId` removido — catálogo é da sala, não vinculado a NPC. Cada entrada aceita `items?` e `pairs?` (opcionais); campo malformado é descartado com `warn` e o resto do catálogo é registrado. |
| `task_assign_request`   | `{}` (sem campos)                                                   | Solicita ao servidor que sorteie N=3 tasks para o jogador. `playerId` vem da sessão — não é enviado no payload. **A resposta traz apenas a PRIMEIRA**: as demais ficam numa fila e são liberadas uma a uma conforme o jogador conclui (ver §4, `task_assigned`). |
| `task_start_interaction` | `{ taskId: string }`                                               | Jogador inicia interação com objeto de task. Servidor valida posse e transição `pending → in_progress`. |
| `task_complete_attempt`  | `{ taskId: string, success: boolean }`                             | Jogador reporta resultado do minigame QTE. Servidor determina status final autoritativamente. |
| `task_progress`          | `{ taskId: string, itemId?: string, slotId?: string }`             | Jogador entregou +1 item de uma task incremental (ex: `collect`). Servidor soma +1, faz `pending → in_progress` no primeiro incremento e `completed` ao atingir `targetCount`. Nunca confia em contagem do cliente. `itemId` identifica **qual** item e `slotId` **onde** foi entregue; ambos são opcionais e só validados quando a entrada de catálogo declara `items`/`pairs`. Recusa de gameplay responde `task_rejected`; `itemId`/`slotId` de tipo errado respondem `ERROR` (falha de protocolo). |

---

## 4. Eventos: Servidor → Cliente

| Evento              | Payload                                                                 | Descrição                                                          |
| :------------------ | :---------------------------------------------------------------------- | :----------------------------------------------------------------- |
| `CONN_SUCCESS`      | `{ id: string, sessionKey: string }`                                    | Handshake aceito. `id` = playerId (ou guestId).                    |
| `CONN_ERROR`        | `{ message: string }`                                                   | Handshake rejeitado (token inválido ou ausente).                   |
| `room_joined`       | `{ roomId: string, playerId: string, message: string }`                 | Confirmação de entrada na sala (auto-join ou `join_room`).         |
| `player_joined`     | `{ id: string, playerName: string }`                                    | Broadcast para a sala: novo jogador entrou.                        |
| `player_move`       | `{ id: string, p: number[], r: number }`                                | Broadcast da posição de outro jogador.                             |
| `player_sprint`     | `{ id: string, s: boolean }`                                            | Broadcast do estado de sprint de outro jogador.                    |
| `npc_registered`    | `{ id: string, type: string, isMaster: boolean }`                       | Resposta ao `npc_register`. `isMaster=true` = este cliente controla a IA deste NPC. |
| `npc_move`          | `{ id: string, p: number[], r: number }`                                | Broadcast da posição autoritativa do NPC (emitido pelo master).    |
| `pong`              | `{ timestamp: number }`                                                 | Resposta ao `ping` para cálculo de latência.                       |
| `ERROR`             | `{ message: string }`                                                   | Erro genérico (ex.: pacote recebido sem sessão ativa).             |
| `task_assigned`     | `{ playerId: string, tasks: AssignedTask[] }`                           | Broadcast para a sala com a(s) task(s) atribuída(s) ao jogador. `tasks[]` tem shape: `{ id, description, type, targetCount, currentProgress: 0, status: "pending" }`.<br><br>**Fila sequencial:** o jogador recebe UMA tarefa por vez. Este evento é emitido (a) na resposta ao `task_assign_request` e (b) **de novo, a cada tarefa concluída**, com a próxima da fila — sempre com `tasks` de um único elemento. O cliente deve **mesclar** pelo `id`, nunca substituir a lista: substituir apaga o histórico das já concluídas. A fila seca após N=3 tarefas e nenhum evento novo é emitido. |
| `task_updated`      | `{ playerId: string, taskId: string, currentProgress: number, status: string }` | Broadcast para todos na sala quando o status **ou o progresso** de uma task muda (via `task_start_interaction`, `task_complete_attempt` ou cada `task_progress`). Status possíveis: `in_progress`, `completed`, `failed`. Construído pelo servidor — não reflete campos crus do cliente. **Nunca** carrega `itemId`/`slotId`. |
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

### TaskRejectionCode (campo `code` em `task_rejected`)

```ts
type TaskRejectionCode =
    | 'NOT_ASSIGNED'      // jogador não tem a task (ou o taskId não é dele)
    | 'INVALID_STATUS'    // task já completed/failed
    | 'MISSING_ITEM'      // catálogo exige itemId e o payload não mandou
    | 'UNKNOWN_ITEM'      // itemId não pertence a esta task
    | 'WRONG_SLOT'        // par (itemId, slotId) não confere com o catálogo
    | 'ALREADY_COUNTED';  // itemId já contabilizado nesta atribuição
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
    id: string;              // identificador da task (ex: "task-collect-docs")
    description: string;     // descrição legível
    type: string;            // categoria ("collect" | "deliver" | "repair" | "escort")
    targetCount: number;     // quantidade alvo para conclusão
    currentProgress: number; // sempre 0 na atribuição inicial
    status: "pending" | "in_progress" | "completed" | "failed"; // sempre "pending" na atribuição inicial; "failed" é terminal
}
```

### PlayerSession (servidor, in-memory)

```ts
interface PlayerSession {
    id: string;           // playerId (DB) ou guestId ("guest-<uuid8>")
    address: string;
    port: number;
    roomId?: string;      // undefined até join_room ou auto-join
    playerName?: string;
    lastPosition?: number[];
    lastRotation?: number;
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

Cada NPC tem exatamente um "master" por sala — o cliente que o registrou primeiro via `npc_register`.

- O master executa a IA localmente e envia `npc_move_request` para o servidor.
- O servidor valida e broadcast `npc_move` para todos os outros da sala.
- Outros clientes interpolam a posição recebida (sem rodar IA).

Mapa interno: `roomId:npcId → playerId`. Quando o master desconecta, o NPC fica sem mestre até que outro cliente envie `npc_register` para ele.

No **Guest Mode**, o lazy reset (sala vazia → nova CONN guest) limpa o mapa de mastership, permitindo que o próximo entrante assuma todos os NPCs.

---

## 7. Guest Mode

### Fluxo completo

```
Cliente Unity                      Servidor REST         Servidor UDP
     |                                  |                      |
     |── POST /api/auth/guest ─────────►|                      |
     |◄── 201 { token, guestId,         |                      |
     |          roomId: "guest-room" } ──|                      |
     |                                  |                      |
     |── CONN { token: "<guest-jwt>" } ────────────────────────►|
     |                                  |         verifica JWT → decoded.id = "guest-<uuid8>"
     |                                  |         se guest-room vazia: clearRoomState (lazy reset)
     |                                  |         cria session com roomId = "guest-room"
     |◄── CONN_SUCCESS { id: "guest-..." } ────────────────────|
     |◄── room_joined { roomId: "guest-room", ... } ───────────|
     |                                  |                      |
     | (eventos normais: player_move, npc_register, etc.)
```

### Endpoint REST

```
POST /api/auth/guest
Content-Type: application/json
(sem body necessário)

201 Created
{
  "success": true,
  "status": 201,
  "message": "Acesso guest criado com sucesso",
  "data": {
    "token": "<jwt>",
    "guestId": "guest-<8-hex-chars>",
    "roomId": "guest-room"
  }
}
```

### Regras da sala guest

1. **Sala única**: todos os guests compartilham `"guest-room"` (in-memory, sem persistência).
2. **Auto-join**: o servidor detecta `decoded.id.startsWith("guest-")` e atribui `roomId = "guest-room"` automaticamente — o cliente **não precisa** enviar `join_room` separado.
3. **Lazy reset**: ao processar CONN de um guest, se `getRoomSessionCount("guest-room") === 0`, o servidor chama `NpcRegisterHandler.clearRoomState("guest-room")` antes de criar a sessão. Isso elimina NPCs órfãos de sessões anteriores.
4. **Mastership automático**: o primeiro guest a enviar `npc_register` para um NPC se torna seu master. Sem eleição explícita.
5. **Cleanup pós-timeout**: quando a última sessão de `guest-room` expira (30 s de inatividade), `cleanupSessions` chama `NpcRegisterHandler.clearRoomState("guest-room")` automaticamente.

---

## 8. Referências de implementação

| Componente | Arquivo |
| :--------- | :------ |
| Gerenciador UDP | `src/sockets/UdpSocketManager.ts` |
| Factory de handlers | `src/sockets/factories/SocketHandler.Factory.ts` |
| Handler NPC register | `src/sockets/handlers/NpcRegister.Handler.ts` |
| Endpoint guest | `src/api/controllers/GuestController.ts` |
| Service guest | `src/services/guestService.ts` |
| Constantes C# (cliente) | `Assets/Scripts/Network/NetworkEvents.cs` |
| DTOs C# (cliente) | `Assets/Scripts/Network/Models/` |
