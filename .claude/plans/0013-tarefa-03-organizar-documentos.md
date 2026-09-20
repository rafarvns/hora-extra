# Plan 0013 — tarefa-03-organizar-documentos

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 3 — Organizar documentos**.
> **Depende de [0004](0004-infra-carregar-e-validar-itens.md)** (carregar/entregar + `pairs`) e de
> [0009](0009-infra-terminal-de-computador.md) (shell de tela com travamento de input, reusado
> pelo teclado do cadeado). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento pede cinco coisas:

> papéis espalhados pelo cenário · jogador coleta um por vez · cada documento possui uma
> categoria · cada gaveta aceita apenas uma categoria · **algumas gavetas possuem cadeado com
> senha**

Os quatro primeiros já são exatamente o `pairs` do plano 0004: o par `documento → gaveta` é o
critério de acerto, o servidor rejeita `WRONG_SLOT`, e cada documento conta uma vez só
(`ALREADY_COUNTED`). Nada a inventar.

O quinto item é a novidade — e não cabe em nenhum dos dois mecanismos existentes:

- **Não é etapa (`steps`, plano 0009).** Destravar a gaveta não é *progresso*: a tarefa não fica
  "40% organizada" porque o jogador digitou uma senha. E pela regra do plano 0009 uma task conta
  por `steps` **ou** por `items`/`pairs`, nunca pelos dois — esta task já conta por `pairs`.
- **Não é progresso de item (`task_progress`, plano 0004).** Não há item sendo entregue.

Cadeado é um **portão**, não um passo. Este plano introduz esse conceito como tal: `locks` no
catálogo e um canal de destrave próprio que **não mexe em `currentProgress`**. Entregar num slot
ainda trancado passa a ser rejeitado com `LOCKED_SLOT`.

Tratar o cadeado como portão (e não como etapa) é o que mantém a contabilidade de progresso com
uma fonte só, e é o que deixa a gaveta trancada ser opcional: se o jogador guardar todos os
documentos das gavetas abertas primeiro, o contador reflete isso honestamente.

## 2. Scope & target

**Target:** `both`

**Phase backend** — `TaskEntry` ganha `locks?: TaskSlotLock[]`; `TaskService` ganha
`unlockSlot(playerId, taskId, slotId, value)`, o estado `unlockedSlots` e a checagem de
`LOCKED_SLOT` dentro de `incrementProgress`; novo código `LOCKED_SLOT`; novo handler
`SlotUnlockAttempt.Handler.ts` registrado na factory; novo evento S→C `slot_unlocked`;
`COMMUNICATION.md` atualizado.

**Phase client** — `KeypadScreen.cs` (reusa o travamento de input do `TerminalScreen`);
`LockedSlotGate.cs`; entrada de catálogo; textos em `TaskPresentation`; setup de cena com 6
documentos, 3 gavetas (1 trancada) e o bilhete com a senha.

### Contratos cross-repo

| Evento | Direção | Payload |
| :----- | :------ | :------ |
| `task_catalog_register` | C→S | **ALTERADO** — entrada aceita `locks?: Array<{ slotId: string, expects: string }>` |
| `slot_unlock_attempt` | C→S | **NOVO** — `{ taskId: string, slotId: string, value: string }` |
| `slot_unlocked` | S→C unicast | **NOVO** — `{ taskId: string, slotId: string }` |
| `task_rejected` | S→C unicast | **ALTERADO** — `code` aceita `LOCKED_SLOT` |

Senha errada responde `task_rejected` com `WRONG_VALUE` (código já criado no plano 0009) — não
inventar um código novo para o mesmo significado.

## 3. Approach

### Backend

#### Tipos e estado (`TaskService.ts`)

```ts
export interface TaskSlotLock { slotId: string; expects: string; }

// em TaskEntry:
locks?: TaskSlotLock[];

// estado novo:
private unlockedSlots = new Map<string, Set<string>>();  // `${playerId}:${taskId}` → slotIds
```

Terceira estrutura com a mesma chave composta de `collectedItems` (0004) e `completedSteps`
(0009), limpa no mesmo `clearRoom`.

#### `unlockSlot(playerId, taskId, slotId, value)`

1. `NOT_ASSIGNED` / `INVALID_STATUS` como nos outros caminhos.
2. Entrada sem `locks`, ou `slotId` não listado → `WRONG_SLOT` (o slot não tem cadeado; tentar
   destravá-lo é payload sem sentido).
3. `normalize(value) !== normalize(expects)` → `WRONG_VALUE`. Mesma normalização do plano 0009
   (`trim().toLowerCase()`), pelo mesmo motivo de usabilidade.
4. Grava em `unlockedSlots`. **Idempotente:** destravar de novo devolve sucesso em vez de erro —
   não há nada a proteger e evita `LogError` por duplo clique num pacote UDP reenviado.
5. **Não toca em `currentProgress` nem em `status`.**

#### `incrementProgress` ganha a checagem de portão

Entre os passos 4 e 5 do plano 0004: se a entrada declara `locks` contendo o `slotId` da entrega
**e** ele não está em `unlockedSlots` → `LOCKED_SLOT`. Vem **depois** da validação de par
(`WRONG_SLOT`): documento errado na gaveta errada reporta o erro mais informativo, e o jogador não
descobre a senha certa "por eliminação" de mensagem de erro.

#### `SlotUnlockAttempt.Handler.ts` (NEW)

Registrado na `SocketHandlerFactory` como `slot_unlock_attempt`
(`.agents/rules/backend-factory-pattern.md`).

- Shape: `taskId`, `slotId`, `value` strings não-vazias; tipo errado → `ERROR` genérico.
- Sucesso → `sendTo(rinfo, 'slot_unlocked', { taskId, slotId })`. **Sem broadcast**: o destrave é
  por jogador, e `task_updated` não faz sentido porque nada de progresso mudou.
- `TaskRejectionError` → `task_rejected` unicast com `slotId` no payload.
- **Nunca logar `value`** — mesma regra do plano 0009.

#### Destrave é por jogador, não por sala

Decisão explícita: `unlockedSlots` é indexado por jogador. Dois jogadores na mesma sala precisam
cada um digitar a senha. É coerente com todo o resto do sistema (progresso é por jogador) e evita
que um jogador "resolva" o puzzle do outro sem querer. Se a mecânica cooperativa for desejada
depois, é trocar a chave do mapa — e é mudança de regra de jogo, não de estrutura.

### Client

#### `UI/KeypadScreen.cs` (NEW)

Teclado numérico do cadeado. **Não** é um `TerminalApp` (não há computador), mas herda do mesmo
`TerminalScreen` do plano 0009 pelo motivo que importa: o travamento de câmera/movimento e o
`Cursor.lockState` já estão resolvidos e testados lá. Reimplementar isso num script novo é como
se ganha um cursor preso no Editor.

- Campo de senha + botões 0–9 + confirmar/limpar; ESC fecha.
- Confirmar → `TaskSystemBridge.Instance.SendSlotUnlock(taskId, slotId, value)`.
- `slot_unlocked` → feedback de sucesso, fecha, avisa o `LockedSlotGate`.
- `task_rejected` com `WRONG_VALUE` → shake/mensagem, **campo limpo, tela aberta** — errar é
  ilimitado e retentável (plano 0009).

#### `Interactions/LockedSlotGate.cs` (NEW)

Na gaveta trancada, ao lado do `TaskDepositPoint` do plano 0004.

- `[SerializeField] private string _slotId`, `_taskType`, `_lockedPrompt`, `_keypadScreen`;
- enquanto trancada: **esconde o prompt de guardar** do `TaskDepositPoint` e mostra
  "Aperte [E] para digitar a senha"; [E] abre o `KeypadScreen`;
- em `slot_unlocked` deste `slotId`: marca destravada, abre o cadeado visualmente, devolve o
  controle do prompt ao `TaskDepositPoint`.

Desligar o prompt de entrega enquanto trancada é o que evita o jogador queimar tentativas e tomar
`LOCKED_SLOT` sem entender por quê. O código de rejeição continua existindo como rede de
segurança do servidor, não como caminho normal de UX.

#### Catálogo e apresentação

Entrada `task-organizar-documentos-01`, `type = "organize_documents"`, `targetCount = 6`:

| Documento | `_kind` (categoria) | Gaveta (`slotId`) | Trancada? |
| :-------- | :------------------ | :---------------- | :-------- |
| `doc-fiscal-01`, `doc-fiscal-02` | `fiscal` | `gaveta-fiscal` | não |
| `doc-rh-01`, `doc-rh-02` | `rh` | `gaveta-rh` | não |
| `doc-confid-01`, `doc-confid-02` | `confidencial` | `gaveta-confidencial` | **sim** (`expects: "4721"`) |

`pairs` declara os 6 pares; `locks` declara a gaveta confidencial.

`TaskPresentation` ganha `TYPE_ORGANIZE_DOCUMENTS`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Organizar os documentos" |
| `GetHowTo` | "Recolha os 6 documentos espalhados pelo escritório e guarde cada um na gaveta da sua categoria. A gaveta confidencial está trancada — a senha está anotada por aí." |
| `GetActionPrompt` | "Aperte [E] para guardar o documento (X/6)" |

#### Cena `SCN_FirstFloor.unity`

6 documentos com `CarryableItem` (`_kind` = categoria, para o `TaskDepositPoint` filtrar); 3
gavetas com `TaskDepositPoint` + `WorldTaskMarker`; a confidencial também com `LockedSlotGate`.
O **bilhete com a senha** é um objeto de mundo com um `InteractionPrompt` (plano 0004) mostrando
`"4721"` ao chegar perto — sem componente novo, sem rede: descobrir a senha é exploração, não
protocolo.

A categoria do documento precisa ser legível pelo jogador. Como não há textura por documento nesta
entrega, o `_pickupMessage` de cada `CarryableItem` carrega a informação ("Aperte [E] para pegar o
documento fiscal"), e o modo de inspeção por rotação do plano 0008 fica disponível para quem
quiser conferir na mão. Textura legível por categoria está na §8.

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/TaskService.ts                              MODIFY (TaskSlotLock, locks, unlockedSlots, unlockSlot, LOCKED_SLOT em incrementProgress)
hora-extra-backend/src/services/TaskService.test.ts                         MODIFY (ciclos 1-8)
hora-extra-backend/src/sockets/handlers/SlotUnlockAttempt.Handler.ts        NEW
hora-extra-backend/src/sockets/handlers/SlotUnlockAttempt.Handler.test.ts   NEW (ciclos 9-13)
hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts           MODIFY (registra slot_unlock_attempt)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.Handler.ts  MODIFY (valida shape de locks)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.test.ts     MODIFY (ciclo 14)
hora-extra-backend/docs/Networking/COMMUNICATION.md                         MODIFY (locks, slot_unlock_attempt, slot_unlocked, LOCKED_SLOT)
```

### Client

```
hora-extra-client/Assets/Scripts/UI/KeypadScreen.cs                     NEW
hora-extra-client/Assets/Scripts/Interactions/LockedSlotGate.cs         NEW
hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs               MODIFY (SLOT_UNLOCK_ATTEMPT, SLOT_UNLOCKED)
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs           MODIFY (TaskSlotLock, locks, SlotUnlockAttemptPayload, SlotUnlockedPayload)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs         MODIFY (SendSlotUnlock, OnSlotUnlocked, EnsureOrganizeDocumentsEntry, TaskEntryData.locks)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs                 MODIFY (TYPE_ORGANIZE_DOCUMENTS)
hora-extra-client/Assets/Prefab/PFB_UI_Keypad.prefab                    NEW (asset, Editor)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                    MODIFY (asset, Editor — 6 documentos, 3 gavetas, bilhete)
hora-extra-client/Docs/Mechanics/TASK-03-DOCUMENTOS.md                  NEW
```

## 5. TDD breakdown (phase: backend)

### TaskService (`TaskService.test.ts`)

- Cycle 1: `it('unlockSlot aceita a senha correta e registra o slot como destravado')`.
- Cycle 2: `it('unlockSlot lança WRONG_VALUE com senha errada e NÃO destrava')`.
- Cycle 3: `it('unlockSlot normaliza caixa e espaços')`.
- Cycle 4: `it('unlockSlot lança WRONG_SLOT quando o slot não tem cadeado declarado')`.
- Cycle 5: `it('unlockSlot é idempotente — destravar de novo não lança')`.
- Cycle 6: `it('unlockSlot NÃO altera currentProgress nem status')` → o teste que fixa a decisão
  central deste plano.
- Cycle 7: `it('incrementProgress lança LOCKED_SLOT ao entregar em slot com cadeado não destravado')`
  → `currentProgress` inalterado.
- Cycle 8: `it('incrementProgress aceita a entrega após unlockSlot')` → e `clearRoom` limpa
  `unlockedSlots`, fazendo a gaveta voltar a trancar.

Ordem de precedência (`WRONG_SLOT` antes de `LOCKED_SLOT`) é verificada dentro do ciclo 7 com um
par inválido em slot trancado.

### SlotUnlockAttemptHandler (`SlotUnlockAttempt.Handler.test.ts`)

- Cycle 9: `it('repassa taskId, slotId e value para unlockSlot')`.
- Cycle 10: `it('rejeita value ausente ou não-string com ERROR genérico')`.
- Cycle 11: `it('responde slot_unlocked unicast em sucesso e NÃO faz broadcast')`.
- Cycle 12: `it('responde task_rejected com WRONG_VALUE e slotId quando a senha está errada')`.
- Cycle 13: `it('não escreve value em nenhum log')` → spy no `logger`.

### TaskCatalogRegisterHandler

- Cycle 14: `it('descarta locks malformado (entrada sem expects) com warn e registra o resto')`.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. Estado inicial

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: how-to da §3 e `0/6`; `WorldTaskMarker` nas 3 gavetas; cadeado visível na gaveta
  confidencial.

### 2. Gaveta aberta, categoria certa

- Ação: pegar `doc-fiscal-01` e guardar em `gaveta-fiscal`.
- Esperado: `task_progress … itemId=doc-fiscal-01 slotId=gaveta-fiscal` → `task_updated … 1/6` →
  **só então** o documento some da mão.

### 3. Categoria errada

- Ação: levar `doc-rh-01` até `gaveta-fiscal`.
- Esperado: o prompt nem aparece (`_acceptedKind` diferente). Forçando o envio:
  `task_rejected — code=WRONG_SLOT`, documento volta para a mão, contador não muda.

### 4. Gaveta trancada bloqueia a entrega

- Ação: pegar `doc-confid-01` e chegar na gaveta confidencial.
- Esperado: prompt de guardar **não** aparece; aparece "Aperte [E] para digitar a senha".

### 5. Senha errada é retentável

- Ação: [E] na gaveta, digitar `0000`, confirmar.
- Esperado: `task_rejected — code=WRONG_VALUE`; campo limpo; **tela continua aberta**; cadeado
  continua fechado; contador não muda.

### 6. Senha não vaza no log

- Ação: procurar `4721` e `0000` no Console do Unity e em `hora-extra-backend/logs/`.
- Esperado: nenhuma ocorrência vinda do fluxo de destrave.

### 7. Achar a senha no mundo

- Ação: encontrar o bilhete e chegar perto.
- Esperado: o prompt mostra `4721`. Nenhum pacote é enviado — é objeto puramente local.

### 8. Destravar e guardar

- Ação: digitar `4721`.
- Esperado: `[GAMEPLAY] slot_unlocked — slotId=gaveta-confidencial`; cadeado abre; tela fecha;
  contador **não muda** (destravar não é progresso). Em seguida, [E] guarda o documento → `3/6`.

### 9. Destravado continua destravado

- Ação: sair, andar, voltar com o segundo documento confidencial.
- Esperado: o keypad **não** reaparece; o prompt de guardar aparece direto.

### 10. Documento repetido

- Ação: reativar `doc-fiscal-01` pelo Inspector e guardar de novo.
- Esperado: `task_rejected — code=ALREADY_COUNTED`; volta para a mão; contador inalterado.

### 11. Concluir

- Ação: guardar os 6.
- Esperado: `status=completed progress=6`; marcadores somem; prompts deixam de aparecer.

### 12. Cleanup

- Ação: sair do Play Mode com o keypad aberto.
- Esperado: nenhum `LogError`; cursor normal no Editor.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/SlotUnlockAttempt.Handler.test.ts
npx vitest run src/sockets/handlers/TaskCatalogRegisterHandler.test.ts
npm test
npx tsc --noEmit
```

### Client

Seguir os 12 passos da §6 em Play Mode.

## 8. Out of scope

- **Senha gerada pelo servidor** e desconhecida do cliente. `expects` vem do catálogo, com a
  mesma limitação documentada na "Nota de autoridade" do plano 0009.
- Senha diferente por jogador ou por partida (o valor é fixo no catálogo).
- Limite de tentativas, cooldown ou alarme após N erros.
- Destrave compartilhado entre jogadores da mesma sala (ver "Destrave é por jogador" na §3).
- Textura legível de categoria em cada documento; nesta entrega a categoria vem pelo texto do
  prompt de pickup.
- Animação de gaveta abrindo/fechando e de cadeado.
- Gaveta com capacidade limitada ou ordem interna dos documentos.
- Mais de um cadeado por gaveta, ou cadeado com chave física em vez de senha.
- Snap visual do documento dentro da gaveta — a gaveta é recipiente (`TaskDepositPoint`), não
  slot de posição; para posição-alvo ver plano [0011](0011-infra-slot-de-posicionamento.md).
