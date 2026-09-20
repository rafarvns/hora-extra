# Plan 0009 — infra-terminal-de-computador

> **Plano-base do eixo "computador".** Origem: `TAREFAS DOS JOGADORES.pdf` — as Tarefas **2**
> (imprimir), **4** (enviar arquivo/e-mail), **5** (remover vírus) e **7** (escrever e-mail)
> acontecem todas dentro da tela de um PC; a Tarefa **3** usa o mesmo mecanismo para o cadeado
> com senha das gavetas.
>
> Depende do plano [0004](0004-infra-carregar-e-validar-itens.md) **apenas pelo canal de recusa**
> (`task_rejected` e `TaskRejectionError`): este plano acrescenta códigos à enum de lá, não
> reimplementa o canal. Se 0004 ainda não tiver sido implementado, implementar primeiro.
>
> Consumido por: [0012](0012-tarefa-02-imprimir-documentos-e-levar-ao-chefe.md),
> [0013](0013-tarefa-03-organizar-documentos.md), [0014](0014-tarefa-04-enviar-arquivos-e-emails.md),
> [0015](0015-tarefa-05-remocao-de-virus.md), [0017](0017-tarefa-07-escrever-e-enviar-email.md).
> Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O sistema de tasks tem hoje exatamente duas formas de avançar:

- **binária** — `task_start_interaction` → `task_complete_attempt` (o QTE da cafeteira, plano 0003);
- **incremental por item** — `task_progress` +1, com identidade e destino de item (plano 0004).

Cinco das dezoito tarefas não cabem em nenhuma das duas. Elas são **sequências ordenadas de
etapas heterogêneas**, e cada etapa tem uma resposta *certa*:

| Tarefa | Sequência |
| :----- | :-------- |
| 2 — Imprimir | abrir arquivos → **selecionar o arquivo correto** → clicar em imprimir |
| 4 — Enviar arquivo | **digitar a senha** → abrir o e-mail → clicar em enviar |
| 5 — Vírus | **achar o antivírus** → clicar → minigame de escaneamento |
| 7 — Escrever e-mail | **destinatário** → **assunto** → **conteúdo** → revisar → enviar |
| 3 — Gavetas | **digitar a senha do cadeado** para liberar a gaveta |

Três lacunas impedem isso hoje:

1. **Não existe etapa nomeada.** `task_progress` soma +1 cego: o servidor não sabe *qual* etapa
   o jogador cumpriu nem se ele pulou alguma. Um cliente poderia mandar 3× `task_progress` e
   "imprimir" sem nunca selecionar o arquivo certo.
2. **Não existe resposta esperada.** Nada no protocolo carrega "a senha é `1234`" ou "o arquivo
   certo é `relatorio-q3.pdf`". Sem isso, acertar e errar são indistinguíveis para o servidor —
   e a Tarefa 4 inteira é sobre *descobrir* a senha.
3. **Não existe tela diegética.** Não há nenhuma UI de mundo com a qual o jogador interaja por
   clique; todo o gameplay atual é trigger + tecla [E]. `CoffeeQTE` é a coisa mais próxima, mas é
   um overlay de minigame de uso único, não um shell reaproveitável.

Este plano entrega as três coisas — **etapa nomeada, resposta validada no servidor e shell de
tela** — de forma genérica, para que os cinco planos de tarefa acima sejam só catálogo + telas.

### Nota de autoridade (limitação conhecida, decidida de propósito)

O catálogo é **declarado pelo cliente** (`task_catalog_register`, plano 0001). Logo, o valor
esperado de cada etapa (`expects`) trafega do cliente para o servidor no registro — o cliente
"sabe a senha". Isso **não** é uma regressão: hoje o cliente já define descrição, `targetCount` e
todo o resto; a regra `.agents/rules/backend-design-pattern.md` exige que o servidor não confie
no *relato de progresso* do cliente, e isso continua valendo — o servidor compara, decide e é o
único que muda `status`.

O que este desenho impede é o que importa para o jogo: **pular etapa, repetir etapa, executar
fora de ordem e submeter valor errado**. O que ele não impede é um cliente modificado ler o
próprio catálogo. Mover o catálogo para o servidor (seed em banco ou arquivo) resolveria, é
mudança estrutural que afeta os planos 0001–0008 inteiros, e por isso está na §8 como plano
futuro em vez de embutida aqui.

## 2. Scope & target

**Target:** `both`

**Phase backend** — `TaskEntry` ganha `steps?: TaskStep[]`; `TaskService` ganha
`submitStep(playerId, taskId, stepId, value?)` e o estado `completedSteps`; `TaskRejectionCode`
ganha 4 códigos; novo handler `TaskStepSubmit.Handler.ts` registrado na `SocketHandlerFactory`;
novo evento S→C `task_step_ack`; `TaskCatalogRegisterHandler` valida o shape de `steps`;
`COMMUNICATION.md` atualizado.

**Phase client** — novo `ComputerTerminal.cs` em `Assets/Scripts/Interactions/`; novo subdomínio
`Assets/Scripts/UI/Terminal/` com `TerminalScreen.cs`, `TerminalApp.cs` (base) e
`TerminalStepButton.cs`; `TaskSystemBridge` ganha `SendStepSubmit` e `OnTaskStepAck`;
`NetworkEvents` ganha `TASK_STEP_SUBMIT`/`TASK_STEP_ACK`; `TaskModels.cs` ganha os DTOs.

Este plano **não** entrega nenhuma tela concreta (arquivos, e-mail, antivírus) nem nenhuma
entrada de catálogo — só o shell e o protocolo. A tela de sanidade da §6 é descartável.

### Contratos cross-repo

| Evento | Direção | Payload |
| :----- | :------ | :------ |
| `task_catalog_register` | C→S | **ALTERADO** — cada entrada aceita `steps?: Array<{ stepId: string, expects?: string, label?: string }>` |
| `task_step_submit` | C→S | **NOVO** — `{ taskId: string, stepId: string, value?: string }` |
| `task_step_ack` | S→C unicast | **NOVO** — `{ taskId: string, stepId: string, completedSteps: string[], nextStepId: string \| null }` |
| `task_rejected` | S→C unicast | **ALTERADO** — `code` aceita `UNKNOWN_STEP`, `STEP_OUT_OF_ORDER`, `WRONG_VALUE`, `STEP_ALREADY_DONE` |

`steps` é opcional; entradas sem ele seguem idênticas. `task_step_ack` é **unicast** (é estado de
UI do remetente); a mudança de progresso continua saindo no `task_updated` em broadcast, como
todo o resto do sistema.

## 3. Approach

### Backend

#### Tipos (`TaskService.ts`)

```ts
export interface TaskStep {
    stepId: string;    // id único dentro da task
    expects?: string;  // resposta correta; ausente = etapa sem valor (só clicar)
    label?: string;    // texto opcional para log/telemetria — não usado em lógica
}

export interface TaskEntry {
    // … campos dos planos 0001 e 0004 …
    steps?: TaskStep[];  // ordem do array É a ordem exigida
}
```

`TaskRejectionCode` ganha, **somando** aos 6 do plano 0004:

```ts
| 'UNKNOWN_STEP'       // stepId não existe no catálogo desta task
| 'STEP_OUT_OF_ORDER'  // há etapa anterior pendente
| 'WRONG_VALUE'        // value != expects (não consome a etapa — pode tentar de novo)
| 'STEP_ALREADY_DONE'  // etapa já cumprida nesta atribuição
```

#### Estado novo

```ts
private completedSteps = new Map<string, Set<string>>();  // `${playerId}:${taskId}` → stepIds
```

Mesma chave composta de `collectedItems` (plano 0004), e limpo no mesmo ponto: `clearRoom`.

#### `submitStep(playerId, taskId, stepId, value?)`

Ordem de validação — cada falha lança `TaskRejectionError`:

1. Jogador sem tasks / `taskId` não atribuído → `NOT_ASSIGNED`.
2. `status` fora de `pending`/`in_progress` → `INVALID_STATUS`.
3. Entrada de catálogo sem `steps` ou `stepId` fora dela → `UNKNOWN_STEP`.
4. `stepId` já em `completedSteps` → `STEP_ALREADY_DONE`.
5. Alguma etapa **anterior** no array ainda não cumprida → `STEP_OUT_OF_ORDER`.
6. `expects` declarado e `normalize(value) !== normalize(expects)` → `WRONG_VALUE`.
   **Não** marca a etapa como cumprida — errar a senha é retentável, e é justamente o loop de
   gameplay das Tarefas 4 e 3.
7. Marca a etapa; `pending → in_progress` na primeira; `currentProgress = completedSteps.size`;
   `completed` quando `completedSteps.size === steps.length`.

`normalize` = `String(v ?? '').trim().toLowerCase()`. Comparação insensível a caixa e a espaço
nas pontas é requisito de usabilidade: digitar a senha e o assunto do e-mail com maiúscula não
pode falhar. Documentar isso no `COMMUNICATION.md` — é contrato, não detalhe.

#### Regra: `steps` **ou** `items`/`pairs`, nunca os dois

Uma task conta progresso por **um** critério só. `incrementProgress` soma por item e
`submitStep` soma por etapa; se a mesma entrada declarasse os dois, `currentProgress` teria duas
fontes e `targetCount` não teria significado único.

`registerCatalog` rejeita a entrada que declarar `steps` junto com `items` ou `pairs`: descarta os
campos de item, mantém `steps`, e emite `logger.warn`. Mesmo critério tolerante aplicado a
catálogo malformado no plano 0004 — avisar e seguir, em vez de derrubar o registro da sala.

Consequência de desenho para os planos de tarefa: quando uma tarefa mistura *tela* e *mundo*
(a Tarefa 2 imprime no PC e **entrega em mãos**), ela é modelada inteira como `steps`, e a ação
física vira uma etapa — um ponto de interação no mundo submete `task_step_submit` em vez de
`task_progress`. O plano [0012](0012-tarefa-02-imprimir-documentos-e-levar-ao-chefe.md) introduz o
`StepInteractionPoint` que faz essa ponte, e o plano
[0024](0024-tarefa-14-bater-ponto.md) o reusa.

O inverso também vale: uma tarefa **só** de mundo, mesmo disparada por evento ou com prazo,
continua contando por `items`/`pairs` — é o caso da Tarefa 8 (plano
[0018](0018-tarefa-08-reabastecer-impressora.md)), que não abre tela nenhuma e portanto não tem
motivo para virar `steps`.

#### Coerência `targetCount` ↔ `steps.length`

No `registerCatalog`, quando `steps` existe e `targetCount !== steps.length`, **`targetCount` é
sobrescrito** por `steps.length` e um `logger.warn` é emitido. Motivo: o HUD mostra `X/Y` a partir
do `targetCount`, e uma task de 3 etapas com `targetCount: 5` nunca completaria. Corrigir no
servidor (e avisar) é mais seguro do que rejeitar o catálogo inteiro — o mesmo critério tolerante
que o plano 0004 aplica a `items`/`pairs` malformados.

#### `TaskStepSubmit.Handler.ts` (NEW)

Registrado na `SocketHandlerFactory` como `task_step_submit` — via bloco estático, nunca
`if/else` no `UdpSocketManager` (`.agents/rules/backend-factory-pattern.md`).

- Valida shape: `taskId` e `stepId` strings não-vazias; `value` string quando presente. Tipo
  errado → `ERROR` genérico (falha de protocolo, não de gameplay).
- Chama `ServiceFactory.getTaskService().submitStep(...)`.
- **Sucesso:** `sendTo(rinfo, 'task_step_ack', { taskId, stepId, completedSteps, nextStepId })` e
  `broadcastToRoom(roomId, 'task_updated', { playerId, taskId, currentProgress, status })`.
  `nextStepId` é a primeira etapa ainda não cumprida, ou `null` se acabou — é o que deixa a tela
  do cliente avançar sem lógica de ordem duplicada.
- **`TaskRejectionError`:** `sendTo(rinfo, 'task_rejected', { taskId, code, message, stepId })`.
  Mesmo canal do plano 0004, com `stepId` no lugar de `itemId`.
- Qualquer outro erro → `ERROR` genérico.
- Logging com `{ module: 'UDP_SOCKET' }`, e **nunca logar `value`** — é senha.

### Client

#### `Interactions/ComputerTerminal.cs` (NEW)

O objeto de mundo. Trigger + [E] via `InteractionInput` do plano 0004 (não duplicar o
`#if ENABLE_INPUT_SYSTEM`).

- `[SerializeField] private string _terminalId` — qual PC é este; a tela consulta para saber se
  a task sorteada é dele.
- `[SerializeField] private TerminalScreen _screen`.
- `[SerializeField] private string _promptMessage` — ex.: `"Aperte [E] para usar o computador"`.
- Gate do prompt: jogador no trigger **e** existe task `pending`/`in_progress` cujo tipo este
  terminal atende (`TaskSystemBridge.Instance.FindMyTask(...)`).
- Ao abrir: `SendStartInteraction(task.Id)` (reusa o `pending → in_progress` que já existe — não
  inventar transição nova) e `_screen.Open(task, _terminalId)`.

#### `UI/Terminal/TerminalScreen.cs` (NEW)

Shell da tela. Um `Canvas` com pilha de `TerminalApp`.

- `Open(AssignedTask task, string terminalId)` / `Close()`.
- **Controle de input** — o ponto que mais dá errado se ficar implícito:
  - guarda o `CursorLockMode` anterior, faz `Cursor.lockState = None` + `visible = true`;
  - desabilita o `PlayerController` (que hoje trava o cursor no `Awake` e faz look no `Update`),
    guardando o estado anterior para restaurar;
  - `Close()` restaura os dois. `Close()` tem que ser chamado também em `OnDisable` — sair do
    Play Mode com a tela aberta não pode deixar o cursor preso.
- `ESC` fecha. Fechar **não** cancela a task: as etapas já cumpridas estão no servidor, e o
  jogador volta de onde parou. Isso é o que torna a Tarefa 4 jogável (sair, achar o post-it,
  voltar).
- `public static event Action<bool> OnTerminalOpenChanged` — Observer para HUD e para qualquer
  script que precise se silenciar enquanto a tela está aberta.

#### `UI/Terminal/TerminalApp.cs` (NEW, abstrata)

Base de toda tela concreta dos planos 0012–0017. Recebe `AssignedTask` e o `TerminalScreen` dono;
expõe `Submit(string stepId, string value = null)`, que delega ao bridge; reage a
`OnTaskStepAck` (avançar) e `OnTaskRejected` (mostrar erro na própria tela). Cada plano de tarefa
herda em vez de falar com o `SocketManager` direto.

#### `UI/Terminal/TerminalStepButton.cs` (NEW)

Cola de Inspector: um `Button` + `_stepId` + `_value` opcional → chama `Submit`. Cobre sozinho
todas as etapas de clique puro ("imprimir", "enviar", "abrir antivírus"), sem script por tarefa.

#### `Characters/TaskSystemBridge.cs` (MODIFY)

- `public void SendStepSubmit(string taskId, string stepId, string value = null)`.
- Assina `TASK_STEP_ACK` em `OnEnable`, remove em `OnDisable`
  (`.agents/rules/client-design-pattern.md`).
- `public static event Action<TaskStepAckPayload> OnTaskStepAck`.
- `TaskEntryData` ganha `public List<TaskStepData> steps` (`[Serializable]`, para aparecer no
  Inspector) e `RegisterCatalog()` copia para o `TaskEntry` enviado.

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/TaskService.ts                             MODIFY (TaskStep, steps, completedSteps, submitStep, 4 códigos novos, clamp de targetCount)
hora-extra-backend/src/services/TaskService.test.ts                        MODIFY (ciclos 1-11)
hora-extra-backend/src/sockets/handlers/TaskStepSubmit.Handler.ts          NEW
hora-extra-backend/src/sockets/handlers/TaskStepSubmit.Handler.test.ts     NEW (ciclos 12-18)
hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts          MODIFY (registra task_step_submit)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.Handler.ts   MODIFY (valida shape de steps)
hora-extra-backend/src/sockets/handlers/TaskCatalogRegisterHandler.test.ts      MODIFY (ciclos 19-20)
hora-extra-backend/docs/Networking/COMMUNICATION.md                        MODIFY (task_step_submit, task_step_ack, steps, códigos novos, regra de normalize)
hora-extra-backend/docs/Mechanics/TASK-STEPS.md                            NEW (docs-files.md: feature nova documenta em docs/<Categoria>/)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/ComputerTerminal.cs          NEW
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalScreen.cs             NEW
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalApp.cs                NEW (abstrata)
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalStepButton.cs         NEW
hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs                  MODIFY (TASK_STEP_SUBMIT, TASK_STEP_ACK)
hora-extra-client/Assets/Scripts/Network/Models/TaskModels.cs              MODIFY (TaskStep, steps em TaskEntry, TaskStepSubmitPayload, TaskStepAckPayload)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs            MODIFY (SendStepSubmit, OnTaskStepAck, TaskEntryData.steps)
hora-extra-client/Assets/Prefab/PFB_UI_TerminalScreen.prefab               NEW (asset, Editor — canvas do shell; prefixo PFB_ por unity-asset-management.md)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                       MODIFY (asset, Editor — 1 ComputerTerminal de sanidade)
hora-extra-client/Docs/Mechanics/TERMINAL.md                               NEW
```

## 5. TDD breakdown (phase: backend)

### TaskService (`TaskService.test.ts`)

- Cycle 1: `it('submitStep marca a primeira etapa e faz pending → in_progress')`.
- Cycle 2: `it('submitStep soma currentProgress = nº de etapas cumpridas')`.
- Cycle 3: `it('submitStep completa a task ao cumprir a última etapa')` → `status='completed'`.
- Cycle 4: `it('submitStep lança UNKNOWN_STEP quando o catálogo não declara steps')`.
- Cycle 5: `it('submitStep lança UNKNOWN_STEP quando o stepId não existe na task')`.
- Cycle 6: `it('submitStep lança STEP_OUT_OF_ORDER quando há etapa anterior pendente')` →
  `currentProgress` inalterado.
- Cycle 7: `it('submitStep lança STEP_ALREADY_DONE ao repetir a mesma etapa')`.
- Cycle 8: `it('submitStep lança WRONG_VALUE quando value != expects e NÃO marca a etapa')` →
  a mesma etapa aceita o valor certo na chamada seguinte.
- Cycle 9: `it('submitStep normaliza caixa e espaços ao comparar com expects')` →
  `'  SenhA123 '` casa com `'senha123'`.
- Cycle 10: `it('registerCatalog sobrescreve targetCount divergente por steps.length com warn')`.
- Cycle 11: `it('clearRoom limpa completedSteps — a mesma etapa volta a ser aceita')`.
- Cycle 12: `it('registerCatalog descarta items/pairs quando a entrada também declara steps, com warn')`
  → e a entrada segue registrada com `steps` intactos.

### TaskStepSubmitHandler (`TaskStepSubmit.Handler.test.ts`)

- Cycle 13: `it('repassa taskId, stepId e value para submitStep')`.
- Cycle 14: `it('rejeita stepId ausente ou não-string com ERROR genérico')`.
- Cycle 15: `it('responde task_step_ack unicast com completedSteps e nextStepId')`.
- Cycle 16: `it('nextStepId é null quando todas as etapas foram cumpridas')`.
- Cycle 17: `it('faz broadcastToRoom task_updated em sucesso')` → e o payload do broadcast **não**
  contém `value`.
- Cycle 18: `it('responde task_rejected com code e stepId quando o service lança TaskRejectionError')`
  → `broadcastToRoom` não chamado.
- Cycle 19: `it('não escreve value em nenhum log')` → spy no `logger`; garante que senha não vaza.

### TaskCatalogRegisterHandler (`TaskCatalogRegisterHandler.test.ts`)

- Cycle 20: `it('repassa steps ao registerCatalog quando presente')`.
- Cycle 21: `it('descarta steps malformado (entrada sem stepId) com warn e registra o resto')`.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev`), `SCN_FirstFloor.unity` aberta,
`SocketManager.UseTestToken = true`, Console com "Clear on Play".

**Cena de sanidade:** um `ComputerTerminal` (`_terminalId = "pc-sanity"`) com um `TerminalScreen`
contendo três `TerminalStepButton` — `s1` (sem `expects`), `s2` (`expects = "1234"`, via campo de
texto) e `s3` (sem `expects`) — e a entrada de catálogo `task-sanity-steps` com
`steps = [s1, s2(expects "1234"), s3]`, `type = "terminal_sanity"`. Descartar depois.

### 1. Catálogo com steps

- Ação: entrar em Play Mode.
- Esperado: `[NETWORK] task_catalog_register enviado`; backend sem warn de shape de `steps` e sem
  warn de `targetCount` divergente (deve ser 3).

### 2. Abrir a tela

- Ação: chegar no PC e pressionar [E].
- Esperado: prompt aparece só com a task atribuída; a tela abre; **o cursor aparece e destrava**;
  mover o mouse **não** gira a câmera; WASD não move o personagem.

### 3. Etapa fora de ordem

- Ação: clicar direto no botão `s3`.
- Esperado: `[NETWORK] task_rejected — code=STEP_OUT_OF_ORDER stepId=s3`; mensagem do servidor na
  própria tela; HUD de missão **não** muda.

### 4. Etapa válida

- Ação: clicar em `s1`.
- Esperado, nesta ordem: `[NETWORK] task_step_submit enviado — taskId=… stepId=s1` →
  `[GAMEPLAY] task_step_ack — completedSteps=[s1] nextStepId=s2` → `task_updated … progress=1`.
  A tela só avança **depois** do ack.

### 5. Repetir etapa

- Ação: clicar em `s1` de novo.
- Esperado: `task_rejected — code=STEP_ALREADY_DONE`; contador continua `1/3`.

### 6. Valor errado é retentável

- Ação: em `s2`, digitar `9999` e confirmar.
- Esperado: `task_rejected — code=WRONG_VALUE`; contador continua `1/3`; o campo continua
  disponível. Em seguida digitar `1234` → ack normal, `2/3`.

### 7. Senha não vaza no log

- Ação: procurar `1234` e `9999` no Console do Unity e no log do backend (`logs/`).
- Esperado: **nenhuma** ocorrência vinda do fluxo de etapa. Se aparecer, é falha — corrigir antes
  de seguir para o plano 0014.

### 8. Sair e voltar preserva progresso

- Ação: pressionar ESC, andar pela sala, voltar e pressionar [E].
- Esperado: cursor volta a travar ao fechar e destrava ao reabrir; a tela reabre já em `s3`
  (`nextStepId` do último ack); contador segue `2/3`.

### 9. Concluir

- Ação: clicar em `s3`.
- Esperado: `task_updated … status=completed progress=3`; `nextStepId=null`; a tela pode fechar
  sozinha; prompt do PC deixa de aparecer.

### 10. Regressão dos fluxos antigos

- Ação: executar o QTE da cafeteira (plano 0003) e uma coleta com `itemId` (plano 0004).
- Esperado: comportamento idêntico ao de antes — nenhum `task_step_ack`, nenhum `task_rejected`.

### 11. Cleanup

- Ação: sair do Play Mode **com a tela aberta**.
- Esperado: nenhum `LogError`; ao voltar para o Editor o cursor está normal. Este passo existe
  porque o bug clássico aqui é o `Cursor.lockState` ficar preso.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/TaskService.test.ts
npx vitest run src/sockets/handlers/TaskStepSubmit.Handler.test.ts
npx vitest run src/sockets/handlers/TaskCatalogRegisterHandler.test.ts
npm test
npx tsc --noEmit
```

### Client

Seguir os 11 passos da §6 em Play Mode.

## 8. Out of scope

- **Qualquer tela concreta** (navegador de arquivos, cliente de e-mail, antivírus) e suas entradas
  de catálogo — planos 0012, 0013, 0014, 0015 e 0017. Aqui só o shell.
- **Mover o catálogo para o servidor** para que `expects` deixe de ser conhecido pelo cliente (ver
  "Nota de autoridade" na §1). É plano próprio e mexe em 0001–0008.
- Ramificação/condicional entre etapas (etapa A leva a B *ou* C). `steps` é uma lista linear; se
  uma tarefa precisar de árvore, é extensão de protocolo em outro plano.
- Etapa com múltiplas respostas aceitas (`expects` é um valor só).
- Limite de tentativas ou cooldown em `WRONG_VALUE` — errar é ilimitado e sem custo nesta entrega.
  Penalidade por erro é assunto do plano [0010](0010-infra-agenda-notificacoes-e-penalidades.md).
- Validação de proximidade: o servidor não confere se o jogador está de fato na frente do PC (a
  mesma limitação já assumida no plano 0004).
- Outro jogador ver a tela ligada/desligada, ou dois jogadores no mesmo PC.
- Minigames dentro da tela (o escaneamento da Tarefa 5) — plano 0015, que reusa o par
  `task_start_interaction`/`task_complete_attempt` do plano 0003 em vez de `steps`.
- Teclado virtual/diegético: a digitação usa `InputField` do uGUI comum.
