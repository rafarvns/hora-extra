# Plan 0018 — tarefa-08-reabastecer-impressora

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 8 — Reabastecer impressora**.
> **Depende de [0004](0004-infra-carregar-e-validar-itens.md)** (carregar/entregar + `pairs`) e de
> [0010](0010-infra-agenda-notificacoes-e-penalidades.md) (evento de mundo — a impressora acaba o
> suprimento *durante o expediente*, não no começo da partida).
> Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> a impressora fica sem papel ou sem tinta **durante o expediente** → o jogador deve localizar os
> suprimentos necessários → após reabastecer, a impressora volta a funcionar

O verbo de gameplay é o mesmo do plano 0004: pegar dois suprimentos no almoxarifado e levar até a
impressora. `pairs` cobre a validação inteira (`resma → entrada de papel`, `toner → entrada de
toner`), e `ALREADY_COUNTED` impede recontagem. **Nada de protocolo novo.**

O que distingue esta tarefa é o **"durante o expediente"**: ela não deveria simplesmente estar na
lista desde o primeiro segundo. É a primeira tarefa do projeto disparada por um **evento de
mundo** — e por isso ela é a validação mais barata do `world_event` do plano 0010, antes que a
reunião (0020) e o vazamento (0023) construam coisas mais complicadas em cima.

### Decisão: o evento muda o estado, não atribui a tarefa

Duas formas de usar o `world_event` aqui:

- **(a)** o evento *atribui* a task ao jogador;
- **(b)** a task entra no sorteio normal (3 de N) e o evento só liga o estado "sem suprimento" da
  impressora no mundo.

Este plano adota **(b)**. Atribuição fora do sorteio é o caminho de *penalidade*
(`assignPenaltyTask`, plano 0010), e usá-lo para conteúdo normal misturaria as duas coisas — um
jogador receberia 4 tarefas sem ter errado nada. Com (b), o sorteio segue sendo a única porta de
entrada de tarefa normal, e o evento faz o que evento faz: muda o mundo.

Consequência honesta: um jogador pode receber a task antes do evento disparar. Por isso o
`schedule` do catálogo coloca o evento **cedo** (~20 s após o registro), e o estado visual da
impressora é ligado também na atribuição da task, o que vier primeiro. O evento agrega a
notificação e o efeito para quem *não* tem a task.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. Catálogo sobre o `pairs` do plano 0004 e o `schedule` do
plano 0010; o backend só recebe documentação.

**Phase client** — `PrinterSupplyState.cs`; entrada de catálogo; textos em `TaskPresentation`;
setup de cena (impressora + suprimentos no almoxarifado).

### Contratos cross-repo

Nenhum evento novo. A entrada usa campos já documentados pelos planos 0004 e 0010:

| taskId | type | targetCount | pairs | schedule |
| :----- | :--- | :---------- | :---- | :------- |
| `task-reabastecer-impressora-01` | `refill_printer` | 2 | `resma-papel-01 → slot-impressora-papel`, `toner-01 → slot-impressora-toner` | `[{ atSeconds: 20 }]` → `world_event` `type: "printer_out_of_supplies"` |

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md` como **primeiro exemplo de catálogo
que combina `pairs` com `schedule`**, e acrescentar `refill_printer` aos valores conhecidos de
`type` e `printer_out_of_supplies` aos tipos conhecidos de `world_event`.

Manter a lista de tipos de `world_event` no mesmo documento é o que evita dois planos escolherem
a mesma string para coisas diferentes — a regra `.agents/rules/communication-sync-rule.md` cobre
exatamente este caso.

### Client

#### `Interactions/PrinterSupplyState.cs` (NEW)

No mesmo GameObject da impressora (`SM_Env_Printer`), ao lado dos dois `TaskDepositPoint`.

- Estados: **ok** (padrão) e **sem suprimento** (luz vermelha, display de erro, bandeja aberta).
- Liga o estado "sem suprimento" quando **qualquer** dos dois acontecer: `world_event` com
  `type == "printer_out_of_supplies"` (via `WorldEventRouter`, plano 0010) **ou** `task_assigned`
  contendo uma task `refill_printer`. Ver a decisão na §1.
- Assina `OnTaskUpdated`; volta para **ok** quando a task completa (`status == "completed"`) — o
  *"volta a funcionar"* do documento.
- Progresso parcial (`1/2`) mantém o estado "sem suprimento": meia impressora não imprime.

#### Catálogo e apresentação

`TaskSystemBridge.EnsureRefillPrinterEntry()`, no padrão dos `Ensure*` existentes, incluindo o
`schedule` (campo novo de `TaskEntryData` no plano 0010).

`TaskPresentation` ganha `TYPE_REFILL_PRINTER`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Reabastecer a impressora" |
| `GetHowTo` | "A impressora ficou sem papel e sem toner. Pegue uma resma e um toner no almoxarifado e coloque cada um no lugar certo da impressora." |
| `GetActionPrompt` | "Aperte [E] para abastecer (X/2)" |

A notificação do `world_event` no `PhoneNotificationHud` (plano 0010): *"A impressora do
escritório ficou sem suprimentos."*

#### Cena `SCN_FirstFloor.unity`

| GameObject | Componentes | Configuração |
| :--------- | :---------- | :----------- |
| Resma de papel (almoxarifado) | `CarryableItem` | `resma-papel-01`, `_kind = "papel-resma"` |
| Toner (`SM_Env_TonerCartridge`) | `CarryableItem` | `toner-01`, `_kind = "toner"` |
| Impressora — entrada de papel | `TaskDepositPoint` + `WorldTaskMarker` | `slot-impressora-papel`, `_acceptedKind = "papel-resma"` |
| Impressora — entrada de toner | `TaskDepositPoint` | `slot-impressora-toner`, `_acceptedKind = "toner"` |
| Impressora (raiz) | `PrinterSupplyState` | — |

Dois `TaskDepositPoint` no mesmo objeto, com triggers **separados** e posicionados nas bocas
corretas (bandeja embaixo, toner na frente). Se os dois triggers se sobrepuserem, o `_kind` ainda
desambigua qual aceita o item na mão — mas o prompt dos dois aparece junto, e aí a leitura fica
confusa. Separar fisicamente é parte da tarefa de cena, não detalhe.

O `_kind` do papel é `papel-resma`, **não** `papel`: o `papel` já é usado pelas bolinhas e folhas
soltas da Tarefa 16 (plano 0005) e da Tarefa 18 (plano 0006). Dois itens com o mesmo `_kind` fazem
o recipiente errado aceitar o item errado — o servidor rejeitaria com `WRONG_SLOT`, mas o prompt
apareceria e o jogador levaria a culpa por um erro de autoria.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 8 + type refill_printer + world_event printer_out_of_supplies)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/PrinterSupplyState.cs   NEW
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs       MODIFY (EnsureRefillPrinterEntry com pairs + schedule)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs               MODIFY (TYPE_REFILL_PRINTER)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                  MODIFY (asset, Editor — impressora com 2 slots + resma e toner no almoxarifado)
hora-extra-client/Docs/Mechanics/TASK-08-IMPRESSORA.md                NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo.** Esta tarefa não adiciona lógica de servidor — é catálogo sobre o
`pairs` do plano 0004 e o `schedule` do plano 0010, ambos já cobertos pelos ciclos de lá.

```bash
cd hora-extra-backend && npm test
```

Se aparecer necessidade de lógica nova no servidor, **parar** e abrir ciclo TDD.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. Impressora começa funcionando

- Ação: entrar em Play Mode **sem** a task sorteada e olhar a impressora nos primeiros segundos.
- Esperado: estado **ok** — sem luz vermelha, sem display de erro.

### 2. Evento de mundo dispara

- Ação: esperar ~20 s.
- Esperado: `[NETWORK] world_event — type=printer_out_of_supplies`, **uma vez**; notificação no
  `PhoneNotificationHud`; a impressora passa para "sem suprimento" — inclusive para quem não tem
  a task.

### 3. Task atribuída também liga o estado

- Ação: reiniciar com a task sorteada e olhar a impressora **antes** dos 20 s.
- Esperado: já está em "sem suprimento" (decisão da §1). Nenhum `LogError` por ligar duas vezes
  quando o evento chegar depois.

### 4. Estado inicial da missão

- Ação: olhar o `MissionListHud`.
- Esperado: how-to da §3 e `0/2`; `WorldTaskMarker` na impressora.

### 5. Item errado não entra

- Ação: pegar o toner e chegar na entrada de **papel**.
- Esperado: prompt não aparece (`_acceptedKind` diferente). Forçando o envio:
  `task_rejected — code=WRONG_SLOT`; o toner volta para a mão.

### 6. Prompts não se atropelam

- Ação: carregando a resma, ficar entre as duas bocas da impressora.
- Esperado: aparece **um** prompt só — o da entrada de papel. Se os dois aparecerem, separar os
  triggers (§3).

### 7. Abastecer papel

- Ação: [E] na entrada de papel com a resma.
- Esperado, nesta ordem: `task_progress … itemId=resma-papel-01 slotId=slot-impressora-papel` →
  `task_updated … 1/2` → **só então** a resma some da mão. A impressora **continua** em "sem
  suprimento".

### 8. Abastecer toner e voltar a funcionar

- Ação: buscar o toner e colocar.
- Esperado: `task_updated … status=completed progress=2`; **só então** a impressora volta ao
  estado ok (luz apaga, display normaliza); marcador some.

### 9. Suprimento repetido

- Ação: reativar a resma pelo Inspector e tentar de novo.
- Esperado: `task_rejected — code=ALREADY_COUNTED`; volta para a mão; contador inalterado.

### 10. Não conflita com a Tarefa 2

- Ação: se o plano 0012 já estiver implementado, rodar a Tarefa 2 (imprimir) com a impressora em
  "sem suprimento".
- Esperado: a Tarefa 2 funciona normalmente — as duas **não** se encadeiam nesta entrega (§8).
  Nenhum `LogError` por dois scripts na mesma impressora.

### 11. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; nenhum suprimento preso no `_handAnchor`.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo nesta tarefa
npx tsc --noEmit
```

### Client

Seguir os 11 passos da §6 em Play Mode.

## 8. Out of scope

- **Encadear com a Tarefa 2** (impressora sem suprimento bloqueando a impressão do plano 0012).
  Seria dependência entre tarefas, que o sistema de catálogo não modela — cada task é independente
  e sorteável isoladamente. Se for pedido, é plano próprio com o conceito de pré-requisito.
- Escolher *qual* suprimento acabou (só papel, só tinta). Aqui acabam os dois, sempre.
- Consumo real de papel a cada impressão.
- Estoque finito no almoxarifado, ou suprimento que precise ser comprado/pedido.
- Atolamento de papel, ou outros defeitos de impressora.
- Animação de abrir a bandeja, encaixar o toner, som de impressora.
- Prazo ou penalidade por demorar a reabastecer — o `world_event` aqui não tem
  `deadlineSeconds`. Prazo é o eixo do plano 0010, exercitado nos planos 0020 e 0024.
- Sincronizar entre jogadores o estado da impressora: cada cliente deriva o estado dos eventos e
  da própria task (limitação geral desta entrega).
- Snap visual do toner dentro da impressora — é recipiente (`TaskDepositPoint`), não slot de
  posição; para isso ver plano [0011](0011-infra-slot-de-posicionamento.md).
