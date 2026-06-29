# 0001 — Task System Scaffold

## 1. Context

O jogo não possui sistema de tarefas; NPCs existem mas não há missão/objetivo
atribuível a um jogador. Esta feature cria o scaffold completo: catálogo de
tarefas registrado via UDP pelo cliente, atribuição de tarefa a um jogador
(validada pelo servidor) e broadcast de confirmação para a sala. O objetivo é
ter a infraestrutura backend pronta para a fase de polish de gameplay sem
depender de persistence — o estado é in-memory por enquanto.

---

## 2. Scope & Target

**Target:** `both`

**Fase backend (primeiro):**
- 3 novos eventos UDP
- `TaskService` in-memory
- Registro no `SocketHandlerFactory`
- Atualização de `COMMUNICATION.md`

**Fase client (segundo):**
- Envio de `task_catalog_register` na inicialização da sala
- Envio de `task_assign_request` via UI/trigger
- Recepção de `task_assigned` e log no `Debug`

**Cross-repo contracts (novos eventos em `COMMUNICATION.md`):**

| Evento | Direção | Campos |
|---|---|---|
| `task_catalog_register` | C→S | `d.tasks: Array<{ id, label, npcId }>` |
| `task_assign_request` | C→S | `d.taskId: string, d.playerId: string` |
| `task_assigned` | S→C (broadcast) | `d.taskId: string, d.playerId: string, d.label: string` |

---

## 3. Approach

O backend segue o pattern já estabelecido: novo handler por evento, registrado no
`SocketHandlerFactory` via bloco estático. `TaskService` é adicionado ao
`ServiceFactory` e mantém dois Maps in-memory: `catalog` (taskId → TaskEntry) e
`assignments` (taskId → playerId). A validação replica o padrão do
`PlayerSprintHandler`: whitelist de IDs do catálogo antes de qualquer mutação.

`task_catalog_register` popula o catálogo somente se a sessão pertence à sala
(`getSession` → `roomId`). `task_assign_request` valida que o `taskId` existe no
catálogo, que não está já atribuído, e que o `playerId` pertence à mesma sala;
então persiste e executa `broadcastToRoom`. Nenhum dado é ecoado cru — servidor
constrói o payload de saída a partir do catálogo interno.

O cliente usa o padrão Observer já em vigor no `SocketManager`: subscreve
`task_assigned` em `OnEnable` / desinscreve em `OnDisable`, ação de teste por
botão UI ou `Start()`.

---

## 4. Files to Change

**Backend**

```
hora-extra-backend/src/services/TaskService.ts                  NEW
hora-extra-backend/src/services/TaskService.test.ts             NEW
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.ts   NEW
hora-extra-backend/src/sockets/handlers/TaskAssignRequestHandler.ts     NEW
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.test.ts  NEW
hora-extra-backend/src/sockets/handlers/TaskAssignRequestHandler.test.ts    NEW
hora-extra-backend/src/core/factories/Service.Factory.ts        MODIFY (add TaskService)
hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts  MODIFY (register 2 handlers)
hora-extra-backend/docs/Networking/COMMUNICATION.md             MODIFY (add 3 events)
```

**Client**

```
hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs       MODIFY (add 3 constants)
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs   NEW (DTOs: TaskEntry, TaskAssignedPayload)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs NEW (MonoBehaviour: register catalog, send assign, receive assigned)
```

---

## 5. TDD Breakdown (fase backend)

Todos os ciclos rodam com `npx vitest run` dentro de `hora-extra-backend/`.

**TaskService (`TaskService.test.ts`)**

- Cycle 1: `it('registerCatalog — armazena tarefas indexadas por id')` → Map preenchido corretamente
- Cycle 2: `it('registerCatalog — sobrescreve catálogo existente da sala')` → chamada dupla substitui entries
- Cycle 3: `it('assignTask — retorna a entry ao atribuir tarefa válida')` → happy path
- Cycle 4: `it('assignTask — lança erro se taskId não existe no catálogo')` → taskId inválido
- Cycle 5: `it('assignTask — lança erro se tarefa já está atribuída')` → idempotência de atribuição
- Cycle 6: `it('getAssignment — retorna playerId atribuído ou undefined')` → leitura de estado

**TaskCatalogRegisterHandler (`TaskCatalogRegisterHandler.test.ts`)**

- Cycle 7: `it('rejeita payload sem campo tasks')` → validação de shape
- Cycle 8: `it('rejeita sessão sem roomId (jogador não está em sala)')` → guard de sessão
- Cycle 9: `it('chama registerCatalog com tasks e roomId corretos')` → integração com TaskService

**TaskAssignRequestHandler (`TaskAssignRequestHandler.test.ts`)**

- Cycle 10: `it('rejeita payload sem taskId ou sem playerId')` → validação de campos
- Cycle 11: `it('rejeita se taskId não está no catálogo')` → delega ao TaskService, captura erro
- Cycle 12: `it('rejeita se tarefa já atribuída')` → segundo assign falha
- Cycle 13: `it('faz broadcastToRoom com payload task_assigned ao atribuir com sucesso')` → integração UdpSocketManager

---

## 6. Manual Verification Steps (fase client)

Pré-condição: backend rodando (`npm run dev`), Unity Play Mode ativo, `USE_SQLITE=true`.

1. **Registro de catálogo automático**
   - Ação: entrar em Play Mode com `TaskSystemBridge` em cena; verificar log do backend.
   - Resultado esperado: log `[UDP_SOCKET] task_catalog_register received` + `TaskService.registerCatalog called`.

2. **Atribuição de tarefa**
   - Ação: acionar `TaskSystemBridge.RequestAssign("task-01", <playerId>)` via botão ou método público.
   - Resultado esperado: `Debug.Log` no Unity mostrando `task_assigned` recebido com `taskId=task-01` e `label` correto.

3. **Tarefa já atribuída**
   - Ação: acionar `RequestAssign` duas vezes com o mesmo `taskId`.
   - Resultado esperado: segunda chamada gera `Debug.LogWarning` no Unity (resposta de erro do servidor) e catálogo não muda.

4. **Desconexão limpa**
   - Ação: sair do Play Mode e entrar novamente.
   - Resultado esperado: catálogo é re-registrado sem erros; nenhum estado residual do ciclo anterior causa exceção.

---

## 7. Verification Commands

**Backend (CI / test-runner):**

```bash
cd hora-extra-backend
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/TaskCatalogRegisterHandler.test.ts
npx vitest run src/sockets/handlers/TaskAssignRequestHandler.test.ts
# cobertura geral
npm run test:coverage
```

**Client:** seguir §6 Manual Verification Steps acima.

---

## 8. Out of Scope

- Persistência de tarefas no banco (Prisma/MySQL) — in-memory apenas.
- UI de listagem de tarefas ou HUD de progresso.
- Lógica de conclusão/entrega de tarefa (`task_complete` event).
- Sistema de recompensas ou XP.
- Múltiplas atribuições da mesma tarefa para jogadores diferentes.
- Integração com o sistema de NPCs além do campo `npcId` no catálogo.
