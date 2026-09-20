# Plan 0004 — infra-carregar-e-validar-itens

> **Plano-base da entrega.** Os planos 0005 (Tarefa 16), 0006 (Tarefa 18), 0007 (Tarefa 15)
> e 0008 (Tarefa 17) — todos derivados de `Explicação das tarefas para essa entrega.pdf` —
> dependem da infraestrutura definida aqui. Implementar este plano **primeiro**.

## 1. Context

Os planos 0001–0003 entregaram o sistema de tasks autoritativo: catálogo por sala
(`task_catalog_register`), sorteio de N=3 tasks (`task_assign_request` → `task_assigned`),
progresso incremental (`task_progress` → `task_updated`) e o QTE da cafeteira
(`task_start_interaction` / `task_complete_attempt`).

As quatro tarefas do documento de entrega (15, 16, 17 e 18) compartilham um mesmo verbo de
gameplay que **ainda não existe no projeto**: o jogador **pega um objeto do cenário, carrega
na "mão", leva até um recipiente/lugar e pressiona [E] para guardar**. Hoje o único coletável
(`MissionPaperCollectible`) envia `task_progress` no instante em que o jogador aperta [E] em
cima do papel — não há carregamento, não há destino, e o servidor não sabe *qual* item foi
coletado nem *onde* foi entregue.

Essa lacuna quebra duas exigências do documento:

- **Tarefa 17** exige que o servidor saiba distinguir "encomenda certa na estante certa" de
  "encomenda certa na estante errada" — o par item→destino é o próprio critério de acerto.
- **Tarefas 15/16/18** exigem que cada item conte **uma única vez**; hoje um cliente poderia
  enviar 4× `task_progress` sem sair do lugar e completar a task.

Este plano entrega a fundação compartilhada: o sistema de carregar/entregar no cliente e a
extensão do protocolo que dá ao servidor identidade de item e destino, mantendo a regra
`.agents/rules/backend-design-pattern.md` (servidor é source of truth — nunca ecoa dado cru
do cliente).

## 2. Scope & target

**Target:** `both`

**Phase backend** — `TaskEntry` ganha os campos opcionais `items` e `pairs`; `task_progress`
ganha os campos opcionais `itemId` e `slotId`; `TaskService.incrementProgress` ganha validação
de pertencimento, pareamento e deduplicação; novo erro tipado `TaskRejectionError`; novo evento
S→C `task_rejected` (unicast ao remetente); `TaskProgress.Handler.ts` estendido; atualização de
`COMMUNICATION.md`.

**Phase client** — novo domínio `Assets/Scripts/Interactions/` com `InteractionInput.cs`,
`InteractionPrompt.cs`, `CarryableItem.cs`, `PlayerCarrier.cs` e `TaskDepositPoint.cs`;
`TaskSystemBridge` ganha overload de `SendProgress`, listener de `task_rejected` e evento
`OnTaskRejected`; `TaskEntryData` ganha `items`/`pairs`; `NetworkEvents.cs` ganha
`TASK_REJECTED`; `TaskModels.cs` ganha os DTOs correspondentes.

### Contratos cross-repo

Entradas novas/alteradas em `hora-extra-backend/docs/Networking/COMMUNICATION.md`:

| Evento | Direção | Payload |
|--------|---------|---------|
| `task_catalog_register` | C→S | **ALTERADO** — cada entrada de `tasks[]` aceita `items?: string[]` e `pairs?: Array<{ itemId: string, slotId: string }>` |
| `task_progress` | C→S | **ALTERADO** — `{ taskId: string, itemId?: string, slotId?: string }` |
| `task_rejected` | S→C unicast | **NOVO** — `{ taskId: string, code: string, message: string, itemId?: string }` |

Compatibilidade: os três campos novos de payload são **opcionais**. Entradas de catálogo sem
`items`/`pairs` mantêm o comportamento atual (contagem cega +1), então a task `coffee_maker`
e a task `collect` existentes continuam funcionando sem alteração.

## 3. Approach

### Backend

#### Tipos (`TaskService.ts`)

```ts
export interface TaskItemPair { itemId: string; slotId: string; }

export interface TaskEntry {
    id: string;
    description: string;
    type: string;
    targetCount: number;
    roomId: string;
    items?: string[];          // itemIds que contam progresso nesta task
    pairs?: TaskItemPair[];     // destino obrigatório por item (tarefas de pareamento)
}

export type TaskRejectionCode =
    | 'NOT_ASSIGNED'     // jogador não tem a task
    | 'INVALID_STATUS'   // task já completed/failed
    | 'MISSING_ITEM'     // catálogo exige itemId e o payload não mandou
    | 'UNKNOWN_ITEM'     // itemId não pertence a esta task
    | 'WRONG_SLOT'       // par (itemId, slotId) não confere
    | 'ALREADY_COUNTED'; // itemId já contabilizado nesta atribuição

export class TaskRejectionError extends ApiError {
    public readonly code: TaskRejectionCode;
    constructor(code: TaskRejectionCode, message: string) { super(message, 400); ... }
}
```

`TaskRejectionError` estende `ApiError` para não quebrar nenhum `catch (err: any)` existente —
handlers antigos continuam lendo `err.message`; o handler novo lê também `err.code`.

#### Estado privado novo no `TaskService`

```ts
private assignedRoom   = new Map<string, string>();               // playerId → roomId
private collectedItems = new Map<string, Set<string>>();          // `${playerId}:${taskId}` → itemIds já contados
```

- `assignRandomTasks` passa a gravar `assignedRoom.set(playerId, roomId)`. É o que permite ao
  `incrementProgress` (que só recebe `playerId`) localizar a entrada de catálogo correta, já
  que o catálogo é **por sala**.
- `clearRoom` limpa as duas estruturas novas além de `assignments`.

#### `incrementProgress(playerId, taskId, itemId?, slotId?)`

Ordem de validação (cada falha lança `TaskRejectionError` com o código correspondente):

1. Jogador sem tasks → `NOT_ASSIGNED`.
2. `taskId` não encontrado no array do jogador → `NOT_ASSIGNED`.
3. `status` fora de `pending` / `in_progress` → `INVALID_STATUS`.
4. Localiza a `TaskEntry` via `catalog.get(assignedRoom.get(playerId))`. Se a entrada declara
   `items` **ou** `pairs`:
   - `itemId` ausente/vazio → `MISSING_ITEM`.
   - `itemId` fora de `items` (quando `items` declarado) → `UNKNOWN_ITEM`.
   - `pairs` declarado e não existe par com esse `itemId` → `UNKNOWN_ITEM`.
   - `pairs` declarado e `pair.slotId !== slotId` → `WRONG_SLOT`.
   - `itemId` já presente em `collectedItems` → `ALREADY_COUNTED`.
5. Registra o `itemId` em `collectedItems` (quando houver itemId).
6. Incrementa como hoje: `pending → in_progress` no primeiro incremento, `currentProgress += 1`
   clampeado em `targetCount`, `completed` ao atingir a meta.

Entradas de catálogo **sem** `items` e **sem** `pairs` pulam o passo 4 inteiro — comportamento
idêntico ao atual.

#### `TaskProgress.Handler.ts`

- Validação de payload passa a aceitar `itemId`/`slotId` opcionais (`string` quando presentes;
  tipo errado → `ERROR` genérico, que é falha de protocolo e não de gameplay).
- Repassa `data.itemId` / `data.slotId` ao service.
- No `catch`: se `err instanceof TaskRejectionError`, responde
  `sendTo(rinfo, 'task_rejected', { taskId, code: err.code, message: err.message, itemId })`.
  Qualquer outro erro mantém o `ERROR` genérico de hoje.
- Sucesso segue igual: `broadcastToRoom(roomId, 'task_updated', { playerId, taskId, currentProgress, status })`.

`task_rejected` é **unicast** (`sendTo`), não broadcast — a rejeição é feedback do remetente,
não estado de sala.

#### `TaskCatalogRegisterHandler`

Passa `items`/`pairs` adiante quando presentes, com validação de shape (array de string /
array de `{itemId, slotId}`); campos malformados são descartados com `logger.warn` em vez de
derrubar o registro inteiro do catálogo.

### Client

Novo domínio `Assets/Scripts/Interactions/` (ao lado de `AI/`, `Characters/`, `Network/`, `UI/`),
namespace `HoraExtra.Interactions`. Motivo: nem `Characters/` nem `UI/` descrevem objetos de
cenário interativos, e as quatro tarefas da entrega adicionam dezenas deles.

**`InteractionInput.cs`** — helper estático que centraliza o `#if ENABLE_INPUT_SYSTEM` /
`ENABLE_LEGACY_INPUT_MANAGER` hoje duplicado em `MissionPaperCollectible` e
`CoffeeMakerInteraction`. Expõe `InteractPressed()` (borda) e `InteractHeld()` (contínuo, usado
pelo plano 0006).

**`InteractionPrompt.cs`** — `MonoBehaviour` que encapsula mostrar/ocultar um `Text` de prompt
de mundo. `Show(string message)` / `Hide()`. Elimina a duplicação de `ShowPrompt`/`HidePrompt`.

**`CarryableItem.cs`** — item do cenário que pode ser pego:
- `[SerializeField] private string _itemId` — id único, usado no `task_progress` e declarado em
  `items`/`pairs` no catálogo. **Precisa bater exatamente com o catálogo.**
- `[SerializeField] private string _kind` — categoria aceita pelo destino (`"papel"`, `"caneta"`,
  `"rodo"`…). O `TaskDepositPoint` filtra por `_kind`, não por `_itemId`.
- `[SerializeField] private bool _isTool` — `true` para rodo/vassoura: ferramenta **não é
  consumida** na entrega, continua na mão do jogador (usado pelo plano 0006).
- `[SerializeField] private string _pickupMessage` — ex: `"Aperte [E] para pegar o papel"`.
- Detecção de proximidade por `OnTriggerEnter`/`OnTriggerExit` (o documento confirma que todos
  os assets **já têm box collider com is trigger ativado**). Em `Update`, com o jogador dentro
  do trigger e nada nas mãos, `InteractionInput.InteractPressed()` → `PlayerCarrier.TryPickup(this)`.

**`PlayerCarrier.cs`** — componente no prefab `Player`. Segura **no máximo um** item por vez:
- `[SerializeField] private Transform _handAnchor` — ponto de encaixe visual do item carregado.
- `public CarryableItem Carried { get; }`, `public bool IsCarrying { get; }`.
- `TryPickup(CarryableItem item)` — recusa (com log `[GAMEPLAY]`) se já está carregando; senão
  reparenta o item em `_handAnchor`, desliga o collider, zera a rotação local.
- `Consume()` — chamado pelo `TaskDepositPoint` após o servidor **confirmar** a entrega: se
  `_isTool`, mantém na mão; senão desativa o GameObject e limpa `Carried`.
- `DropCurrent()` — solta o item de volta no mundo (usado ao trocar de ferramenta).
- `public static event Action<CarryableItem> OnCarryChanged` — Observer para HUD/prompts.

**`TaskDepositPoint.cs`** — o recipiente (caixa de papéis, porta-canetas, máquina de vendas,
lixo, estante):
- `[SerializeField] private string _slotId` — destino, enviado no `task_progress`.
- `[SerializeField] private string _acceptedKind` — categoria aceita.
- `[SerializeField] private string _taskType` — tipo de task que este destino atende.
- `[SerializeField] private string _promptMessage` — ex: `"Aperte [E] para guardar o papel"`.
- Gate de exibição do prompt (tudo precisa ser verdade): jogador dentro do trigger **e**
  `PlayerCarrier.IsCarrying` com `Carried.Kind == _acceptedKind` **e**
  `TaskSystemBridge.Instance.FindMyTask(_taskType, "pending", "in_progress") != null`.
- Ao pressionar [E]: guarda o item entregue num campo `_pendingItem`, envia
  `TaskSystemBridge.Instance.SendProgress(task.Id, item.ItemId, _slotId)` e **espera a
  confirmação do servidor**. O item só some da mão no callback de `task_updated`
  (`OnTaskUpdated` com `taskId` igual e `currentProgress` maior que o último visto) → `carrier.Consume()`.
  Se vier `OnTaskRejected` com esse `taskId`, o item **volta** para a mão e o prompt exibe
  `payload.Message` por alguns segundos.

Esse ida-e-volta é o ponto central da regra server-authoritative: o mundo visual só muda depois
que o servidor contabilizou. É o que faz a Tarefa 17 (estante errada) funcionar sem lógica de
acerto duplicada no cliente.

**`TaskSystemBridge.cs`** (MODIFY):
- Overload `SendProgress(string taskId, string itemId, string slotId)` — mantém o
  `SendProgress(string taskId)` atual delegando com `itemId`/`slotId` nulos (Newtonsoft omite
  `null` só com `NullValueHandling.Ignore`; usar `[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]`
  nos dois campos novos do DTO para não mandar `"itemId": null` na rede).
- Assina `NetworkEvents.TASK_REJECTED` em `OnEnable`, remove em `OnDisable`.
- `public static event Action<TaskRejectedPayload> OnTaskRejected`.
- `TaskEntryData` ganha `public List<string> items` e `public List<TaskPairData> pairs`, ambos
  serializáveis no Inspector (`TaskPairData` é `[Serializable] class { public string itemId; public string slotId; }`
  — `Dictionary` não serializa no Inspector do Unity, por isso o par é uma lista).
- `RegisterCatalog()` copia `items`/`pairs` para o `TaskEntry` enviado.

**`TaskPresentation.cs`** (MODIFY) — apenas o fallback genérico de carregar/entregar
(`"Pressione E para guardar"`); as constantes e textos de cada tarefa entram nos planos 0005–0008.

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/TaskService.ts                              MODIFY (TaskItemPair, TaskRejectionError, items/pairs, assignedRoom, collectedItems, incrementProgress validado)
hora-extra-backend/src/services/TaskService.test.ts                         MODIFY (ciclos 1-12)
hora-extra-backend/src/sockets/handlers/TaskProgress.Handler.ts             MODIFY (itemId/slotId opcionais, resposta task_rejected)
hora-extra-backend/src/sockets/handlers/TaskProgress.Handler.test.ts        MODIFY (ciclos 13-19)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.Handler.ts     MODIFY (repassa items/pairs com validação de shape)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.test.ts        MODIFY (ciclos 20-22)
hora-extra-backend/docs/Networking/COMMUNICATION.md                         MODIFY (task_progress, task_catalog_register, task_rejected, schema TaskEntry)
```

Nenhum handler novo → `SocketHandler.Factory.ts` **não** muda (`task_rejected` é S→C).

### Client

```
hora-extra-client/Assets/Scripts/Interactions/InteractionInput.cs           NEW
hora-extra-client/Assets/Scripts/Interactions/InteractionPrompt.cs          NEW
hora-extra-client/Assets/Scripts/Interactions/CarryableItem.cs              NEW
hora-extra-client/Assets/Scripts/Interactions/PlayerCarrier.cs              NEW
hora-extra-client/Assets/Scripts/Interactions/TaskDepositPoint.cs           NEW
hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs                   MODIFY (TASK_REJECTED)
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs               MODIFY (TaskItemPair, items/pairs em TaskEntry, itemId/slotId em TaskProgressPayload, TaskRejectedPayload)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs             MODIFY (overload SendProgress, TASK_REJECTED, OnTaskRejected, TaskEntryData.items/pairs)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs                     MODIFY (fallback de prompt de entrega)
hora-extra-client/Assets/Prefab/Player.prefab                               MODIFY (asset, Editor — adicionar PlayerCarrier + child _handAnchor)
```

## 5. TDD breakdown (phase: backend)

### TaskService — validação de item (`TaskService.test.ts`)

- Cycle 1: `it('incrementProgress sem items/pairs no catálogo mantém o comportamento atual de +1')` → regressão: entrada legada continua contando.
- Cycle 2: `it('incrementProgress aceita itemId declarado em items e soma +1')` → happy path com `items`.
- Cycle 3: `it('incrementProgress lança MISSING_ITEM quando o catálogo declara items e o payload não manda itemId')`.
- Cycle 4: `it('incrementProgress lança UNKNOWN_ITEM quando itemId não está em items')`.
- Cycle 5: `it('incrementProgress lança ALREADY_COUNTED ao repetir o mesmo itemId')` → chamar 2× com o mesmo id; `currentProgress` continua 1.
- Cycle 6: `it('incrementProgress aceita par (itemId, slotId) declarado em pairs')` → happy path de pareamento.
- Cycle 7: `it('incrementProgress lança WRONG_SLOT quando slotId não confere com o par declarado')` → `currentProgress` inalterado.
- Cycle 8: `it('incrementProgress lança UNKNOWN_ITEM quando itemId não está em pairs')`.
- Cycle 9: `it('incrementProgress lança NOT_ASSIGNED quando o jogador não tem a task')`.
- Cycle 10: `it('incrementProgress lança INVALID_STATUS quando a task já está completed')`.
- Cycle 11: `it('TaskRejectionError expõe code e é instanceof ApiError')` → garante que `catch` antigos não quebram.
- Cycle 12: `it('clearRoom limpa assignedRoom e collectedItems — mesmo itemId volta a contar após reset')`.

### TaskProgressHandler (`TaskProgress.Handler.test.ts`)

- Cycle 13: `it('repassa itemId e slotId do payload para incrementProgress')` → spy nos 4 argumentos.
- Cycle 14: `it('aceita payload sem itemId/slotId — retrocompatibilidade')` → chama service com `undefined`.
- Cycle 15: `it('rejeita itemId de tipo não-string com ERROR genérico')` → falha de protocolo, não de gameplay.
- Cycle 16: `it('responde task_rejected com code e message quando o service lança TaskRejectionError')` → `sendTo` com evento `task_rejected`; `broadcastToRoom` não chamado.
- Cycle 17: `it('inclui o itemId original no payload de task_rejected')`.
- Cycle 18: `it('responde ERROR genérico quando o erro não é TaskRejectionError')`.
- Cycle 19: `it('faz broadcastToRoom task_updated em sucesso com itemId presente')` → payload de broadcast **não** contém `itemId`/`slotId`.

### TaskCatalogRegisterHandler (`TaskCatalogRegisterHandler.test.ts`)

- Cycle 20: `it('repassa items e pairs ao registerCatalog quando presentes')`.
- Cycle 21: `it('descarta items malformado (não-array de string) com warn e registra o resto do catálogo')`.
- Cycle 22: `it('descarta pairs com entrada sem itemId ou sem slotId')`.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev`), `SCN_Main.unity` aberta, `SocketManager.UseTestToken = true`,
Console com "Clear on Play". Para este plano-base, usar uma **cena de sanidade**: um
`CarryableItem` (`_itemId="sanity-item-01"`, `_kind="papel"`) e um `TaskDepositPoint`
(`_slotId="sanity-slot"`, `_acceptedKind="papel"`, `_taskType="collect"`), com a entrada de
catálogo `task-collect-papers-01` ajustada no Inspector para `items = ["sanity-item-01"]`.

### 1. Catálogo com items/pairs

- Ação: entrar em Play Mode.
- Esperado: `[NETWORK] task_catalog_register enviado — N tarefa(s)`; backend loga o registro sem warn de shape.

### 2. Pegar o item

- Ação: andar até o item e pressionar [E].
- Esperado: `[GAMEPLAY] PlayerCarrier — item 'sanity-item-01' pego`. O objeto encosta na mão do
  personagem (segue o `_handAnchor`) e não fica mais no chão.

### 3. Gate de mão cheia

- Ação: andar até outro `CarryableItem` e pressionar [E].
- Esperado: prompt de pegar **não** aparece; se aparecer o log, é
  `[GAMEPLAY] PlayerCarrier — já carregando, pickup recusado`. Nenhum `LogError`.

### 4. Gate do destino sem task

- Pré-condição: task `collect` **não** atribuída (desmarcar `_autoRequestTasksOnConnect`).
- Ação: chegar no `TaskDepositPoint` carregando o item.
- Esperado: prompt "Aperte [E] para guardar…" **não** aparece.

### 5. Entrega válida — confirmação autoritativa

- Pré-condição: task atribuída, carregando o item.
- Ação: entrar no trigger do destino e pressionar [E].
- Esperado, **nesta ordem**:
  - `[NETWORK] task_progress enviado — taskId=… itemId=sanity-item-01 slotId=sanity-slot`.
  - `[GAMEPLAY] task_updated recebido — taskId=… status=in_progress progress=1`.
  - **só então** o item some da mão. Se sumir antes do `task_updated`, o fluxo autoritativo está
    invertido — é falha.

### 6. Item repetido é rejeitado (ALREADY_COUNTED)

- Ação: reativar o mesmo GameObject do item pelo Inspector, pegar e entregar de novo.
- Esperado: `[NETWORK] task_rejected — code=ALREADY_COUNTED taskId=…`; o item **volta para a mão**;
  o contador do HUD não muda.

### 7. Destino errado é rejeitado (WRONG_SLOT)

- Pré-condição: trocar a entrada de catálogo para usar `pairs = [{ itemId: "sanity-item-01", slotId: "outro-slot" }]`.
- Ação: entregar no `TaskDepositPoint` de `_slotId="sanity-slot"`.
- Esperado: `[NETWORK] task_rejected — code=WRONG_SLOT`; item volta para a mão; mensagem do
  servidor exibida no prompt.

### 8. Retrocompatibilidade do QTE da cafeteira

- Ação: executar o fluxo do plano 0003 (cafeteira → QTE → sucesso).
- Esperado: comportamento idêntico ao de antes desta mudança — nenhum `task_rejected`.

### 9. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError` residual; nenhum item órfão parented no `_handAnchor`.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/TaskProgress.Handler.test.ts
npx vitest run src/sockets/handlers/TaskCatalogRegisterHandler.test.ts
npm test
npx tsc --noEmit
```

### Client

Seguir os 9 passos da §6 em Play Mode.

## 8. Out of scope

- Os assets, prefabs e entradas de catálogo das Tarefas 15/16/17/18 — cada um no seu plano
  (0005–0008). Este plano entrega só a mecânica e o protocolo.
- Carregar **mais de um** item por vez (inventário). `PlayerCarrier` é single-slot por decisão
  de escopo; se uma tarefa precisar de inventário, é outro plano.
- Hold-to-interact (segurar [E]) — `InteractionInput.InteractHeld()` fica declarado aqui mas
  quem consome é o plano 0006.
- Rotacionar o objeto carregado — plano 0008.
- Destaque visual de slot vazio ("ficar chamativo") — plano 0007.
- Persistência em banco (Prisma): todo o estado de task segue in-memory.
- Sincronizar o item carregado entre jogadores (outro player não vê o papel na mão do colega) —
  exigiria evento novo de estado de carregamento; fora do escopo desta entrega.
- Anti-cheat de posição: o servidor valida identidade e destino do item, **não** valida se o
  jogador estava fisicamente perto do recipiente.
