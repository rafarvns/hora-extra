# Plan 0015 — tarefa-05-remocao-de-virus

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 5 — Remoção de vírus do computador**.
> **Depende de [0009](0009-infra-terminal-de-computador.md)**. Mapa das 18 tarefas:
> [README.md](README.md).

## 1. Context

O documento descreve:

> um computador apresenta falha devido a vírus → após **encontrar o programa do antivírus**, o
> jogador deve clicar nele → um **minigame de escaneamento** pode ser iniciado para remover as
> ameaças → o computador volta a funcionar após a conclusão

Duas coisas a resolver.

**"Encontrar o programa" é busca dentro da tela.** Diferente da Tarefa 4, onde a pista está no
mundo, aqui a dificuldade é a própria área de trabalho: muitos ícones, um deles é o antivírus.
Isso mapeia direto para uma etapa com `expects` — o ícone certo submete o valor certo, os outros
tomam `WRONG_VALUE` e o jogador tenta de novo.

**O minigame é o segundo minigame do projeto.** O primeiro é o QTE da cafeteira (`CoffeeQTE`,
plano 0003), que roda pelo par `task_start_interaction` / `task_complete_attempt`. Aqui ele
precisa rodar **dentro** de uma sequência de etapas, e os dois caminhos não podem coexistir na
mesma task: `task_complete_attempt` chama `resolveTask`, que marca `completed` direto e
atropelaria a contagem de `steps`.

**Decisão: a tarefa inteira é `steps`, e o resultado do minigame é submetido como a etapa final.**
O cliente só envia `escanear` quando o escaneamento termina com sucesso; se falhar, nada é
enviado e o jogador repete.

Isso **não** enfraquece o modelo de confiança: o `task_complete_attempt` de hoje já recebe
`success: boolean` do cliente (plano 0003) — o resultado de minigame sempre foi reportado pelo
cliente neste projeto. O que o servidor continua garantindo é o que ele sempre garantiu aqui:
**ordem** (não dá para escanear sem ter achado o antivírus) e **unicidade**
(`STEP_ALREADY_DONE`). Ganhar o minigame de forma desonesta é o mesmo grau de exposição do QTE da
cafeteira, e resolver isso é um plano de anti-cheat próprio, não deste escopo.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. As três etapas são **dados de catálogo** sobre o
`submitStep` do plano 0009.

**Phase client** — `TerminalDesktopApp.cs` (área de trabalho com ícones); `AntivirusScanMinigame.cs`
(herda de `TerminalApp`); `InfectedComputerState.cs` (o "PC com falha" e o "volta a funcionar");
entrada de catálogo; textos; setup de cena.

### Contratos cross-repo

Nenhum evento novo.

| taskId | type | steps (em ordem) |
| :----- | :--- | :--------------- |
| `task-remover-virus-01` | `remove_virus` | `abrir-pc` · `achar-antivirus` (`expects: "antivirus"`) · `escanear` |

`targetCount` = 3.

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md` e acrescentar `remove_virus` aos
valores conhecidos de `type`. Documentar, junto, a nota da §1 sobre resultado de minigame ser
reportado pelo cliente — é contrato de confiança, e contrato mora no doc, não em comentário de
código.

### Client

#### `UI/Terminal/TerminalDesktopApp.cs` (NEW)

Área de trabalho genérica: grade de ícones, cada um um `TerminalStepButton` (plano 0009) com
`_stepId = "achar-antivirus"` e `_value` = o id daquele ícone.

- Só **um** ícone carrega `_value = "antivirus"`; os outros (`navegador`, `planilha`, `musica`,
  `lixeira`…) são iscas legítimas e tomam `WRONG_VALUE` retentável.
- Os ícones ficam **visualmente iguais em peso** — se o antivírus tiver um ícone gritante, não há
  busca. A dificuldade é achar, não clicar.
- Genérico de propósito: o plano 0014 também precisa de área de trabalho. Se 0014 já tiver sido
  implementado, esta tela substitui a área de trabalho improvisada lá, e o ícone de e-mail vira
  mais um item da grade.

#### `UI/Terminal/AntivirusScanMinigame.cs` (NEW)

Herda de `TerminalApp`. Escaneamento com barra de progresso e ameaças aparecendo:

- barra que enche em ~8 s; a cada ~1,5 s aparece uma "ameaça" clicável com tempo de vida curto;
- deixar N ameaças escaparem → **falha**: barra zera, mensagem "Escaneamento falhou", botão
  "Tentar novamente". **Nenhum pacote é enviado numa falha** — falhar não pode consumir a etapa,
  senão a tarefa fica impossível;
- sucesso → `Submit("escanear")`, e a tela só mostra "Ameaças removidas" **depois** do ack.

Olhar o `CoffeeQTE` do plano 0003 antes de escrever: o loop de timing, o feedback e o estado de
sucesso/falha já estão resolvidos lá. Reaproveitar o que der — e **não** copiar o envio, que aqui
é `SendStepSubmit`, não `SendCompleteAttempt`.

#### `Interactions/InfectedComputerState.cs` (NEW)

O "apresenta falha" e o "volta a funcionar" do documento. No mesmo GameObject do
`ComputerTerminal`:

- estado infectado: tela com textura de erro/glitch, partícula ou luz piscando;
- assina `OnTaskStepAck`; no `stepId == "escanear"` da task deste tipo, troca para a tela normal;
- o estado infectado liga quando a task é atribuída (`OnTaskAssigned`) e desliga quando ela
  completa — **não** fica infectado permanentemente na cena, senão um PC usado por outra tarefa
  (0012, 0014) aparece quebrado sem motivo.

Mudar o visual só no ack, e não no clique, é a mesma regra autoritativa dos outros planos.

#### Catálogo e apresentação

`TaskSystemBridge.EnsureRemoveVirusEntry()`.

`TaskPresentation` ganha `TYPE_REMOVE_VIRUS`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Remover o vírus do computador" |
| `GetHowTo` | "Um computador do escritório está com vírus. Procure o programa de antivírus na área de trabalho, abra e conclua o escaneamento." |
| `GetActionPrompt` | "Aperte [E] para usar o computador" |

#### Cena `SCN_FirstFloor.unity`

Um `ComputerTerminal` (`_terminalId = "pc-ti"`, `_taskType = "remove_virus"`) com
`TerminalDesktopApp` + `AntivirusScanMinigame` + `InfectedComputerState`; `WorldTaskMarker` no PC.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 5 + type remove_virus + nota sobre resultado de minigame)
```

### Client

```
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalDesktopApp.cs        NEW (genérico; compartilhado com o plano 0014)
hora-extra-client/Assets/Scripts/UI/Terminal/AntivirusScanMinigame.cs     NEW
hora-extra-client/Assets/Scripts/Interactions/InfectedComputerState.cs    NEW
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs           MODIFY (EnsureRemoveVirusEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs                   MODIFY (TYPE_REMOVE_VIRUS)
hora-extra-client/Assets/Prefab/PFB_UI_AntivirusScan.prefab               NEW (asset, Editor)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                      MODIFY (asset, Editor — PC da TI + estado infectado)
hora-extra-client/Docs/Mechanics/TASK-05-ANTIVIRUS.md                     NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo** — catálogo sobre o `submitStep` do plano 0009.

```bash
cd hora-extra-backend && npm test
```

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. PC aparece com defeito

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: how-to da §3 e `0/3`; o PC da TI mostra o estado infectado; os outros PCs da cena
  estão normais.

### 2. Abrir

- Ação: [E] no PC.
- Esperado: `stepId=abrir-pc` → ack → `1/3`; área de trabalho com vários ícones.

### 3. Ícone errado é retentável

- Ação: clicar em `navegador`.
- Esperado: `task_rejected — code=WRONG_VALUE`; mensagem na tela; contador `1/3`; a grade continua
  clicável. Tentar mais dois errados e confirmar que nada bloqueia.

### 4. Ícone certo

- Ação: clicar no antivírus.
- Esperado: ack de `achar-antivirus` → `2/3`; o minigame abre **depois** do ack.

### 5. Falhar não consome a etapa

- Ação: deixar as ameaças escaparem de propósito.
- Esperado: "Escaneamento falhou"; **nenhum** `task_step_submit` é enviado; contador continua
  `2/3`; botão "Tentar novamente" funciona.

### 6. Concluir o escaneamento

- Ação: jogar até o fim com sucesso.
- Esperado: `stepId=escanear` → ack `nextStepId=null` → `status=completed progress=3`; **só
  então** "Ameaças removidas" e o PC volta ao visual normal.

### 7. Fora de ordem

- Ação: reiniciar e forçar `escanear` pelo Inspector logo após `abrir-pc`.
- Esperado: `task_rejected — code=STEP_OUT_OF_ORDER`; minigame não abre.

### 8. Sair no meio do minigame

- Ação: reiniciar até `2/3`, abrir o minigame, apertar ESC no meio.
- Esperado: a tela fecha, cursor volta a travar, **nenhum pacote enviado**; reabrindo o PC o
  minigame recomeça do zero e o contador segue `2/3`.

### 9. Repetir

- Ação: tentar escanear de novo com a task concluída.
- Esperado: prompt do PC não aparece mais; forçando o envio, `task_rejected` (`INVALID_STATUS`
  ou `STEP_ALREADY_DONE`).

### 10. Cleanup

- Ação: sair do Play Mode com o minigame aberto.
- Esperado: nenhum `LogError`; cursor normal; nenhum `Coroutine` do scan pendurado (conferir que
  o minigame para em `OnDisable`).

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

- **Anti-cheat do resultado do minigame** (ver §1). O servidor garante ordem e unicidade, não
  desempenho. Vale igualmente para o QTE do plano 0003.
- Dificuldade dinâmica, pontuação, ranking ou tempo recorde de escaneamento.
- Vírus que se espalha para outros PCs, ou que afeta outras tarefas (travar a impressora, sumir
  com arquivos). Cada tarefa é independente nesta entrega.
- Penalidade por falhar o escaneamento — falhar é grátis e ilimitado. Penalidade é o eixo do
  plano [0010](0010-infra-agenda-notificacoes-e-penalidades.md).
- O vírus aparecer sozinho no meio do expediente (evento de mundo) — seria
  `world_event` do plano 0010; aqui o estado infectado liga com a atribuição da task.
- Som e animação de glitch além de uma textura trocada.
- Outro jogador ver o PC infectado (o estado é local, derivado da task do próprio jogador).
- Mais de um antivírus, ou falso antivírus que piora a situação.
