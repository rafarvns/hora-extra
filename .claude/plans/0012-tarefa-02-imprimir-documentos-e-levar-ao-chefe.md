# Plan 0012 — tarefa-02-imprimir-documentos-e-levar-ao-chefe

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 2 — Imprimir documentos e levar ao chefe**.
> **Depende de [0009](0009-infra-terminal-de-computador.md)** (etapas validadas + shell de tela) e
> de [0004](0004-infra-carregar-e-validar-itens.md) (carregar o item impresso).
> Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento descreve seis passos, nesta ordem:

> vai ao computador → seleciona o arquivo correto → clica em "imprimir" → vai até a impressora →
> coleta as folhas impressas → leva até a sala do chefe

É a primeira tarefa do projeto que **atravessa tela e mundo**: começa numa UI e termina com um
objeto na mão, em outra sala. Duas decisões saem disso.

**Decisão 1 — a tarefa inteira é modelada como `steps`.** Pela regra do plano 0009, uma entrada de
catálogo conta progresso por `steps` **ou** por `items`/`pairs`, nunca pelos dois. Como os três
primeiros passos só existem como etapa de tela, o resto se alinha a eles: coletar e entregar viram
etapas também. Alternativa descartada: partir em duas tasks (imprimir / entregar), que perderia a
ordem entre elas — o jogador poderia "entregar" antes de imprimir.

**Decisão 2 — as folhas impressas não existem antes de existirem.** O objeto que o jogador carrega
é *criado pela ação anterior*. Ele fica na cena desativado e só é ligado quando o servidor
confirma o passo `imprimir`. Se estivesse ligado desde o começo, o jogador pegaria as folhas sem
nunca usar o PC, e a ordem das etapas viraria decoração.

A ponte "ação física → etapa" não existe no plano 0009, que só entrega o shell de tela. Ela nasce
aqui como `StepInteractionPoint` e é reusada pelo plano 0024 (bater ponto).

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. As cinco etapas são **dados de catálogo** sobre o
`submitStep` do plano 0009. O backend só recebe documentação.

**Phase client** — `StepInteractionPoint.cs` (novo, genérico); `TerminalFileBrowser.cs` (tela de
arquivos, herda de `TerminalApp`); `PrintedDocumentSpawner.cs`; entrada de catálogo no
`TaskSystemBridge`; textos em `TaskPresentation`; setup de cena (PC, impressora, mesa do chefe).

### Contratos cross-repo

Nenhum evento novo. A entrada abaixo passa a ser enviada no `task_catalog_register` usando o campo
`steps` do plano 0009:

| taskId | type | steps (em ordem) |
| :----- | :--- | :--------------- |
| `task-imprimir-chefe-01` | `print_and_deliver` | `abrir-arquivos` · `selecionar-arquivo` (`expects: "relatorio-trimestral"`) · `imprimir` · `coletar-folhas` · `entregar-chefe` |

`targetCount` = 5, igual a `steps.length` (o servidor corrige e avisa se divergir — plano 0009).

## 3. Approach

### Backend

Só documentação: registrar em `COMMUNICATION.md` esta entrada como **exemplo canônico de task
mista tela+mundo**, e acrescentar `print_and_deliver` à lista de valores conhecidos de `type`.

O valor esperado do passo `selecionar-arquivo` mora no catálogo — com a limitação já documentada
na "Nota de autoridade" do plano 0009. O que o servidor garante aqui é o que importa para o jogo:
não dá para clicar em "imprimir" sem ter selecionado o arquivo certo antes
(`STEP_OUT_OF_ORDER`), nem para entregar sem ter coletado.

### Client

#### `Interactions/StepInteractionPoint.cs` (NEW, genérico)

Trigger + [E] que submete **uma etapa** em vez de progresso de item. É o irmão de mundo do
`TerminalStepButton` (que faz o mesmo por clique de UI).

- `[SerializeField] private string _stepId`, `_taskType`, `_promptMessage`;
- `[SerializeField] private bool _requiresCarriedItem` e `_requiredItemId` — para o passo
  `entregar-chefe`, que só vale com as folhas na mão;
- `[SerializeField] private bool _consumeCarriedOnAck` — tira o item da mão **depois** do ack;
- gate do prompt: jogador no trigger **e** task do tipo ativa **e** (se exigido) carregando o item
  certo. Não checa ordem de etapa — quem decide ordem é o servidor;
- ao pressionar [E]: `SendStepSubmit(taskId, _stepId)`; em `OnTaskStepAck` do par (task, step),
  consome o item se configurado; em `OnTaskRejected`, exibe `payload.Message` no prompt.

Reusado pelo plano 0024 sem alteração — por isso nenhum campo específico de impressão.

#### `UI/Terminal/TerminalFileBrowser.cs` (NEW)

Herda de `TerminalApp` (plano 0009). Lista de arquivos falsos com um `Button` por linha; cada
botão submete `selecionar-arquivo` com `value = fileId`. Um botão "Imprimir" submete `imprimir`.

- Os nomes dos arquivos errados são só decoração de UI — o servidor rejeita qualquer um deles com
  `WRONG_VALUE`, e **errar é retentável** (plano 0009): o jogador tenta o próximo. É o que torna
  "selecionar o arquivo **correto**" uma decisão de verdade.
- O botão "Imprimir" fica visível desde o começo. Clicar antes de selecionar dá
  `STEP_OUT_OF_ORDER`, com a mensagem do servidor na tela — feedback melhor do que um botão
  desabilitado que não explica nada.
- A lista de arquivos vem de um `[SerializeField] private List<FileRow>` no Inspector, **não** do
  catálogo: o catálogo carrega só o `expects`, e os rótulos são conteúdo de UI.

#### `Interactions/PrintedDocumentSpawner.cs` (NEW)

Na impressora. Guarda uma referência a um `CarryableItem` **desativado** na cena
(`_itemId = "folhas-impressas-01"`, `_kind = "documento"`). Assina `OnTaskStepAck`; quando chega
`stepId == "imprimir"` da task deste tipo, ativa o objeto na bandeja de saída e dispara o
feedback (som/animação ficam fora — §8).

Ativar na confirmação do servidor, e não no clique, é a mesma regra autoritativa do plano 0004: o
mundo só muda depois que o servidor contabilizou.

#### Catálogo e apresentação

`TaskSystemBridge.EnsurePrintDeliverEntry()`, no mesmo padrão dos `Ensure*` existentes (só
adiciona se o `_initialCatalog` do Inspector ainda não tiver a entrada).

`TaskPresentation` ganha `TYPE_PRINT_AND_DELIVER = "print_and_deliver"` e os ramos:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Imprimir documentos para o chefe" |
| `GetHowTo` | "Use o computador para achar e imprimir o relatório, pegue as folhas na impressora e leve até a sala do chefe." |
| `GetActionPrompt` | varia por etapa pendente — ver abaixo |

Como o prompt depende da **etapa**, e não só da task, `TaskPresentation` ganha um overload
`GetActionPrompt(AssignedTask task, string nextStepId)`. Mapa: `abrir-arquivos` →
"Aperte [E] para usar o computador"; `coletar-folhas` → "Aperte [E] para pegar as folhas
impressas"; `entregar-chefe` → "Aperte [E] para entregar os documentos". O `nextStepId` vem do
último `task_step_ack` — o cliente não deduz ordem sozinho.

#### Cena `SCN_FirstFloor.unity`

| GameObject | Componente | Configuração |
| :--------- | :--------- | :----------- |
| PC do escritório | `ComputerTerminal` + `TerminalFileBrowser` | `_terminalId = "pc-escritorio"`, `_taskType = "print_and_deliver"` |
| Impressora (`SM_Env_Printer`) | `PrintedDocumentSpawner` + `StepInteractionPoint` | step `coletar-folhas`, `_consumeCarriedOnAck = false` |
| Folhas impressas | `CarryableItem` **desativado** | `folhas-impressas-01`, kind `documento` |
| Mesa do chefe (`SM_Env_BossDesk`) | `StepInteractionPoint` + `WorldTaskMarker` | step `entregar-chefe`, `_requiresCarriedItem = true`, `_requiredItemId = "folhas-impressas-01"`, `_consumeCarriedOnAck = true` |

O passo `coletar-folhas` é submetido pelo `StepInteractionPoint` da impressora **e** o pickup do
`CarryableItem` acontece no mesmo [E]. Para não disputar o input, o `CarryableItem` das folhas
entra com pickup desabilitado e é o `StepInteractionPoint` que chama `PlayerCarrier.TryPickup`
no ack. Um único dono do [E] por objeto — sem isso, os dois componentes reagem ao mesmo frame.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 2 + type print_and_deliver)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/StepInteractionPoint.cs        NEW (genérico; reusado pelo plano 0024)
hora-extra-client/Assets/Scripts/Interactions/PrintedDocumentSpawner.cs      NEW
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalFileBrowser.cs         NEW
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs             MODIFY (EnsurePrintDeliverEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs                     MODIFY (TYPE_PRINT_AND_DELIVER + overload GetActionPrompt(task, nextStepId))
hora-extra-client/Assets/Prefab/PFB_UI_FileBrowser.prefab                   NEW (asset, Editor)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                        MODIFY (asset, Editor — PC, impressora, folhas, mesa do chefe)
hora-extra-client/Docs/Mechanics/TASK-02-IMPRIMIR.md                        NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo.** Esta tarefa não adiciona lógica de servidor — é catálogo sobre o
`submitStep` já coberto pelos ciclos do plano 0009.

```bash
cd hora-extra-backend && npm test
```

Se aparecer necessidade de lógica nova no servidor durante a implementação, **parar** e abrir
ciclo TDD (`.agents/rules/backend-unit-tests.md` não abre exceção para "é só dado").

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev`), `SCN_FirstFloor.unity` aberta,
`UseTestToken = true`, "Clear on Play". Como o servidor sorteia 3 de N, pode ser preciso reiniciar
algumas vezes — ou esvaziar temporariamente o `_initialCatalog` deixando só esta entrada.

### 1. Estado inicial

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: `MissionListHud` mostra o how-to da §3 e `0/5`. As folhas impressas **não** estão
  visíveis na impressora. O prompt da impressora não aparece.

### 2. Ordem é obrigatória

- Ação: ir direto à mesa do chefe e pressionar [E].
- Esperado: o prompt nem aparece (`_requiresCarriedItem`). Forçando o envio pelo Inspector:
  `task_rejected — code=STEP_OUT_OF_ORDER`; contador continua `0/5`.

### 3. Abrir o PC

- Ação: [E] no computador.
- Esperado: `task_step_submit … stepId=abrir-arquivos` → `task_step_ack … nextStepId=selecionar-arquivo`
  → `1/5`. Cursor destrava, câmera e movimento travam (plano 0009).

### 4. Arquivo errado é retentável

- Ação: clicar num arquivo errado.
- Esperado: `task_rejected — code=WRONG_VALUE`; mensagem na tela; contador continua `1/5`; a
  lista continua clicável. Em seguida clicar no certo → ack, `2/5`.

### 5. Imprimir fora de ordem

- Ação: reiniciar e clicar em "Imprimir" logo após `abrir-arquivos`.
- Esperado: `task_rejected — code=STEP_OUT_OF_ORDER` com a mensagem do servidor na tela.

### 6. Imprimir materializa as folhas

- Ação: com o arquivo certo selecionado, clicar em "Imprimir".
- Esperado, nesta ordem: `task_step_ack … stepId=imprimir` → **só então** as folhas aparecem na
  bandeja da impressora → `3/5`. Se aparecerem antes do ack, o fluxo autoritativo está invertido —
  é falha.

### 7. Coletar

- Ação: fechar a tela (ESC), ir até a impressora e pressionar [E].
- Esperado: `stepId=coletar-folhas` → ack → `4/5`; as folhas vão para a mão do personagem; o
  prompt da impressora some.

### 8. Entregar

- Ação: levar até a mesa do chefe e pressionar [E].
- Esperado: `stepId=entregar-chefe` → ack com `nextStepId=null` → `task_updated … status=completed
  progress=5`; **só então** as folhas somem da mão; `WorldTaskMarker` da mesa some.

### 9. Sair no meio não perde progresso

- Ação: reiniciar, cumprir até `imprimir`, sair da tela, andar pelo mapa, voltar ao PC.
- Esperado: a tela reabre já no estado certo; contador preservado; nenhum passo repetido é aceito
  (`STEP_ALREADY_DONE`).

### 10. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; cursor normal; nenhuma folha órfã na mão ou na bandeja.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo nesta tarefa
npx tsc --noEmit
```

### Client

Seguir os 10 passos da §6 em Play Mode.

## 8. Out of scope

- Animação/som de impressão, de pegar e de entregar; partícula de papel saindo.
- O chefe como NPC que reage à entrega (diálogo, animação). A entrega é num ponto da mesa; NPC de
  chefe é assunto da Tarefa 10 (plano 0020) e mesmo lá é só zona.
- Conteúdo legível do documento impresso (textura com o relatório).
- Fila de impressão, impressora ocupada, ou disputa entre dois jogadores pelo mesmo PC.
- Impressora sem papel/tinta bloqueando esta tarefa — é a Tarefa 8, plano 0018. As duas usam a
  mesma impressora de cena, mas **não** se encadeiam nesta entrega.
- Outro jogador ver as folhas na mão do colega (limitação geral do plano 0004).
- Validação de proximidade pelo servidor (mesma limitação dos planos 0004 e 0009).
- Mais de um arquivo correto ou mais de um destinatário.
