# Plan 0019 — tarefa-09-transporte-cooperativo

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 9 — Transporte cooperativo de itens**.
> **Depende de [0004](0004-infra-carregar-e-validar-itens.md)** (carregar/entregar, `pairs`,
> `task_rejected`). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> certos objetos são pesados ou volumosos demais para um único jogador → **dois ou mais jogadores
> precisam interagir simultaneamente** com o objeto → enquanto transportam, os jogadores têm
> **movimentação reduzida** → o item deve ser levado até um local de destino específico

É a **única tarefa das 18 que exige mais de um jogador**, e a primeira em que o estado de um
objeto pertence a um grupo em vez de a uma pessoa. Três coisas quebram aqui.

**`PlayerCarrier` é single-owner.** O plano 0004 modelou "o item está na mão do jogador X" — uma
mão, um dono. Aqui o item está em duas mãos ao mesmo tempo, e sair da mão de um não o solta.

**O servidor não tem conceito de estado compartilhado de objeto.** Todo o estado de task é
`playerId → tasks`. "Quem está segurando o sofá" não é estado de jogador nem de task: é estado de
**objeto**, e não existe lugar para isso hoje.

**Progresso é por jogador.** Se dois jogadores carregam o sofá até o destino, os dois cumpriram a
tarefa. O `incrementProgress` do plano 0004 credita um `playerId` só.

### Decisão: posição do objeto é derivada, não sincronizada

A tentação seria criar um evento de posição para o objeto co-carregado. Descartado. O servidor já
transmite a posição autoritativa de cada jogador (`state_update`, 20 Hz), e todo cliente já as
tem. O objeto co-carregado então **segue o ponto médio entre os carregadores**, calculado
localmente em cada cliente a partir de posições que já chegam.

Isso custa zero banda, é consistente entre clientes por construção (mesma entrada, mesma conta) e
não inventa um segundo canal de posição concorrendo com o que já existe. O preço é que o objeto
não tem física própria enquanto carregado — ele interpola entre as duas pessoas. Para uma caixa
grande ou um sofá, é aceitável; ver §8.

### Decisão: o servidor credita todos os carregadores

`task_progress` de um carregador credita progresso a **todos** que estavam segurando no momento
da entrega. Não é conveniência: é a regra do documento (*"dois ou mais jogadores precisam
interagir"*) — quem carregou cumpriu. A alternativa (só quem apertou [E] ganha) transformaria
cooperação em corrida pelo botão.

## 2. Scope & target

**Target:** `both`

**Phase backend** — novo `CoopCarryService` (+ `ServiceFactory`); `TaskEntry` ganha
`coop?: { requiredCarriers: number }`; novo código `COOP_NOT_READY`; `incrementProgress` ganha o
gate de cooperação e passa a creditar múltiplos jogadores; 2 handlers novos
(`CoopGrab.Handler.ts`, `CoopRelease.Handler.ts`) registrados na factory; novo evento
`coop_state`; limpeza na desconexão; `COMMUNICATION.md` atualizado.

**Phase client** — `CoopCarryable.cs`, `CoopCarryFollower.cs`; `PlayerController` ganha o
multiplicador de velocidade; `TaskSystemBridge` ganha `SendCoopGrab`/`SendCoopRelease` e
`OnCoopState`; entrada de catálogo; textos; setup de cena.

### Contratos cross-repo

| Evento | Direção | Payload |
| :----- | :------ | :------ |
| `task_catalog_register` | C→S | **ALTERADO** — entrada aceita `coop?: { requiredCarriers: number }` |
| `coop_grab` | C→S | **NOVO** — `{ taskId: string, itemId: string }` |
| `coop_release` | C→S | **NOVO** — `{ itemId: string }` |
| `coop_state` | S→C broadcast | **NOVO** — `{ itemId: string, carriers: string[], active: boolean }` |
| `task_updated` | S→C broadcast | **INALTERADO no shape** — passa a ser emitido **uma vez por carregador** numa entrega cooperativa |
| `task_rejected` | S→C unicast | **ALTERADO** — `code` aceita `COOP_NOT_READY` |

`coop_state` é **broadcast**: quem está segurando o quê é estado de sala, ao contrário de
`task_rejected`/`task_step_ack`, que são feedback do remetente. `active` é
`carriers.length >= requiredCarriers` — calculado pelo servidor, para os clientes não divergirem.

## 3. Approach

### Backend

#### `CoopCarryService.ts` (NEW)

Estado de **objeto**, não de jogador — por isso service próprio, registrado na `ServiceFactory`
(`.agents/rules/backend-factory-pattern.md`).

```ts
private carriers = new Map<string, Set<string>>();   // itemId → playerIds
private itemRoom = new Map<string, string>();        // itemId → roomId
```

- `grab(roomId, playerId, itemId): string[]` — adiciona e devolve a lista atual. Idempotente:
  segurar de novo não duplica (é `Set`) e não é erro — pacote UDP repetido não pode virar
  `LogError`.
- `release(playerId, itemId): string[]` — remove; se o set esvaziar, limpa as duas entradas.
- `releaseAllFor(playerId): string[]` — **chamado na desconexão e no reaper de sessão inativa**.
  Sem isso, um jogador que fecha o jogo segurando o sofá deixa o objeto travado em "1 de 2" para
  sempre, e a task fica impossível. Este é o bug mais provável deste plano.
- `getCarriers(itemId): string[]`, `clearRoom(roomId)`.

#### `TaskService` (MODIFY)

- `TaskEntry.coop?: { requiredCarriers: number }`.
- `TaskRejectionCode` ganha `COOP_NOT_READY`.
- `incrementProgress` ganha um parâmetro `carriers?: string[]`:
  - se a entrada declara `coop` e `carriers.length < requiredCarriers` → `COOP_NOT_READY`,
    **antes** de consumir o item (senão a entrega falha e o item fica marcado como contado);
  - em sucesso, credita **cada** `playerId` de `carriers` que tenha a task atribuída. Quem não
    tem a task é ignorado em silêncio — um jogador pode ajudar a carregar sem ter a tarefa, e
    isso não pode virar erro.
- A validação de `pairs`/`items`/`ALREADY_COUNTED` do plano 0004 roda por jogador creditado, como
  hoje.

Ordem do gate: `COOP_NOT_READY` é avaliado **depois** de `WRONG_SLOT` — entregar no lugar errado
com gente suficiente reporta o erro mais útil.

#### Handlers (NEW)

`CoopGrab.Handler.ts` e `CoopRelease.Handler.ts`, registrados na `SocketHandlerFactory` como
`coop_grab` e `coop_release`. Cada um valida shape, chama o service e faz
`broadcastToRoom(roomId, 'coop_state', { itemId, carriers, active })`.

O `TaskProgressHandler` (plano 0004) passa a consultar
`ServiceFactory.getCoopCarryService().getCarriers(itemId)` e a repassar a lista ao
`incrementProgress`; para itens sem carregadores registrados, a lista é `[playerId]` e o
comportamento fica **idêntico ao de hoje** — nenhuma regressão nas tarefas 15–18.

Como a entrega credita N jogadores, o handler emite **um `task_updated` por jogador creditado**.
O shape do evento não muda; o que muda é a quantidade. Isso mantém o HUD de todo mundo coerente
sem inventar um payload plural.

#### Desconexão

`UdpSocketManager`, no ponto onde já remove a sessão (desconexão explícita e reaper de 30 s):
chamar `releaseAllFor(playerId)` e fazer broadcast do `coop_state` resultante.

### Client

#### `Interactions/CoopCarryable.cs` (NEW)

O objeto pesado. **Não** usa `PlayerCarrier` — é o ponto da §1.

- `[SerializeField] private string _itemId`, `_taskType`, `_grabPromptMessage`;
- `[SerializeField] private int _requiredCarriers = 2` — espelha o catálogo, só para UI;
- trigger + [E] → `SendCoopGrab(taskId, _itemId)`; [E] de novo (ou afastar-se demais) →
  `SendCoopRelease(_itemId)`;
- assina `OnCoopState` do próprio `_itemId`: guarda a lista de carregadores e o `active`;
- prompt mostra o estado do grupo: *"Aperte [E] para ajudar a carregar (1/2)"* → *"Carregando
  (2/2) — levem até o destino"*. Mostrar a contagem é o que deixa o jogador entender que precisa
  de ajuda, em vez de achar que o objeto está bugado.

#### `Interactions/CoopCarryFollower.cs` (NEW)

Move o objeto enquanto `active`. Resolve os `Transform` dos carregadores (o local via
`PlayerController`, os remotos via `RemotePlayerSpawner`), calcula o ponto médio e aplica
`Vector3.Lerp` — **nunca** teletransporta (`.agents/rules/client-design-pattern.md`). Altura fixa
de carregamento; orientação alinhada ao vetor entre os dois.

Se um carregador remoto ainda não tiver `Transform` (entrou agora), o objeto mantém a última
posição válida em vez de saltar para a origem.

#### `Characters/PlayerController.cs` (MODIFY)

`public float SpeedMultiplier { get; set; } = 1f`, aplicado sobre caminhada e corrida. O
`CoopCarryable` seta `0.5f` enquanto o jogador local estiver na lista de carregadores e `1f` ao
sair — o *"movimentação reduzida"* do documento.

Como propriedade pública e não como campo de Inspector: quem manda no multiplicador é o estado de
rede, e o valor precisa voltar a `1f` de forma confiável. Restaurar em `OnDisable` do
`CoopCarryable` também, para o jogador não ficar lento para sempre se o objeto for destruído.

#### Catálogo e apresentação

Entrada `task-transporte-coop-01`, `type = "coop_transport"`, `targetCount = 1`,
`coop = { requiredCarriers: 2 }`,
`pairs = [{ itemId: "caixa-pesada-01", slotId: "slot-deposito-caixa" }]`.

`TaskPresentation` ganha `TYPE_COOP_TRANSPORT`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Transportar a caixa pesada" |
| `GetHowTo` | "A caixa é pesada demais para uma pessoa. Chame outro jogador, segurem juntos e levem até o depósito." |
| `GetActionPrompt` | "Aperte [E] para largar a caixa no depósito" |

#### Cena `SCN_FirstFloor.unity`

Um `CoopCarryable` + `CoopCarryFollower` numa caixa grande (`SM_Env_PlasticBox` em escala maior,
ou o sofá `SM_Env_Sofa`); um `TaskDepositPoint` (`slot-deposito-caixa`) no destino, com
`WorldTaskMarker`.

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/CoopCarryService.ts                    NEW
hora-extra-backend/src/services/CoopCarryService.test.ts               NEW (ciclos 1-7)
hora-extra-backend/src/core/factories/Service.Factory.ts               MODIFY (registra CoopCarryService)
hora-extra-backend/src/services/TaskService.ts                         MODIFY (coop, COOP_NOT_READY, incrementProgress com carriers)
hora-extra-backend/src/services/TaskService.test.ts                    MODIFY (ciclos 8-13)
hora-extra-backend/src/sockets/handlers/CoopGrab.Handler.ts            NEW
hora-extra-backend/src/sockets/handlers/CoopGrab.Handler.test.ts       NEW (ciclos 14-16)
hora-extra-backend/src/sockets/handlers/CoopRelease.Handler.ts         NEW
hora-extra-backend/src/sockets/handlers/CoopRelease.Handler.test.ts    NEW (ciclos 17-18)
hora-extra-backend/src/sockets/handlers/TaskProgress.Handler.ts        MODIFY (consulta carriers, emite N task_updated)
hora-extra-backend/src/sockets/handlers/TaskProgress.Handler.test.ts   MODIFY (ciclos 19-21)
hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts      MODIFY (coop_grab, coop_release)
hora-extra-backend/src/sockets/UdpSocketManager.ts                     MODIFY (releaseAllFor na desconexão e no reaper)
hora-extra-backend/docs/Networking/COMMUNICATION.md                    MODIFY (coop_grab, coop_release, coop_state, campo coop, COOP_NOT_READY, nota do task_updated plural)
hora-extra-backend/docs/Mechanics/COOP-CARRY.md                        NEW
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/CoopCarryable.cs       NEW
hora-extra-client/Assets/Scripts/Interactions/CoopCarryFollower.cs   NEW
hora-extra-client/Assets/Scripts/Characters/PlayerController.cs      MODIFY (SpeedMultiplier)
hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs            MODIFY (COOP_GRAB, COOP_RELEASE, COOP_STATE)
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs        MODIFY (CoopGrabPayload, CoopReleasePayload, CoopStatePayload, coop em TaskEntry)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs      MODIFY (SendCoopGrab, SendCoopRelease, OnCoopState, EnsureCoopTransportEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs              MODIFY (TYPE_COOP_TRANSPORT)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                 MODIFY (asset, Editor — caixa pesada + depósito)
hora-extra-client/Docs/Mechanics/TASK-09-COOP.md                     NEW
```

## 5. TDD breakdown (phase: backend)

### CoopCarryService (`CoopCarryService.test.ts`)

- Cycle 1: `it('grab adiciona o jogador e devolve a lista de carregadores')`.
- Cycle 2: `it('grab é idempotente — o mesmo jogador não entra duas vezes')`.
- Cycle 3: `it('release remove o jogador e mantém os demais')`.
- Cycle 4: `it('release do último carregador limpa o registro do item')`.
- Cycle 5: `it('releaseAllFor solta o jogador de todos os itens que ele segurava')` → cobre a
  desconexão, o bug mais provável deste plano.
- Cycle 6: `it('release de quem não estava segurando não lança')`.
- Cycle 7: `it('clearRoom limpa os carregadores da sala')`.

### TaskService (`TaskService.test.ts`)

- Cycle 8: `it('incrementProgress lança COOP_NOT_READY com menos carregadores que requiredCarriers')`
  → e o `itemId` **não** entra em `collectedItems`.
- Cycle 9: `it('incrementProgress aceita com carregadores suficientes')`.
- Cycle 10: `it('incrementProgress credita todos os carregadores que têm a task')`.
- Cycle 11: `it('incrementProgress ignora carregador que não tem a task, sem lançar')`.
- Cycle 12: `it('WRONG_SLOT tem precedência sobre COOP_NOT_READY')`.
- Cycle 13: `it('entrada sem coop mantém o comportamento de jogador único')` → regressão dos
  planos 0004–0008.

### Handlers

- Cycle 14: `it('CoopGrab faz broadcast de coop_state com a lista de carregadores')`.
- Cycle 15: `it('CoopGrab marca active=true ao atingir requiredCarriers')`.
- Cycle 16: `it('CoopGrab rejeita itemId ausente com ERROR genérico')`.
- Cycle 17: `it('CoopRelease faz broadcast de coop_state com active=false')`.
- Cycle 18: `it('CoopRelease de item sem carregadores não lança')`.
- Cycle 19: `it('TaskProgress consulta os carregadores e repassa ao incrementProgress')`.
- Cycle 20: `it('TaskProgress emite um task_updated por carregador creditado')`.
- Cycle 21: `it('TaskProgress com item sem carregadores emite um único task_updated')` →
  regressão explícita.

## 6. Manual verification steps (phase: client)

**Pré-condição diferente das outras tarefas: são necessários DOIS clientes.** Rodar uma build
(`File > Build And Run`) e o Editor em paralelo, ou duas builds. O bypass de dev usa o mesmo
`DEV_TEST_USER_ID` para os dois — usar **Guest Mode** (`POST /api/auth/guest`) em pelo menos um
deles para que sejam jogadores distintos, senão o servidor vê um jogador só e nada deste plano é
exercitado.

### 1. Um jogador não carrega

- Ação: jogador A pega a caixa sozinho.
- Esperado: `coop_grab` enviado; `coop_state — carriers=[A] active=false`; prompt mostra `1/2`; a
  caixa **não** se move e A anda na velocidade normal.

### 2. Segundo jogador completa o grupo

- Ação: jogador B aperta [E] na caixa.
- Esperado: `coop_state — carriers=[A,B] active=true` nos **dois** clientes; a caixa sobe para a
  posição de carregamento; os dois ficam lentos (~50%).

### 3. A caixa acompanha os dois

- Ação: A e B andam juntos.
- Esperado: a caixa segue o ponto médio, com `Lerp` — sem teleporte, sem tremer. Vista dos dois
  clientes, a caixa está **no mesmo lugar**.

### 4. Um solta no meio do caminho

- Ação: B aperta [E] para soltar.
- Esperado: `coop_state — active=false`; a caixa para (ou cai na posição atual); B volta à
  velocidade normal; A continua na lista com `1/2`.

### 5. Entrega com gente de menos

- Ação: com só A segurando, chegar no depósito e apertar [E].
- Esperado: `task_rejected — code=COOP_NOT_READY`; contador não muda; a caixa continua segura.

### 6. Entrega válida credita os dois

- Ação: os dois levam até o depósito; **A** aperta [E].
- Esperado: `task_progress` sai do cliente de A; chegam `task_updated` para A **e** para B; o
  `MissionListHud` dos **dois** marca a task como concluída. Este é o teste da decisão da §1.

### 7. Ajudante sem a task

- Ação: repetir com B **sem** a task `coop_transport` atribuída.
- Esperado: B ajuda normalmente; a entrega funciona; A recebe o progresso; B não recebe nada e
  **nenhum erro** aparece em nenhum dos dois consoles.

### 8. Desconexão solta o objeto

- Ação: com `active=true`, **fechar** o cliente de B abruptamente.
- Esperado: dentro do prazo do reaper (~30 s), `coop_state — carriers=[A] active=false` chega ao
  cliente de A; a caixa pode ser pega de novo por um terceiro. Se ficar travada em `2/2` para
  sempre, o `releaseAllFor` não está sendo chamado — é falha bloqueante.

### 9. Velocidade restaurada

- Ação: soltar a caixa de todas as formas possíveis (soltar com [E], entregar, desconectar e
  reconectar).
- Esperado: em todos os casos a velocidade volta ao normal. Andar um pouco e comparar com outro
  jogador para confirmar.

### 10. Regressão de carregamento simples

- Ação: executar uma coleta comum (plano 0005) com os dois clientes conectados.
- Esperado: comportamento idêntico ao de antes — **um** `task_updated` por entrega, nenhum
  `coop_state`.

### 11. Cleanup

- Ação: sair dos dois clientes.
- Esperado: nenhum `LogError`; nenhum `coop_state` órfão no log do servidor.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/CoopCarryService.test.ts
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/CoopGrab.Handler.test.ts
npx vitest run src/sockets/handlers/CoopRelease.Handler.test.ts
npx vitest run src/sockets/handlers/TaskProgress.Handler.test.ts
npm test
npx tsc --noEmit
```

### Client

Seguir os 11 passos da §6 em Play Mode, **com dois clientes** (ver a pré-condição).

## 8. Out of scope

- **Mais de 2 carregadores.** `requiredCarriers` é um número no catálogo e o service não assume 2,
  mas a cena e o `CoopCarryFollower` (ponto médio de dois) são pensados para 2. Para 3+, o
  follower precisa do centroide e das poses de mão — ajuste pequeno, mas não verificado aqui.
- **Física do objeto carregado.** Ele interpola entre os jogadores e atravessa paredes; não há
  colisão nem cordas/juntas. Ver a decisão na §1.
- Sincronizar a **posição** do objeto pela rede (por desenho — é derivada).
- Animação de carregar a dois, poses de mão, IK.
- Largar o objeto em qualquer lugar com física (ele fica onde estava).
- Penalidade por soltar no meio do caminho.
- Dois grupos carregando objetos diferentes ao mesmo tempo — funciona por construção (o estado é
  por `itemId`), mas não está na verificação.
- Um jogador segurar dois objetos cooperativos ao mesmo tempo.
- Reconciliação de `coop_state` perdido: é UDP e sem confirmação. Um `coop_grab` perdido exige um
  novo [E]. Aceitável porque o prompt mostra a contagem atual e o jogador percebe.
- Integração com `PlayerCarrier` (um jogador segurando um item comum **e** ajudando no
  cooperativo). Nesta entrega os dois sistemas são independentes e não se bloqueiam.
