# Plan 0002 — task-system-correcao

## 1. Context

O scaffold do sistema de tasks (plano 0001) divergiu dos requisitos: tinha `npcId`
nos tipos, atribuição explícita por `taskId` (sem sorteio), e apenas 1 task por
jogador. Esta correção alinha a implementação aos requisitos reais: catálogo
definido pelo cliente (sem `npcId`), auto-assign aleatório via Fisher-Yates,
múltiplas tasks por jogador (N=3), shape de `AssignedTask` com `currentProgress` e
`status`, e idempotência silenciosa (2ª solicitação não substitui). Todo estado
permanece in-memory; sem Prisma. Sem tasks concretas de gameplay.

## 2. Scope & target

**Target:** `both`

**Phase backend** — correção de `TaskService`, `TaskCatalogRegisterHandler`,
`TaskAssignRequestHandler`, integração com `UdpSocketManager.resetRoom`.
**Phase client** — ajuste dos models C# e do `TaskSystemBridge` para consumir o
contrato corrigido.

### Contratos cross-repo (3 eventos em `COMMUNICATION.md`)

| Evento | Direção | Payload |
|--------|---------|---------|
| `task_catalog_register` | C→S | `{ tasks: [{ id, description, type, targetCount }] }` — sem `npcId` |
| `task_assign_request` | C→S | `{}` (payload vazio) |
| `task_assigned` | S→C broadcast | `{ playerId: string, tasks: AssignedTask[] }` |

Shape de `AssignedTask` (inicializado na atribuição):
```
{
  id: string,
  description: string,
  type: string,
  targetCount: number,
  currentProgress: number,   // sempre 0 na atribuição
  status: "pending" | "in_progress" | "completed"  // sempre "pending" na atribuição
}
```

## 3. Approach

### Backend

`TaskService` (singleton via `ServiceFactory`, in-memory) possui:
- `catalog: Map<roomId, Task[]>` — `registerCatalog(roomId, tasks)` substitui o
  catálogo da sala; tasks sem `npcId`.
- `assignments: Map<playerId, AssignedTask[]>` — `assignRandomTasks(roomId, playerId)`
  realiza Fisher-Yates shuffle, pega os primeiros `TASK_ASSIGN_COUNT=3` (ou todos se
  catálogo < N), mapeia para `AssignedTask` com `currentProgress=0` e
  `status="pending"`. **Idempotência silenciosa**: se o jogador já tem tasks, loga
  warn e retorna `[]` sem mutar. Lança `ApiError` se catálogo da sala estiver vazio.
- `getAssignedTasks(roomId, playerId): AssignedTask[]`.
- `clearRoom(playerIds: string[])` — remove entradas do map `assignments` para os
  playerIds fornecidos.

`TaskCatalogRegisterHandler` — modificado para remover `npcId` do payload e validar
`{ tasks: [...] }` sem esse campo.

`TaskAssignRequestHandler` — reescrito: payload `{}`, `playerId` vem da sessão,
chama `assignRandomTasks(roomId, playerId)`. Se retornar `[]` (idempotência), não
faz broadcast. Se `ApiError`, responde com `ERROR` ao remetente. Caso contrário,
`broadcastToRoom` com `{ playerId, tasks }`.

`UdpSocketManager.resetRoomState` — coleta `playerIds` da sala e chama
`taskService.clearRoom(playerIds)` antes de limpar as sessões.

Segue o pattern de `PlayerSprintHandler` (handler enxuto + serviço testável) e usa
`broadcastToRoom` / `ServiceFactory` já existentes.

### Client

`TaskModels.cs` — remover `NpcId`; `AssignedTask` ganha `currentProgress` e
`status`; `TaskAssignedPayload` vira `{ playerId, tasks: AssignedTask[] }`;
`TaskAssignRequestPayload` removido (payload é vazio, `SocketManager` envia `{}`).

`TaskSystemBridge.cs` — `RequestMyTasks()` sem argumentos envia payload `{}`;
`OnTaskAssignedReceived` itera o array de tasks recebido.

`NetworkEvents.cs` — nenhuma constante muda (os 3 nomes de evento permanecem).

## 4. Files to change

### Backend (todos MODIFY — existem desde o plano 0001)

```
hora-extra-backend/src/services/TaskService.ts
hora-extra-backend/src/services/TaskService.test.ts
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.Handler.ts
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.Handler.test.ts
hora-extra-backend/src/sockets/handlers/TaskAssignRequestHandler.Handler.ts
hora-extra-backend/src/sockets/handlers/TaskAssignRequestHandler.Handler.test.ts
hora-extra-backend/src/sockets/UdpSocketManager.ts
hora-extra-backend/src/sockets/UdpSocketManager.test.ts
hora-extra-backend/src/sockets/types/SocketEvent.ts
hora-extra-backend/docs/Networking/COMMUNICATION.md
```

### Client (todos MODIFY — existem desde o plano 0001)

```
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs
```

`hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs` — sem alteração.

## 5. TDD breakdown (phase: backend)

### TaskService (`TaskService.test.ts`)

- Cycle 1: `it('registerCatalog armazena tasks sem npcId')` → `registerCatalog` guarda
  `{ id, description, type, targetCount }` indexado por `roomId`.
- Cycle 2: `it('registerCatalog sobrescreve catálogo existente da sala')` → segunda
  chamada substitui a anterior para o mesmo `roomId`.
- Cycle 3: `it('assignRandomTasks retorna AssignedTask[] com currentProgress=0 e status=pending')` →
  shape inicial correto em cada item.
- Cycle 4: `it('assignRandomTasks retorna todas as tasks se catálogo < TASK_ASSIGN_COUNT')` →
  array menor que 3 quando pool tem menos itens.
- Cycle 5: `it('assignRandomTasks é idempotente — retorna [] se playerId já tem tasks')` →
  segunda chamada retorna `[]` sem mutar o map.
- Cycle 6: `it('assignRandomTasks lança ApiError se catálogo da sala estiver vazio')` →
  pool vazia ou sala sem catálogo dispara erro.
- Cycle 7: `it('getAssignedTasks retorna AssignedTask[] do jogador ou []')` →
  leitura do map de assignments.
- Cycle 8: `it('clearRoom remove assignments dos playerIds fornecidos')` →
  após `clearRoom([p1])`, `getAssignedTasks(roomId, p1)` retorna `[]`.

### TaskCatalogRegisterHandler (`TaskCatalogRegisterHandler.Handler.test.ts`)

- Cycle 9: `it('rejeita payload sem campo tasks')` → responde `ERROR` ao remetente.
- Cycle 10: `it('rejeita sessão sem roomId')` → responde `ERROR` ao remetente.
- Cycle 11: `it('chama registerCatalog com tasks sem npcId e com roomId da sessão')` →
  spy em `TaskService.registerCatalog`; confirma ausência de `npcId` nos args.

### TaskAssignRequestHandler (`TaskAssignRequestHandler.Handler.test.ts`)

- Cycle 12: `it('rejeita sessão sem roomId — payload {}')` → responde `ERROR`.
- Cycle 13: `it('não faz broadcast se assignRandomTasks retorna [] — idempotência')` →
  spy em `broadcastToRoom` nunca chamado.
- Cycle 14: `it('captura ApiError de catálogo vazio e responde ERROR ao remetente')` →
  `sendTo` chamado com evento `ERROR`.
- Cycle 15: `it('faz broadcastToRoom com {playerId, tasks} ao atribuir com sucesso')` →
  spy em `broadcastToRoom`; payload contém `playerId` e array de `AssignedTask`.

### UdpSocketManager (`UdpSocketManager.test.ts`)

Integrado aos ciclos existentes do arquivo; o novo comportamento é coberto por:

- `it('resetRoomState chama taskService.clearRoom com playerIds da sala')` →
  spy em `TaskService.clearRoom`; confirma que recebe os playerIds corretos.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev` em `hora-extra-backend/`), Unity em
Play Mode, cena `SCN_Main.unity`, `SocketManager.UseTestToken = true`.

1. **Conexão** — Console Unity deve exibir log com prefixo `[NETWORK]` indicando
   `CONN_SUCCESS`. Se não aparecer, checar porta 5001.

2. **Registro de catálogo** — ao entrar na sala, `TaskSystemBridge` envia
   `task_catalog_register` com payload `{ tasks: [...] }` (sem `npcId`). Esperado:
   log `[NETWORK] task_catalog_register enviado` e nenhum `ERROR` retornado.

3. **Solicitar tasks** — chamar `TaskSystemBridge.RequestMyTasks()` (sem args).
   Esperado: log `[NETWORK] task_assign_request enviado`.

4. **Receber tasks** — aguardar ~100 ms. Esperado: log `[GAMEPLAY] tasks recebidas`
   com array de `AssignedTask`; cada item com `currentProgress=0` e
   `status=pending`.

5. **Idempotência** — chamar `RequestMyTasks()` uma segunda vez. Esperado: nenhum
   novo broadcast recebido (servidor retorna `[]` silenciosamente); log de warning
   no backend confirmado via terminal.

6. **Reset e re-atribuição** — parar Play Mode e religar (ou enviar CONN com
   `resetRoom: true`). Registrar catálogo novamente e solicitar tasks. Esperado:
   tasks atribuídas normalmente, sem erros.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/TaskCatalogRegisterHandler.Handler.test.ts
npx vitest run src/sockets/handlers/TaskAssignRequestHandler.Handler.test.ts
npx vitest run src/sockets/UdpSocketManager.test.ts
# Ou suite completa:
npm test
```

### Client

Seguir os 6 passos da §6 Manual verification steps em Play Mode.

## 8. Out of scope

- Tasks concretas de gameplay (conteúdo do catálogo é responsabilidade do cliente).
- Persistência de tasks em banco de dados (Prisma) — tudo in-memory nesta fase.
- Atualização de progresso (`task_progress_update`) — plano futuro.
- Conclusão e recompensa (`task_completed`) — plano futuro.
- Integração com NPCs (`npcId` removido).
- UI/HUD de tasks — nenhum novo componente de interface nesta fase.
- Atribuição desigual entre jogadores.
