# Plan 0020 — tarefa-10-reuniao-com-o-chefe

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 10 — Reunião obrigatória com o chefe**.
> **Depende de [0010](0010-infra-agenda-notificacoes-e-penalidades.md)** (agenda, zona AABB,
> notificação no celular, penalidade). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> em momentos aleatórios ou pré-definidos, uma reunião é convocada → todos os jogadores recebem
> uma **notificação no celular corporativo** → os jogadores devem se dirigir à sala do chefe
> **dentro de um tempo limite** → durante a reunião, **não podem executar outras ações ou sair da
> sala** → jogadores que não comparecerem **ou interromperem** a reunião recebem penalidades
> (tarefa extra)

É a tarefa que mais depende do plano 0010 e a única com **duas janelas de tempo em sequência**:

1. **Chegada** — do momento da convocação até o prazo. Conferida **uma vez**, no vencimento.
2. **Permanência** — da chegada até o fim da reunião. Precisa ser conferida **continuamente**, e
   é o que o plano 0010 não faz: lá o prazo é um instante, não um intervalo.

Essa segunda janela é a única lógica de servidor que este plano acrescenta. A checagem contínua
mora no mesmo tick de 1 Hz e usa a mesma `isInsideZone` — é uma extensão pequena do
`ScheduleService`, não um mecanismo paralelo.

### Decisão: tolerância de 3 segundos fora da sala

Punir no primeiro frame fora da AABB seria injusto e frágil: a posição vem por UDP a 20 Hz e pode
atrasar, a porta fica na borda da zona, e um passo para trás não é "interromper a reunião". A
permanência é violada só depois de **3 segundos consecutivos** fora; voltar antes disso zera o
contador.

O número é generoso de propósito. O custo de punir injustamente (jogador perde e não entende por
quê) é muito maior do que o de deixar alguém escapar por 2 segundos.

### Decisão: reunião é da sala, presença é por jogador

A convocação é um `world_event` — chega para todos. Mas a penalidade é individual: quem faltou,
faltou. Isso cai naturalmente no desenho do plano 0010 (`world_event` broadcast, `penalty_assigned`
unicast) e não exige nada novo.

## 2. Scope & target

**Target:** `both`

**Phase backend** — `ScheduleService` ganha `watchPresence(...)` e a avaliação contínua no tick
existente; `TaskEntry` ganha `meetingSeconds?` e `presenceToleranceSeconds?`; o handler da
convocação passa a abrir a task e agendar as duas janelas; `COMMUNICATION.md` atualizado.

**Phase client** — `MeetingState.cs`; bloqueio de interações durante a reunião; entrada de
catálogo; textos; a zona da sala do chefe autorada com o `TaskZoneGizmo` do plano 0010.

### Contratos cross-repo

| Evento | Direção | Payload |
| :----- | :------ | :------ |
| `task_catalog_register` | C→S | **ALTERADO** — entrada aceita `meetingSeconds?: number` e `presenceToleranceSeconds?: number` |
| `world_event` | S→C | **INALTERADO no shape** — tipos novos: `meeting_called` e `meeting_started` |
| `task_expired` | S→C | **INALTERADO no shape** — `reason` novo: `left_meeting` |

Nenhum evento novo. Os campos de agenda (`deadlineSeconds`, `zone`, `penaltyTaskId`, `schedule`)
vêm todos do plano 0010.

## 3. Approach

### Backend

#### `ScheduleService` (MODIFY) — a janela de permanência

```ts
export interface PresenceWatch {
    roomId: string; playerId: string; taskId: string;
    zone: TaskZone;
    endsAtMs: number;
    toleranceSeconds: number;
    outsideSinceMs: number | null;   // null = está dentro
}
```

- `watchPresence(...)` registra; `stopWatch(taskId, playerId)` cancela.
- `evaluatePresence(nowMs, positions): PresenceOutcome[]` — **função pura**, no mesmo formato de
  `evaluateDeadlines` (plano 0010) e pelo mesmo motivo: testável sem relógio. Para cada watch:
  - dentro da zona → `outsideSinceMs = null`;
  - fora e `outsideSinceMs == null` → marca o instante;
  - fora há mais de `toleranceSeconds` → devolve `{ violated: true }` e o watch é consumido;
  - `now >= endsAtMs` sem violação → devolve `{ completed: true }`.
- O `tick()` de 1 Hz passa a chamar `evaluateDeadlines` **e** `evaluatePresence`, com as mesmas
  posições — uma leitura só do `UdpSocketManager` por tick.

Resolução de cada `PresenceOutcome`:

- `completed` → `TaskService.resolveTask(playerId, taskId, true)` + `task_updated` broadcast. É o
  único caso do projeto em que uma task completa **sem** o jogador apertar nada — e é o correto:
  a tarefa é *estar lá*.
- `violated` → `task_expired` com `reason: 'left_meeting'` + `assignPenaltyTask` +
  `penalty_assigned`. Mesmo caminho da ausência, com motivo diferente.

#### Sequência completa no servidor

1. `schedule` do catálogo dispara `world_event` `meeting_called` (plano 0010) e, para cada jogador
   da sala com a task, agenda o prazo de chegada (`deadlineSeconds`).
2. No vencimento, `evaluateDeadlines` diz quem está na zona:
   - **ausente** → `task_expired` `reason: 'absent'` + penalidade (plano 0010, sem código novo);
   - **presente** → `world_event` `meeting_started` e `watchPresence` por `meetingSeconds`.
3. No fim da permanência, resolve como acima.

A task fica `in_progress` durante a reunião. O jogador nunca envia um pacote nesta tarefa — todos
os eventos são S→C. É a primeira e única assim, e vale registrar no `COMMUNICATION.md`.

### Client

#### `UI/MeetingState.cs` (NEW)

Singleton leve que reage aos eventos e manda no estado de reunião do cliente local.

- `meeting_called` → notificação no `PhoneNotificationHud` (*"Reunião convocada. Vá até a sala do
  chefe."*) e `TaskDeadlineHud` visível (os dois do plano 0010).
- `meeting_started` → entra em modo reunião: `public static bool InMeeting { get; }` e
  `event Action<bool> OnMeetingChanged`.
- **"não podem executar outras ações"**: `InteractionInput.InteractPressed()` (plano 0004) passa a
  devolver `false` enquanto `InMeeting`. Um ponto único, em vez de um `if` espalhado por cada
  interagível — que é como esse tipo de regra vaza e fica inconsistente.
- `task_expired` com `reason: 'left_meeting'` → notificação (*"Você saiu da reunião."*) e sai do
  modo reunião.
- `task_updated` com `status: 'completed'` → sai do modo reunião, notificação de fim.
- **Aviso de borda**: enquanto `InMeeting`, se o jogador local sair da zona (calculada
  localmente com os mesmos `center`/`size` do catálogo), mostrar *"Volte para a reunião!"*. É
  dica local, **não** decisão: quem pune é o servidor. Sem esse aviso, a tolerância de 3 s é
  invisível e a punição parece arbitrária.

#### Catálogo e apresentação

Entrada `task-reuniao-chefe-01`, `type = "boss_meeting"`, `targetCount = 1`,
`deadlineSeconds = 45`, `meetingSeconds = 30`, `presenceToleranceSeconds = 3`,
`zone` = AABB da sala do chefe, `penaltyTaskId = "task-recep-papeis-01"` (uma tarefa de coleta
existente serve de tarefa extra — não criar tarefa de castigo nova),
`schedule = [{ atSeconds: 60 }]`.

`TaskPresentation` ganha `TYPE_BOSS_MEETING`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Reunião com o chefe" |
| `GetHowTo` | "Vá até a sala do chefe antes do tempo acabar e fique lá até a reunião terminar. Sair no meio conta como falta." |
| `GetActionPrompt` | *(nenhum — não há interação)* |

#### Cena `SCN_FirstFloor.unity`

`TaskZoneGizmo` (plano 0010) cobrindo a sala do chefe — **sem incluir o vão da porta**, para que
ficar na soleira não conte como dentro. Copiar `center`/`size` do gizmo para o catálogo; é a mesma
informação em dois lugares e a fonte de erro mais provável deste plano (§6, passo 1).

## 4. Files to change

### Backend

```
hora-extra-backend/src/services/ScheduleService.ts                  MODIFY (PresenceWatch, watchPresence, stopWatch, evaluatePresence, tick)
hora-extra-backend/src/services/ScheduleService.test.ts             MODIFY (ciclos 1-8)
hora-extra-backend/src/services/TaskService.ts                      MODIFY (meetingSeconds, presenceToleranceSeconds em TaskEntry)
hora-extra-backend/docs/Networking/COMMUNICATION.md                 MODIFY (campos novos, world_event meeting_called/meeting_started, reason left_meeting, nota de task 100% S→C)
hora-extra-backend/docs/Mechanics/SCHEDULE-AND-PENALTIES.md         MODIFY (seção de permanência; arquivo criado no plano 0010)
```

### Client

```
hora-extra-client/Assets/Scripts/UI/MeetingState.cs                 NEW
hora-extra-client/Assets/Scripts/Interactions/InteractionInput.cs   MODIFY (bloqueio global durante a reunião)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs     MODIFY (EnsureBossMeetingEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs             MODIFY (TYPE_BOSS_MEETING)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                MODIFY (asset, Editor — zona da sala do chefe + MeetingState no HUD_Canvas)
hora-extra-client/Docs/Mechanics/TASK-10-REUNIAO.md                 NEW
```

## 5. TDD breakdown (phase: backend)

### ScheduleService (`ScheduleService.test.ts`)

Todos puros — `evaluatePresence` recebe `now` e as posições.

- Cycle 1: `it('evaluatePresence não devolve nada enquanto o jogador está na zona')`.
- Cycle 2: `it('evaluatePresence não pune saída menor que a tolerância')` → fora por 2 s, dentro
  de novo, nada acontece.
- Cycle 3: `it('evaluatePresence zera o contador ao voltar para a zona')` → fora 2 s, dentro,
  fora 2 s → ainda sem violação. O teste que fixa a decisão da §1.
- Cycle 4: `it('evaluatePresence devolve violated após a tolerância consecutiva fora')`.
- Cycle 5: `it('evaluatePresence devolve completed ao fim de meetingSeconds sem violação')`.
- Cycle 6: `it('evaluatePresence trata ausência de posição (offline) como fora da zona')`.
- Cycle 7: `it('evaluatePresence consome o watch — não devolve o mesmo resultado duas vezes')` →
  senão o tick de 1 Hz pune a cada segundo.
- Cycle 8: `it('stopWatch cancela a vigilância — nada é devolvido depois')`.

### Integração da sequência

- Cycle 9: `it('chegar a tempo agenda a vigilância de permanência')`.
- Cycle 10: `it('não chegar a tempo atribui penalidade e NÃO agenda vigilância')`.
- Cycle 11: `it('sair no meio atribui penalidade com reason left_meeting')`.
- Cycle 12: `it('ficar até o fim resolve a task como completed sem pacote do cliente')`.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play". Reduzir `schedule.atSeconds`, `deadlineSeconds` e `meetingSeconds` durante os
testes (ex.: 10 / 20 / 15) — os valores finais deixam cada rodada muito longa.

### 1. Zona confere com o gizmo (antes do Play Mode)

- Ação: comparar `center`/`size` do `TaskZoneGizmo` com os do catálogo.
- Esperado: idênticos, e a AABB **não** cobre o vão da porta. Erro aqui gera punição
  inexplicável — por isso é o passo 1.

### 2. Convocação

- Ação: entrar em Play Mode e esperar o `schedule`.
- Esperado: `world_event — type=meeting_called`; notificação no celular; `TaskDeadlineHud`
  contando.

### 3. Faltar

- Ação: ficar longe da sala até o prazo vencer.
- Esperado: `task_expired — reason=absent`; `penalty_assigned`; tarefa extra no `MissionListHud`;
  notificação de falta.

### 4. Comparecer

- Ação: reiniciar e entrar na sala antes do prazo.
- Esperado: `world_event — type=meeting_started`; `TaskDeadlineHud` passa a contar a reunião;
  **nenhuma** penalidade.

### 5. Interações bloqueadas durante a reunião

- Ação: dentro da reunião, tentar pegar um item ou usar um PC.
- Esperado: nenhum prompt responde a [E]; nenhum pacote sai. Conferir também que o movimento
  continua livre (só a interação é bloqueada).

### 6. Sair e voltar rápido não pune

- Ação: sair da zona por ~2 s e voltar.
- Esperado: aviso *"Volte para a reunião!"* aparece e some; **nenhum** `task_expired`; a reunião
  segue. Repetir duas vezes seguidas para exercitar o zerar do contador (ciclo 3).

### 7. Sair de vez pune

- Ação: sair e ficar fora ~5 s.
- Esperado: `task_expired — reason=left_meeting`; `penalty_assigned`; interações voltam a
  funcionar (saiu do modo reunião).

### 8. Ficar até o fim conclui sozinho

- Ação: reiniciar, comparecer e ficar parado até o fim.
- Esperado: `task_updated … status=completed` **sem nenhum pacote enviado pelo cliente** —
  conferir no log que não saiu `task_progress` nem `task_start_interaction`. Interações
  desbloqueiam; notificação de fim.

### 9. Porta não conta como dentro

- Ação: ficar parado exatamente no vão da porta durante a reunião.
- Esperado: conta como **fora** e, passados os 3 s, pune. Se contar como dentro, a AABB invadiu a
  porta — voltar ao passo 1.

### 10. Desconectar durante a reunião

- Ação: fechar o cliente no meio da reunião e reconectar.
- Esperado: penalidade registrada (jogador offline conta como fora, plano 0010) e entregue ao
  reconectar. Nenhum `LogError`.

### 11. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; `InteractionInput` desbloqueado; nenhum watch pendurado no log do
  servidor.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npx vitest run src/services/ScheduleService.test.ts
npm test
npx tsc --noEmit
```

### Client

Seguir os 11 passos da §6 em Play Mode.

## 8. Out of scope

- **Conteúdo da reunião**: diálogo, slides, chefe como NPC falando, animação de sentar. A reunião
  é "estar na zona pelo tempo X".
- Chefe NPC que se move ou reage à presença.
- Reunião em **momento aleatório** de verdade. `schedule` é uma lista de disparos fixos (plano
  0010); aleatoriedade exigiria um sorteio no servidor e é extensão de outro plano.
- Sincronizar a reunião entre jogadores como um evento único com lista de presentes visível
  ("3 de 4 chegaram").
- Penalidade escalonada (faltar duas vezes é pior).
- Punição por chegar atrasado **mas** chegar — aqui é binário no instante do prazo.
- Bloquear o **movimento** durante a reunião (só a interação é bloqueada, de propósito: travar o
  movimento tornaria impossível sair, e sair é uma escolha que o documento pune, não impede).
- Sala de reunião separada da sala do chefe.
- Tarefa extra específica de castigo — reusa uma entrada de catálogo existente (§3).
