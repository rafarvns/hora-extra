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
| `task_catalog_register` | `{ tasks: Array<{ id: string, description: string, type: string, targetCount: number }> }` | Registra o catálogo de tarefas disponíveis para a sala. Sobrescreve a pool anterior. `npcId` removido — catálogo é da sala, não vinculado a NPC. |
| `task_assign_request`   | `{}` (sem campos)                                                   | Solicita ao servidor que sorteie e atribua N=3 tasks aleatórias ao jogador. `playerId` é obtido da sessão — não enviado no payload. |
| `task_start_interaction` | `{ taskId: string }`                                               | Jogador inicia interação com objeto de task. Servidor valida posse e transição `pending → in_progress`. |
| `task_complete_attempt`  | `{ taskId: string, success: boolean }`                             | Jogador reporta resultado do minigame QTE. Servidor determina status final autoritativamente. |
| `task_progress`          | `{ taskId: string }`                                               | Jogador coletou +1 item de uma task incremental (ex: `collect`). Servidor soma +1, faz `pending → in_progress` no primeiro incremento e `completed` ao atingir `targetCount`. Nunca confia em contagem do cliente. |

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
| `task_assigned`     | `{ playerId: string, tasks: AssignedTask[] }`                           | Broadcast para todos na sala com as tasks sorteadas para o jogador. `tasks[]` tem shape: `{ id, description, type, targetCount, currentProgress: 0, status: "pending" }`. |
| `task_updated`      | `{ playerId: string, taskId: string, currentProgress: number, status: string }` | Broadcast para todos na sala quando o status **ou o progresso** de uma task muda (via `task_start_interaction`, `task_complete_attempt` ou cada `task_progress`). Status possíveis: `in_progress`, `completed`, `failed`. Construído pelo servidor — não reflete campos crus do cliente. |

---

## 5. Schemas de dados

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
