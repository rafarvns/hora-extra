# Plan 0024 — tarefa-14-bater-ponto

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 14 — Bater ponto**.
> **Depende de [0010](0010-infra-agenda-notificacoes-e-penalidades.md)** (agenda, notificação,
> prazo, penalidade) e de [0009](0009-infra-terminal-de-computador.md) + o `StepInteractionPoint`
> do plano [0012](0012-tarefa-02-imprimir-documentos-e-levar-ao-chefe.md).
> Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> o jogador deve registrar **entrada ou saída** em horários específicos → próximo ao horário,
> recebe uma **notificação de lembrete** → o registro deve ser realizado em um **terminal
> eletrônico** → caso perca o horário, recebe **mais tarefas extras**

Mecanicamente é a tarefa mais simples do eixo "tempo": uma ação, um lugar, uma janela. Todo o
maquinário já existe (`schedule`, `deadlineSeconds`, `penaltyTaskId`, `PhoneNotificationHud`,
`StepInteractionPoint`). O valor deste plano está em duas decisões e numa lacuna do plano 0010.

**Duas entradas de catálogo, não uma task com dois passos.** Entrada e saída são separadas por um
expediente inteiro e têm janelas independentes: falhar na entrada não deve impedir de bater a
saída. Duas entradas (`task-bater-ponto-entrada` e `task-bater-ponto-saida`) dão isso de graça;
uma task com dois `steps` encadearia as duas pela ordem obrigatória do plano 0009, que é
exatamente o contrário do desejado.

**A janela precisa fechar no servidor.** O plano 0010 criou o código `DEADLINE_EXPIRED` mas
nenhuma tarefa o usa ainda: lá, o vencimento do prazo *emite* `task_expired` e pune, mas nada
impede o jogador de cumprir a ação **depois**. Para bater ponto isso é errado por definição —
bater ponto atrasado é justamente o que gera a penalidade. Este plano fecha essa lacuna, e a
fecha de forma genérica (`submitStep` e `incrementProgress` passam a consultar o prazo), para que
qualquer tarefa futura com prazo herde o comportamento.

É a única lógica de servidor deste plano.

### Decisão: o prompt só aparece com a janela aberta

O `StepInteractionPoint` da catraca fica inerte até chegar o `world_event` de lembrete. Sem isso,
o jogador bate ponto no primeiro segundo da partida, a janela nunca significa nada e o lembrete
vira decoração.

Isso é um gate **de cliente** — o servidor não sabe "cedo demais", só "tarde demais". A assimetria
é consciente: bater cedo não tem consequência de jogo (é o mesmo que bater na hora), enquanto
bater tarde tem, e é o lado que precisa de autoridade. Registrado na §8.

## 2. Scope & target

**Target:** `both`

**Phase backend** — `TaskService.submitStep` e `incrementProgress` passam a rejeitar com
`DEADLINE_EXPIRED` quando o prazo da task já venceu; `ScheduleService` expõe
`isExpired(taskId, playerId)`; `COMMUNICATION.md` atualizado.

**Phase client** — `PunchClockTerminal.cs` (gate de janela sobre o `StepInteractionPoint`); duas
entradas de catálogo; textos; setup de cena na catraca.

### Contratos cross-repo

Nenhum evento novo. Tipos novos de `world_event`: `punch_clock_in` e `punch_clock_out`.

| taskId | type | steps | agenda |
| :----- | :--- | :---- | :----- |
| `task-bater-ponto-entrada` | `punch_clock` | `[{ stepId: "registrar" }]` | `schedule: [{ atSeconds: 30 }]` → `punch_clock_in`; `deadlineSeconds: 60`; `penaltyTaskId: "task-recep-papeis-01"` |
| `task-bater-ponto-saida` | `punch_clock` | `[{ stepId: "registrar" }]` | `schedule: [{ atSeconds: 300 }]` → `punch_clock_out`; `deadlineSeconds: 60`; `penaltyTaskId: "task-recep-latas-01"` |

`targetCount` = 1 em ambas. As duas compartilham o mesmo `type` — a UI distingue pelo `taskId`,
e o servidor não precisa saber a diferença.

## 3. Approach

### Backend

#### `ScheduleService` (MODIFY)

`isExpired(taskId, playerId): boolean` — devolve `true` se havia um prazo para esse par e ele já
venceu. Consulta pura sobre o estado que o `evaluateDeadlines` já mantém (plano 0010); nenhum
estado novo.

#### `TaskService` (MODIFY)

Tanto `submitStep` (plano 0009) quanto `incrementProgress` (plano 0004) ganham, **logo após** a
checagem de `INVALID_STATUS`:

```ts
if (this.scheduleService.isExpired(taskId, playerId)) {
    throw new TaskRejectionError('DEADLINE_EXPIRED', 'O prazo desta tarefa já venceu.');
}
```

Posição na ordem importa: depois de `NOT_ASSIGNED`/`INVALID_STATUS` (erros mais básicos) e
**antes** de qualquer validação de item ou etapa — não faz sentido conferir se a senha está certa
numa tarefa que já venceu.

**Injeção, não import direto.** `TaskService` não deve importar `ScheduleService` diretamente:
os dois são obtidos pela `ServiceFactory` (`.agents/rules/backend-factory-pattern.md`) e a
dependência é resolvida lá. Isso também é o que permite os testes passarem um stub de
`isExpired` sem montar a agenda inteira.

Efeito colateral desejado: **todas** as tarefas com `deadlineSeconds` passam a rejeitar ação
tardia, não só esta. Rodar a suíte inteira depois desta mudança — é a alteração de maior alcance
de todos os planos de tarefa.

### Client

#### `Interactions/PunchClockTerminal.cs` (NEW)

No mesmo GameObject do `StepInteractionPoint` da catraca.

- `[SerializeField] private string _openOnEventType` — `punch_clock_in` ou `punch_clock_out`;
- assina o `WorldEventRouter` (plano 0010); até o evento chegar, mantém o `StepInteractionPoint`
  **desabilitado** (sem prompt, sem [E]) e o display da catraca apagado;
- ao receber o evento: habilita, acende o display (*"Registre seu ponto"*), e o
  `TaskDeadlineHud` (plano 0010) já mostra o contador sozinho;
- ao concluir (`task_updated` com `completed`): display mostra o horário registrado e volta a
  desabilitar;
- em `task_expired`: display mostra *"Ponto perdido"*, desabilita, e a notificação da penalidade
  chega pelo caminho genérico do plano 0010.

#### Catálogo e apresentação

`TaskSystemBridge.EnsurePunchClockEntries()` — cria as **duas** entradas.

`TaskPresentation` ganha `TYPE_PUNCH_CLOCK`. Como as duas entradas têm o mesmo `type`, os textos
variam por `taskId`:

| taskId | `GetTitle` | `GetHowTo` |
| :----- | :--------- | :--------- |
| `…entrada` | "Bater o ponto de entrada" | "Vá até a catraca da recepção e registre sua entrada antes do prazo acabar." |
| `…saida` | "Bater o ponto de saída" | "Vá até a catraca da recepção e registre sua saída antes do prazo acabar." |

`GetActionPrompt`: "Aperte [E] para bater o ponto".

Notificações do `world_event`: *"Está na hora de bater o ponto de entrada."* / *"…de saída."*

#### Cena `SCN_FirstFloor.unity`

A catraca (`SM_Env_Turnstile`, já em `arte/3D_Models/`) na recepção, com `StepInteractionPoint`
(`_stepId = "registrar"`, `_taskType = "punch_clock"`), `PunchClockTerminal` e `WorldTaskMarker`.

**Duas** instâncias de `StepInteractionPoint` + `PunchClockTerminal` no mesmo objeto — uma para
cada `taskId`/evento. Alternativa: um componente só que descobre a task ativa pelo `_taskType`.
Preferir a segunda se o `StepInteractionPoint` do plano 0012 já resolver a task por tipo — evita
dois prompts competindo pelo mesmo [E], que é o mesmo cuidado tomado na impressora do plano 0018.

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/ScheduleService.ts        MODIFY (isExpired)
hora-extra-backend/src/services/ScheduleService.test.ts   MODIFY (ciclos 1-3)
hora-extra-backend/src/services/TaskService.ts            MODIFY (DEADLINE_EXPIRED em submitStep e incrementProgress, via ServiceFactory)
hora-extra-backend/src/services/TaskService.test.ts       MODIFY (ciclos 4-9)
hora-extra-backend/docs/Networking/COMMUNICATION.md       MODIFY (entradas da Tarefa 14, type punch_clock, world_event punch_clock_in/out, regra de DEADLINE_EXPIRED valendo para TODA task com prazo)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/PunchClockTerminal.cs   NEW
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs       MODIFY (EnsurePunchClockEntries)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs               MODIFY (TYPE_PUNCH_CLOCK com textos por taskId)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                  MODIFY (asset, Editor — catraca com terminal de ponto)
hora-extra-client/Docs/Mechanics/TASK-14-PONTO.md                     NEW
```

## 5. TDD breakdown (phase: backend)

### ScheduleService (`ScheduleService.test.ts`)

- Cycle 1: `it('isExpired devolve false antes do vencimento')`.
- Cycle 2: `it('isExpired devolve true depois do vencimento')`.
- Cycle 3: `it('isExpired devolve false para task sem prazo agendado')` → o caso que protege
  todas as tarefas sem `deadlineSeconds`.

### TaskService (`TaskService.test.ts`)

- Cycle 4: `it('submitStep lança DEADLINE_EXPIRED quando o prazo da task já venceu')`.
- Cycle 5: `it('submitStep aceita normalmente dentro do prazo')`.
- Cycle 6: `it('incrementProgress lança DEADLINE_EXPIRED quando o prazo já venceu')` → e o
  `itemId` **não** entra em `collectedItems`.
- Cycle 7: `it('DEADLINE_EXPIRED tem precedência sobre WRONG_VALUE e WRONG_SLOT')`.
- Cycle 8: `it('NOT_ASSIGNED e INVALID_STATUS têm precedência sobre DEADLINE_EXPIRED')`.
- Cycle 9: `it('task sem prazo nunca lança DEADLINE_EXPIRED')` → regressão explícita de todas as
  tarefas dos planos 0004–0023.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play". Reduzir `atSeconds` e `deadlineSeconds` durante os testes (ex.: 10 / 30).

### 1. Catraca inerte antes da hora

- Ação: entrar em Play Mode e ir direto à catraca.
- Esperado: display apagado; **nenhum** prompt; [E] não faz nada; nenhum pacote sai.

### 2. Lembrete

- Ação: esperar o `schedule` da entrada.
- Esperado: `world_event — type=punch_clock_in`; notificação no celular; display acende;
  `TaskDeadlineHud` começa a contar.

### 3. Bater o ponto

- Ação: [E] na catraca.
- Esperado: `task_step_submit … stepId=registrar` → ack `nextStepId=null` →
  `task_updated … status=completed progress=1`; display mostra o registro; prompt some;
  contador do prazo desaparece.

### 4. Bater duas vezes

- Ação: [E] de novo.
- Esperado: prompt não aparece. Forçando o envio: `task_rejected` (`INVALID_STATUS` ou
  `STEP_ALREADY_DONE`).

### 5. Perder o horário

- Ação: reiniciar e **não** bater o ponto até o prazo vencer.
- Esperado: `task_expired`; `penalty_assigned`; tarefa extra no `MissionListHud`; notificação;
  display mostra "Ponto perdido".

### 6. Bater depois do prazo é recusado

- Ação: logo após o passo 5, ir até a catraca e forçar o envio pelo Inspector.
- Esperado: `task_rejected — code=DEADLINE_EXPIRED`; a task **não** completa. Este é o teste da
  lacuna que este plano fecha (§1) — se o envio for aceito, a mudança no `TaskService` não está
  ativa.

### 7. Saída é independente da entrada

- Ação: perder a entrada de propósito e esperar o `punch_clock_out`.
- Esperado: a janela da saída abre normalmente; dá para bater a saída; duas penalidades **não**
  são aplicadas pela mesma falta.

### 8. Regressão de tarefas sem prazo

- Ação: executar uma tarefa qualquer sem `deadlineSeconds` (coleta do plano 0005, QTE do 0003).
- Esperado: comportamento **idêntico** ao de antes; nenhum `DEADLINE_EXPIRED`. Passo obrigatório —
  a mudança do `TaskService` atinge todas as tarefas.

### 9. Reconexão

- Ação: fechar o cliente com a janela aberta e reconectar dentro do prazo.
- Esperado: a task ainda é cumprível; o contador reflete o tempo restante correto (não reinicia).

### 10. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; nenhum prazo pendurado no log do servidor.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/ScheduleService.test.ts
npx vitest run src/services/TaskService.test.ts
npm test          # obrigatório: a mudança de DEADLINE_EXPIRED atinge todas as tarefas
npx tsc --noEmit
```

### Client

Seguir os 10 passos da §6 em Play Mode.

## 8. Out of scope

- **Rejeitar no servidor o ponto batido cedo demais** (ver a decisão na §1). O gate de janela é de
  cliente; o servidor só conhece "tarde demais". Fechar isso exigiria um `opensAtSeconds` no
  catálogo e um código `NOT_OPEN_YET`.
- Relógio de jogo visível, horário fictício ("08:00"), jornada modelada como entidade — ver a §8
  do plano 0010.
- Intervalo de almoço, hora extra de verdade, banco de horas, folha de ponto consultável.
- Penalidade escalonada por faltas repetidas.
- Catraca que **bloqueia fisicamente** a passagem de quem não bateu o ponto.
- Ponto por biometria/crachá (`SM_Env_Badge` existe em `arte/3D_Models/` — seria outra mecânica).
- Sincronizar entre jogadores quem já bateu o ponto.
- Som e animação da catraca girando.
