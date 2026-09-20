# Plan 0010 — infra-agenda-notificacoes-e-penalidades

> **Plano-base do eixo "tempo".** Origem: `TAREFAS DOS JOGADORES.pdf` — as Tarefas **10**
> (reunião), **13** (vazamento da cafeteira) e **14** (bater ponto) são disparadas pelo
> *relógio*, não pelo jogador; e as Tarefas 10 e 14 punem quem perde o prazo com **tarefa extra**.
> A Tarefa **8** (reabastecer impressora) usa o mesmo disparo de evento de mundo.
>
> Independe dos planos 0004 e 0009 — pode ser implementado em paralelo. Consumido por:
> [0018](0018-tarefa-08-reabastecer-impressora.md), [0020](0020-tarefa-10-reuniao-com-o-chefe.md),
> [0023](0023-tarefa-13-limpeza-de-vazamento-da-cafeteira.md),
> [0024](0024-tarefa-14-bater-ponto.md). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

Tudo no sistema de tasks hoje é **reativo**: o servidor só age quando chega um pacote do cliente.
Não existe nenhum caminho pelo qual o servidor comece algo sozinho.

Quatro tarefas da lista-mestra exigem exatamente isso:

| Tarefa | O que o servidor precisa fazer sozinho |
| :----- | :------------------------------------- |
| 10 — Reunião | convocar *"em momentos aleatórios ou pré-definidos"*, contar o prazo, conferir **quem está na sala** e punir os ausentes |
| 14 — Bater ponto | abrir a janela de registro em horário específico, lembrar o jogador, punir quem perder |
| 13 — Vazamento | fazer a cafeteira vazar em algum momento e criar a sujeira no chão |
| 8 — Impressora | fazer a impressora acabar o papel/tinta *"durante o expediente"* |

E três capacidades faltam:

1. **Relógio autoritativo.** Se o cliente disser "deu a hora", qualquer um vira o relógio e
   escapa da penalidade. O tempo tem que nascer e morrer no servidor —
   `.agents/rules/backend-design-pattern.md`.
2. **Presença verificável.** *"Jogadores que não comparecerem recebem penalidades"* exige que o
   servidor saiba se o jogador estava na sala do chefe no instante do prazo. O servidor **já tem**
   a posição autoritativa de cada jogador (`PlayerSession`, alimentada por `player_move`) — falta
   só o conceito de **zona** para consultá-la.
3. **Penalidade.** `assignRandomTasks` é idempotente de propósito (plano 0001: a segunda
   solicitação do mesmo jogador retorna `[]`). Isso impede, hoje, atribuir a tarefa extra. Falta
   um caminho explícito e separado — não relaxar a idempotência, que protege contra re-sorteio.

Este plano entrega as três, genéricas, mais o canal de **notificação no celular corporativo** que
o documento cita nas Tarefas 10 e 14.

### Por que um service novo e não mais lógica no `TaskService`

`TaskService` é sem estado temporal por desenho — é um mapa de atribuições. Enfiar timers ali
misturaria duas responsabilidades e tornaria cada teste dependente de relógio. `ScheduleService`
nasce separado, registrado na `ServiceFactory` (`.agents/rules/backend-factory-pattern.md`), e
sua lógica de decisão é escrita como **função pura** que recebe `now` e as posições — o timer só
a chama. É o que permite testar prazo e presença sem `setTimeout` em teste.

## 2. Scope & target

**Target:** `both`

**Phase backend** — novo `ScheduleService` (+ registro na `ServiceFactory`); `TaskEntry` ganha
`deadlineSeconds?`, `zone?`, `penaltyTaskId?` e `schedule?`; `TaskService` ganha
`assignPenaltyTask` e `getEntry`; `UdpSocketManager` ganha `getRoomPlayerPositions(roomId)` e
passa a instanciar o tick de agenda; 4 eventos S→C novos; 2 códigos de rejeição novos;
`COMMUNICATION.md` atualizado.

**Phase client** — `Assets/Scripts/UI/PhoneNotificationHud.cs` e
`Assets/Scripts/UI/TaskDeadlineHud.cs`; `Assets/Scripts/Network/WorldEventRouter.cs`;
`Assets/Scripts/Interactions/TaskZoneGizmo.cs` (só autoria/Editor); `TaskSystemBridge` assina os
4 eventos novos e os reemite como `event Action<>`; `NetworkEvents` e `TaskModels` estendidos.

Nenhuma tarefa concreta é entregue aqui — nem reunião, nem ponto, nem vazamento. Só o relógio, a
zona, a penalidade e a notificação.

### Contratos cross-repo

| Evento | Direção | Payload |
| :----- | :------ | :------ |
| `task_catalog_register` | C→S | **ALTERADO** — entrada aceita `deadlineSeconds?: number`, `zone?: { center: [x,y,z], size: [x,y,z] }`, `penaltyTaskId?: string`, `schedule?: Array<{ atSeconds: number }>` |
| `world_event` | S→C broadcast | **NOVO** — `{ eventId: string, type: string, data?: object }` |
| `task_deadline` | S→C broadcast | **NOVO** — `{ taskId: string, remainingSeconds: number }` |
| `task_expired` | S→C broadcast | **NOVO** — `{ taskId: string, playerId: string, reason: string }` |
| `penalty_assigned` | S→C unicast | **NOVO** — `{ task: AssignedTask, reason: string }` |
| `task_rejected` | S→C unicast | **ALTERADO** — `code` aceita `DEADLINE_EXPIRED` e `OUT_OF_ZONE` |

Todos os campos de catálogo são opcionais; entradas sem eles nunca entram na agenda.

**Sobre o tempo na rede:** `task_deadline` carrega `remainingSeconds` (relativo), **não** um
timestamp absoluto. Relógios de cliente e servidor não estão sincronizados, e um `deadlineTs`
absoluto exigiria NTP ou handshake de offset. O servidor reemite `task_deadline` a cada segundo
enquanto o prazo corre; o cliente só exibe o número que recebeu, interpolando entre pacotes. Se um
pacote se perder (é UDP), o seguinte corrige — nenhum estado depende de entrega garantida.

## 3. Approach

### Backend

#### `ScheduleService.ts` (NEW)

```ts
export interface ScheduledDeadline {
    roomId: string;
    playerId: string;
    taskId: string;
    expiresAtMs: number;
    zone?: TaskZone;          // se presente, presença é conferida no vencimento
    penaltyTaskId?: string;
}

export interface TaskZone { center: [number, number, number]; size: [number, number, number]; }
```

Três responsabilidades, cada uma isolável em teste:

1. **`isInsideZone(pos, zone): boolean`** — AABB puro. Half-extents a partir de `size`;
   comparação inclusiva nas bordas. Função pura, sem relógio.
2. **`evaluateDeadlines(nowMs, positions): DeadlineOutcome[]`** — recebe `now` e um mapa
   `playerId → posição`; devolve, para cada prazo vencido, `{ taskId, playerId, roomId, present,
   penaltyTaskId }`. **Não** envia pacote e **não** lê o relógio — quem faz isso é o tick. É esta
   assinatura que torna todo o comportamento de prazo testável sem timer.
3. **`tick()`** — chamado por `setInterval(1000)` a partir do `index.ts`. Lê `Date.now()`, pega as
   posições no `UdpSocketManager`, chama `evaluateDeadlines`, e para cada resultado emite os
   pacotes. Frequência de 1 Hz e não 20 Hz: prazo é granularidade de segundo, e o tick de 20 Hz é
   do estado de movimento (`.agents/rules/backend-design-pattern.md`) — não misturar os dois.

API pública restante: `scheduleDeadline(...)`, `cancelDeadline(taskId, playerId)` (chamado quando
a task completa — prazo cumprido não vence), `scheduleWorldEvent(roomId, type, atSeconds, data?)`,
`clearRoom(roomId)`.

#### Resolução de um prazo vencido

Para cada `DeadlineOutcome`:

- **Presente** (ou sem `zone`, ou sem `penaltyTaskId`) → só `task_expired` com
  `reason: 'completed_in_zone'` ou `'expired'`.
- **Ausente** → `task_expired` com `reason: 'absent'` **e**
  `TaskService.assignPenaltyTask(playerId, roomId, penaltyTaskId)`, seguido de
  `penalty_assigned` unicast e `task_assigned` broadcast (para o HUD dos outros ficar coerente).

Se o jogador estiver **offline** no vencimento, não há posição: conta como ausente, mas a
penalidade é gravada e entregue via `task_assigned` quando ele reconectar — não perder a
penalidade por desconexão é o comportamento correto para uma regra de presença. Se a sessão nunca
voltar, `clearRoom` limpa junto com o resto.

#### `TaskService` (MODIFY)

- `assignPenaltyTask(playerId, roomId, penaltyTaskId): AssignedTask` — busca a entrada no catálogo
  da sala e **anexa** ao array do jogador, ignorando a idempotência de `assignRandomTasks` (que
  continua intacta para o sorteio). Lança `ApiError` se o `penaltyTaskId` não existir no catálogo.
  Se o jogador já tiver aquela task `pending`/`in_progress`, **não duplica** — devolve a existente
  (senão dois atrasos seguidos empilham a mesma tarefa).
- `getEntry(roomId, taskId): TaskEntry | undefined` — o `ScheduleService` precisa ler
  `deadlineSeconds`/`zone`/`penaltyTaskId` do catálogo sem duplicar o mapa.
- `TaskRejectionCode` ganha `DEADLINE_EXPIRED` e `OUT_OF_ZONE`, usados por planos de tarefa que
  validem "chegou tarde" / "não está na sala" no momento da interação.

#### `UdpSocketManager` (MODIFY)

- `getRoomPlayerPositions(roomId): Map<string, [number, number, number]>` — expõe o que já está
  no `PlayerSession`. Só leitura; nenhuma mudança no ciclo de sessão.
- Dispara o `setInterval` do `ScheduleService.tick()` na inicialização e o limpa no shutdown, do
  lado do reaper de sessões inativas que já existe.
- `resetRoomState` (bypass de dev) chama `ScheduleService.clearRoom` — senão um prazo da sessão
  anterior vence no meio do teste seguinte.

#### Quando um prazo entra na agenda

Ao atribuir tasks (`assignRandomTasks`) ou uma penalidade, o handler consulta cada entrada: se
tiver `deadlineSeconds`, chama `scheduleDeadline`. Se tiver `schedule`, os `world_event` da sala
são agendados no **registro do catálogo**, não por jogador — evento de mundo é da sala.

### Client

#### `Network/WorldEventRouter.cs` (NEW)

Assina `world_event` e reemite por tipo: `public static event Action<WorldEventPayload> OnWorldEvent`
e um `Subscribe(string type, Action<WorldEventPayload>)`. Evita que cada tarefa futura (cafeteira,
impressora, reunião, ponto) assine o mesmo evento cru e filtre por `type` na mão. Callback de
socket vem de worker thread → marshalar pela fila de main thread do `SocketManager`
(`.agents/rules/client-design-pattern.md`).

#### `UI/PhoneNotificationHud.cs` (NEW)

O "celular corporativo" do documento. Painel canto-de-tela com fila de notificações:
`Show(string title, string body, float seconds)`. Consome `world_event` e `penalty_assigned`.
Fila, não substituição: duas notificações juntas não podem se atropelar.

#### `UI/TaskDeadlineHud.cs` (NEW)

Contador regressivo da task com prazo ativo. Assina `task_deadline`; interpola localmente entre
pacotes (1/s) para o número não "pular"; esconde em `task_expired` ou ao completar. **Nunca**
decide expiração sozinho — mesmo chegando a zero na tela, quem declara é o servidor.

#### `Interactions/TaskZoneGizmo.cs` (NEW, autoria)

`MonoBehaviour` só de Editor (`OnDrawGizmos`) que desenha a AABB e expõe `center`/`size` em
coordenadas de mundo para copiar na entrada de catálogo. Autorar zona "no olho" é a principal
fonte de erro deste plano — uma zona mal posicionada faz todo mundo ser punido injustamente.
Inclui um botão de Inspector para copiar o par já no formato do catálogo.

#### `Characters/TaskSystemBridge.cs` (MODIFY)

Assina os 4 eventos novos em `OnEnable` / remove em `OnDisable`; reemite como
`OnWorldEvent`, `OnTaskDeadline`, `OnTaskExpired`, `OnPenaltyAssigned`. `TaskEntryData` ganha os
4 campos de catálogo, serializáveis no Inspector (`zone` como `[Serializable] class TaskZoneData`
com dois `Vector3` — `Vector3` serializa no Inspector, e a conversão para `[x,y,z]` acontece no
`RegisterCatalog`).

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/ScheduleService.ts                      NEW
hora-extra-backend/src/services/ScheduleService.test.ts                 NEW (ciclos 1-12)
hora-extra-backend/src/core/factories/Service.Factory.ts                MODIFY (registra ScheduleService)
hora-extra-backend/src/services/TaskService.ts                          MODIFY (assignPenaltyTask, getEntry, 2 códigos, campos de agenda em TaskEntry)
hora-extra-backend/src/services/TaskService.test.ts                     MODIFY (ciclos 13-17)
hora-extra-backend/src/sockets/UdpSocketManager.ts                      MODIFY (getRoomPlayerPositions, tick 1 Hz, clearRoom da agenda)
hora-extra-backend/src/sockets/handlers/TaskAssignRequestHandler.Handler.ts   MODIFY (agenda prazos das tasks sorteadas)
hora-extra-backend/src/sockets/handlers/TaskAssignRequestHandler.test.ts      MODIFY (ciclos 18-19)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.Handler.ts MODIFY (valida shape de zone/schedule; agenda world_events da sala)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.test.ts    MODIFY (ciclos 20-21)
hora-extra-backend/docs/Networking/COMMUNICATION.md                     MODIFY (4 eventos, 4 campos, 2 códigos, nota de relógio relativo)
hora-extra-backend/docs/Mechanics/SCHEDULE-AND-PENALTIES.md             NEW
```

Nenhum handler C→S novo → a `SocketHandlerFactory` **não** muda (os 4 eventos são S→C).

### Client

```
hora-extra-client/Assets/Scripts/Network/WorldEventRouter.cs            NEW
hora-extra-client/Assets/Scripts/UI/PhoneNotificationHud.cs             NEW
hora-extra-client/Assets/Scripts/UI/TaskDeadlineHud.cs                  NEW
hora-extra-client/Assets/Scripts/Interactions/TaskZoneGizmo.cs          NEW (autoria/Editor)
hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs               MODIFY (WORLD_EVENT, TASK_DEADLINE, TASK_EXPIRED, PENALTY_ASSIGNED)
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs           MODIFY (WorldEventPayload, TaskDeadlinePayload, TaskExpiredPayload, PenaltyAssignedPayload, TaskZoneData)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs         MODIFY (4 listeners + 4 eventos + campos de agenda em TaskEntryData)
hora-extra-client/Assets/Prefab/PFB_UI_PhoneNotification.prefab         NEW (asset, Editor)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                    MODIFY (asset, Editor — HUDs no HUD_Canvas + 1 zona de sanidade)
hora-extra-client/Docs/Mechanics/DEADLINES-AND-NOTIFICATIONS.md         NEW
```

## 5. TDD breakdown (phase: backend)

### ScheduleService (`ScheduleService.test.ts`)

Todos sem timer real: `evaluateDeadlines` recebe `now` como argumento.

- Cycle 1: `it('isInsideZone aceita ponto no centro da AABB')`.
- Cycle 2: `it('isInsideZone aceita ponto exatamente na borda')` → comparação inclusiva.
- Cycle 3: `it('isInsideZone rejeita ponto fora em um único eixo')` → o caso que pega bug de
  half-extent trocado; testar os 3 eixos.
- Cycle 4: `it('evaluateDeadlines não devolve nada antes do vencimento')`.
- Cycle 5: `it('evaluateDeadlines devolve present=true quando a posição está na zona')`.
- Cycle 6: `it('evaluateDeadlines devolve present=false quando a posição está fora da zona')`.
- Cycle 7: `it('evaluateDeadlines devolve present=false quando não há posição (jogador offline)')`.
- Cycle 8: `it('evaluateDeadlines devolve present=true quando o prazo não declara zone')` → prazo
  sem presença não pune por localização.
- Cycle 9: `it('evaluateDeadlines não devolve o mesmo prazo duas vezes')` → consumido no primeiro
  vencimento; senão o tick de 1 Hz pune o jogador a cada segundo.
- Cycle 10: `it('cancelDeadline remove o prazo — não vence depois')`.
- Cycle 11: `it('clearRoom remove prazos e eventos agendados da sala')`.
- Cycle 12: `it('scheduleWorldEvent devolve o evento no tick do atSeconds e não antes')`.

### TaskService (`TaskService.test.ts`)

- Cycle 13: `it('assignPenaltyTask anexa a task extra mesmo com o jogador já tendo tasks')` → é o
  caso que a idempotência de `assignRandomTasks` bloqueia.
- Cycle 14: `it('assignPenaltyTask não duplica uma penalidade ainda pendente')`.
- Cycle 15: `it('assignPenaltyTask atribui de novo se a penalidade anterior já foi completed')`.
- Cycle 16: `it('assignPenaltyTask lança ApiError se o penaltyTaskId não está no catálogo da sala')`.
- Cycle 17: `it('assignRandomTasks continua idempotente após a introdução de assignPenaltyTask')`
  → regressão explícita do plano 0001.

### TaskAssignRequestHandler (`TaskAssignRequestHandler.test.ts`)

- Cycle 18: `it('agenda prazo para cada task sorteada que declara deadlineSeconds')`.
- Cycle 19: `it('não agenda prazo para task sem deadlineSeconds')`.

### TaskCatalogRegisterHandler (`TaskCatalogRegisterHandler.test.ts`)

- Cycle 20: `it('descarta zone malformada (array de tamanho != 3) com warn')`.
- Cycle 21: `it('agenda os world_events declarados em schedule no registro do catálogo')`.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev`), `SCN_FirstFloor.unity` aberta,
`SocketManager.UseTestToken = true`, Console com "Clear on Play".

**Cena de sanidade:** entrada de catálogo `task-sanity-deadline` com `deadlineSeconds = 30`,
`zone` cobrindo um canto marcado da recepção (autorada com o `TaskZoneGizmo`) e
`penaltyTaskId = "task-sanity-penalty"`; mais uma entrada `task-sanity-penalty` simples. Usar
`deadlineSeconds` curto para não esperar. Descartar depois.

### 1. Prazo aparece e conta

- Ação: entrar em Play Mode e receber a task com prazo.
- Esperado: `TaskDeadlineHud` aparece contando de ~30; o número desce suave (interpolado), sem
  saltos de 1 em 1 visíveis; `[NETWORK] task_deadline` chegando ~1×/s.

### 2. Zona conferida — dentro

- Ação: ficar dentro da zona marcada até o prazo vencer.
- Esperado: `[GAMEPLAY] task_expired — reason=completed_in_zone`; **nenhuma**
  `penalty_assigned`; nenhuma tarefa nova no `MissionListHud`.

### 3. Zona conferida — fora

- Ação: reiniciar e ficar **fora** da zona até vencer.
- Esperado: `task_expired — reason=absent`; `[GAMEPLAY] penalty_assigned`; notificação no
  `PhoneNotificationHud`; a task extra aparece no `MissionListHud`.

### 4. Borda da zona

- Ação: ficar exatamente em cima da linha desenhada pelo gizmo quando vencer.
- Esperado: conta como **presente**. Se punir, a AABB do servidor e o gizmo do cliente estão
  desalinhados — conferir `center`/`size` antes de seguir para o plano 0020.

### 5. Penalidade não duplica

- Ação: deixar dois prazos vencerem com o mesmo `penaltyTaskId`, sem concluir a penalidade.
- Esperado: a tarefa extra aparece **uma vez só**; a segunda notificação pode aparecer, mas a
  lista de missões não ganha linha duplicada.

### 6. Concluir cancela o prazo

- Ação: concluir a task antes do vencimento.
- Esperado: `TaskDeadlineHud` some; **nenhum** `task_expired` depois; nenhuma penalidade.

### 7. Evento de mundo agendado

- Ação: registrar catálogo com `schedule: [{ atSeconds: 15 }]` e esperar.
- Esperado: `[NETWORK] world_event — type=…` aos ~15 s, **uma vez**, para todos os clientes na
  sala; notificação no celular.

### 8. Desconexão no vencimento

- Ação: fechar o Play Mode faltando ~5 s e reabrir depois.
- Esperado: ao reconectar e pedir tasks, a penalidade está lá (ela não se perde com a
  desconexão). Nenhum `LogError`.

### 9. Reset de sala limpa a agenda

- Ação: reconectar com `resetRoom: true` no CONN.
- Esperado: nenhum `task_deadline` nem `task_expired` remanescente da sessão anterior.

### 10. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; no backend, o `setInterval` do tick para no shutdown (`Ctrl+C` sem
  handle pendurado).

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/ScheduleService.test.ts
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/TaskAssignRequestHandler.test.ts
npm test
npx tsc --noEmit
```

### Client

Seguir os 10 passos da §6 em Play Mode.

## 8. Out of scope

- **Qualquer tarefa concreta** — reunião (0020), ponto (0024), vazamento (0023), impressora
  (0018). Aqui só relógio, zona, penalidade e notificação.
- **Ciclo de jornada / "expediente" completo** (turno com hora de entrada, almoço e saída) como
  entidade do jogo. `schedule` é uma lista de disparos relativos ao registro do catálogo, não um
  modelo de tempo do mundo. Se o TCC precisar de relógio de jogo visível, é plano próprio.
- Sincronização de relógio (NTP/offset handshake) — o protocolo usa tempo **relativo** de
  propósito; ver §2.
- Persistir penalidades em banco (Prisma): segue tudo in-memory, como todo o sistema de tasks.
- Zona não-AABB (esfera, polígono, `NavMesh` area). AABB cobre "sala do chefe" e "perto da
  catraca"; qualquer coisa mais fina é outro plano.
- Penalidade que não seja "tarefa extra" (perder pontos, reduzir velocidade, travar interação).
  O documento só cita tarefa extra.
- Escalonamento de penalidade (2ª falta pior que a 1ª).
- Interromper reunião como gatilho de punição — regra da Tarefa 10, no plano 0020.
- Mostrar no HUD o prazo de **outro** jogador.
