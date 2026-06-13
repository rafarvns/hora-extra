# Plan 0003 — task-qte-cafe

## 1. Context

O scaffold de tasks (planos 0001/0002) está implementado: catálogo, atribuição aleatória, broadcast `task_assigned`. A próxima etapa concretiza a **primeira task jogável**: a tarefa da cafeteira, implementada como minigame QTE (Quick Time Event) com 3 rounds de press-timing no cliente. O servidor permanece source of truth do status: valida posse da task, aceita transições de estado `pending → in_progress → completed | failed` e broadcasta `task_updated` para a sala. O cliente roda o timing local e envia apenas o resultado.

## 2. Scope & target

**Target:** `both`

**Phase backend** — 3 novos eventos UDP (`task_start_interaction`, `task_complete_attempt`, `task_updated`); métodos `startTask` e `resolveTask` no `TaskService`; adição de `'failed'` ao union type `AssignedTask.status`; handlers registrados na factory; atualização de `COMMUNICATION.md`.

**Phase client** — `CoffeeMakerInteraction.cs` (trigger de proximidade + prompt UI + gate por task); `CoffeeQTE.cs` (minigame de timing, N rounds = `targetCount`); `TaskSystemBridge.cs` ampliado para receber `task_updated`; `TaskModels.cs` com `'failed'` no status e novos DTOs; `NetworkEvents.cs` com 3 novas constantes; prefab `PFB_Interactable_CoffeeMaker`; catálogo inicial populado no Inspector com a task coffee_maker.

### Contratos cross-repo

COMMUNICATION.md receberá as seguintes entradas novas (§3 C→S e §4 S→C):

| Evento | Direção | Payload |
|--------|---------|---------|
| `task_start_interaction` | C→S | `{ taskId: string }` |
| `task_complete_attempt` | C→S | `{ taskId: string, success: boolean }` |
| `task_updated` | S→C broadcast | `{ playerId: string, taskId: string, currentProgress: number, status: string }` |

`AssignedTask.status` no §5 Schema de COMMUNICATION.md será atualizado de `"pending" | "in_progress" | "completed"` para `"pending" | "in_progress" | "completed" | "failed"`.

## 3. Approach

### Backend

`TaskService` ganha dois métodos:

- `startTask(playerId, taskId): AssignedTask` — localiza a task no map `assignments` do jogador, valida que `status === 'pending'`, muda para `'in_progress'`, retorna o objeto atualizado. Lança `ApiError` se: jogador sem tasks, taskId não encontrado, status diferente de `pending` (transição inválida).
- `resolveTask(playerId, taskId, success: boolean): AssignedTask` — valida `status === 'in_progress'`; se `success`: seta `status = 'completed'` e `currentProgress = task.targetCount`; se `!success`: seta `status = 'failed'`. Retorna o objeto. Lança `ApiError` em transição inválida.

`AssignedTask.status` passa de `'pending' | 'in_progress' | 'completed'` para `'pending' | 'in_progress' | 'completed' | 'failed'`.

Três novos handlers seguindo o pattern de `PlayerSprintHandler` (handler enxuto + serviço testável):

- `TaskStartInteractionHandler` — valida `{ taskId: string }`, sessão com `roomId`, chama `taskService.startTask`; em sucesso faz `broadcastToRoom` com `task_updated`; em erro `sendTo ERROR`.
- `TaskCompleteAttemptHandler` — valida `{ taskId: string, success: boolean }`, chama `taskService.resolveTask`; em sucesso `broadcastToRoom task_updated`; em erro `sendTo ERROR`.
- Handlers registrados no static block do `SocketHandlerFactory` com chaves `'task_start_interaction'` e `'task_complete_attempt'`.

O payload broadcast de `task_updated` é construído pelo servidor a partir do objeto retornado pelo service — nunca reflete campo `success` do cliente.

### Client

`TaskModels.cs` — `AssignedTask.Status` ganha o valor `"failed"` (apenas documentação no comentário XML; o campo é `string` então não quebra binário). Adicionar `TaskStartInteractionPayload`, `TaskCompleteAttemptPayload` e `TaskUpdatedPayload`.

`NetworkEvents.cs` — adicionar `TASK_START_INTERACTION`, `TASK_COMPLETE_ATTEMPT`, `TASK_UPDATED`.

`TaskSystemBridge.cs` — inscrever `TASK_UPDATED` em `OnEnable`/`OnDisable`; callback `OnTaskUpdatedReceived` atualiza a task local na lista do jogador e dispara novo evento público `OnTaskUpdated(playerId, task)`. Também expõe `SendStartInteraction(taskId)` e `SendCompleteAttempt(taskId, success)` que usam as constantes.

`CoffeeMakerInteraction.cs` (NEW) — `MonoBehaviour` com trigger collider; em `OnTriggerEnter/Exit` controla flag `_playerNearby`; em `Update` detecta input (ex: tecla `E`) quando `_playerNearby`; gate: verifica `TaskSystemBridge.Instance` tem `AssignedTask` com `type == "coffee_maker"` e `status == "pending"` ou `"in_progress"` antes de mostrar prompt; ao pressionar `E`, envia `task_start_interaction` via `TaskSystemBridge.SendStartInteraction` e inicia `CoffeeQTE`. Subscreve `OnTaskUpdated` para reagir a `in_progress` confirmado.

`CoffeeQTE.cs` (NEW) — componente de minigame; recebe `targetCount` (= número de rounds); cada round exibe indicador visual (barra ou seta) e aguarda input num janela de tempo; acerto avança round; erro imediato → chama `TaskSystemBridge.SendCompleteAttempt(taskId, false)`; ao completar todos os rounds → chama `TaskSystemBridge.SendCompleteAttempt(taskId, true)`. UI simples via `Canvas` filho no prefab ou via `GameObject.SetActive`. Segue o padrão de marshal para main thread já presente no `SocketManager`.

`PFB_Interactable_CoffeeMaker` (NEW asset) — criado no Editor; contém trigger `Collider`, `CoffeeMakerInteraction`, `CoffeeQTE`, canvas filho para prompt "Pressione E" e indicador QTE.

Catálogo inicial no Inspector do `TaskSystemBridge`: adicionar entrada `{ id: "task-coffee-maker-01", description: "Prepare o café", type: "coffee_maker", targetCount: 3 }`.

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/TaskService.ts                                        MODIFY (add startTask, resolveTask; add 'failed' to status)
hora-extra-backend/src/services/TaskService.test.ts                                   MODIFY (add cycles 1-8)
hora-extra-backend/src/sockets/handlers/TaskStartInteraction.Handler.ts               NEW
hora-extra-backend/src/sockets/handlers/TaskStartInteraction.Handler.test.ts          NEW
hora-extra-backend/src/sockets/handlers/TaskCompleteAttempt.Handler.ts                NEW
hora-extra-backend/src/sockets/handlers/TaskCompleteAttempt.Handler.test.ts           NEW
hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts                     MODIFY (register 2 new handlers)
hora-extra-backend/docs/Networking/COMMUNICATION.md                                   MODIFY (add 3 events + update AssignedTask schema)
```

### Client

```
hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs                             MODIFY (add 3 constants)
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs                         MODIFY (add 'failed' comment; add 3 new payload DTOs)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs                       MODIFY (add TASK_UPDATED subscription; add SendStartInteraction, SendCompleteAttempt; add OnTaskUpdated event)
hora-extra-client/Assets/Scripts/Characters/CoffeeMakerInteraction.cs                 NEW
hora-extra-client/Assets/Scripts/Characters/CoffeeQTE.cs                              NEW
hora-extra-client/Assets/Prefabs/PFB_Interactable_CoffeeMaker.prefab                  NEW (asset, Editor)
```

## 5. TDD breakdown (phase: backend)

### TaskService — novos métodos (`TaskService.test.ts`)

- Cycle 1: `it('startTask muda status pending → in_progress e retorna AssignedTask atualizada')` → happy path, task com `status='pending'` passa pra `'in_progress'`.
- Cycle 2: `it('startTask lança ApiError se jogador não tem tasks atribuídas')` → jogador sem entrada no map `assignments`.
- Cycle 3: `it('startTask lança ApiError se taskId não encontrado no array do jogador')` → id inexistente no array.
- Cycle 4: `it('startTask lança ApiError se task não está pending — transição inválida')` → task já `'in_progress'` ou `'completed'`.
- Cycle 5: `it('resolveTask com success=true muda in_progress → completed e seta currentProgress=targetCount')` → happy path completo.
- Cycle 6: `it('resolveTask com success=false muda in_progress → failed')` → status final `'failed'`.
- Cycle 7: `it('resolveTask lança ApiError se task não está in_progress')` → transição de `'pending'` ou `'completed'` ou `'failed'` direto para resolve deve falhar.
- Cycle 8: `it('resolveTask lança ApiError se taskId não encontrado')` → id inválido.

### TaskStartInteractionHandler (`TaskStartInteraction.Handler.test.ts`)

- Cycle 9: `it('rejeita payload sem taskId — responde ERROR')` → campo ausente.
- Cycle 10: `it('rejeita sessão sem roomId — responde ERROR')` → guard de sessão.
- Cycle 11: `it('chama taskService.startTask com playerId da sessão e taskId do payload')` → spy em `startTask`.
- Cycle 12: `it('faz broadcastToRoom task_updated com { playerId, taskId, currentProgress, status } em sucesso')` → payload correto.
- Cycle 13: `it('responde ERROR ao remetente quando startTask lança ApiError')` → `sendTo ERROR` chamado, `broadcastToRoom` não.

### TaskCompleteAttemptHandler (`TaskCompleteAttempt.Handler.test.ts`)

- Cycle 14: `it('rejeita payload sem taskId ou sem campo success — responde ERROR')` → validação de shape.
- Cycle 15: `it('rejeita sessão sem roomId — responde ERROR')`.
- Cycle 16: `it('chama taskService.resolveTask com playerId, taskId e success do payload')` → spy.
- Cycle 17: `it('faz broadcastToRoom task_updated com status=completed quando success=true')` → shape do broadcast.
- Cycle 18: `it('faz broadcastToRoom task_updated com status=failed quando success=false')` → status correto.
- Cycle 19: `it('responde ERROR quando resolveTask lança ApiError — sem broadcast')` → transição inválida capturada.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev` em `hora-extra-backend/`), cena `SCN_Main.unity` aberta, `SocketManager.UseTestToken = true`, Console com "Clear on Play" ativado.

### 1. Catálogo com task coffee_maker

- Ação: entrar em Play Mode.
- Esperado: `[NETWORK] task_catalog_register enviado — N tarefa(s)` onde N inclui a entrada `type=coffee_maker`. Nenhum `ERROR` no Console.

### 2. Solicitar tasks e confirmar atribuição

- Ação: acionar `TaskSystemBridge.Instance.RequestMyTasks()` (via botão existente na cena ou via código no `Start`).
- Esperado: `[GAMEPLAY] tasks recebidas para playerId=...` com ao menos uma task com `type=coffee_maker` e `status=pending`.

### 3. Gate — prompt só aparece com task atribuída

- Pré-condição: o prefab `PFB_Interactable_CoffeeMaker` está na cena. Jogador NÃO tem tasks atribuídas (não executou passo 2).
- Ação: mover o personagem para a área de trigger da cafeteira.
- Esperado: prompt "Pressione E" NÃO aparece (gate bloqueado). Nenhum log de erro.

### 4. Proximidade e prompt

- Pré-condição: tasks atribuídas (passo 2 executado), `status=pending` para a coffee_maker task.
- Ação: mover o personagem para o trigger da cafeteira.
- Esperado: `[UI] CoffeeMakerInteraction — jogador próximo, exibindo prompt`. Prompt "Pressione E" visível na tela.

### 5. Iniciar interação — `task_start_interaction` enviado

- Ação: pressionar `E` enquanto prompt está visível.
- Esperado:
  - `[NETWORK] task_start_interaction enviado — taskId=task-coffee-maker-01`.
  - Backend loga `[UDP_SOCKET] task_start_interaction ...` e transmite `task_updated`.
  - Unity Console: `[GAMEPLAY] task_updated recebido — taskId=... status=in_progress`.
  - QTE inicia (indicador visual aparece na tela).

### 6. QTE — acertar todos os rounds (sucesso)

- Ação: pressionar o botão correto nas 3 janelas de timing (3 rounds = `targetCount`).
- Esperado:
  - `[GAMEPLAY] CoffeeQTE — round 1/3 acertado`, `round 2/3`, `round 3/3`.
  - Após round 3: `[NETWORK] task_complete_attempt enviado — taskId=... success=true`.
  - `[GAMEPLAY] task_updated recebido — taskId=... status=completed`.
  - UI do QTE some. Prompt some (task já não está `pending/in_progress`).

### 7. QTE — errar um round (falha)

- Pré-condição: reiniciar Play Mode; executar passos 1-5 novamente (nova atribuição).
- Ação: deixar a janela de timing expirar ou pressionar errado em qualquer round.
- Esperado:
  - `[GAMEPLAY] CoffeeQTE — round N falhou`.
  - `[NETWORK] task_complete_attempt enviado — taskId=... success=false`.
  - `[GAMEPLAY] task_updated recebido — taskId=... status=failed`.
  - QTE some. Prompt não reaparece ao entrar no trigger novamente (task `failed` é terminal — gate rejeita).

### 8. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum LogError vermelho residual; nenhum freeze.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/TaskStartInteraction.Handler.test.ts
npx vitest run src/sockets/handlers/TaskCompleteAttempt.Handler.test.ts
# Suite completa:
npm test
```

### Client

Seguir os 8 passos da §6 em Play Mode.

## 8. Out of scope

- Recompensas, XP ou feedback de conclusão além do status broadcast.
- Múltiplos tipos de QTE (esta feature define apenas o coffee_maker; a infra é genérica o suficiente pra outros types mas sem implementá-los).
- Task `failed` ser re-tentável — é terminal por decisão do usuário (P5).
- UI de lista de tasks ou HUD de progresso persistente.
- Persistência em banco de dados (Prisma) — tudo in-memory.
- Anti-cheat de timing QTE — sem validação server-side de janela de tempo (decisão P1).
- Múltiplas instâncias de cafeteira com IDs distintos — a feature usa `type` como filtro, não `taskId` hardcoded.
- Sons e partículas de feedback do QTE.
