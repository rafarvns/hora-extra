# Índice de planos — Hora-Extra

Mapa das **18 tarefas de jogador** descritas em `TAREFAS DOS JOGADORES.pdf` (raiz do repo) para
os planos deste diretório, na ordem em que devem ser implementados.

Dois documentos são fonte:

- **`TAREFAS DOS JOGADORES.pdf`** — lista-mestra das 18 tarefas. Define *o que* cada tarefa é.
- **`Explicação das tarefas para essa entrega.pdf`** — detalhamento de cena (assets, contagens,
  textos literais, tags) das Tarefas 15–18. Define *com quais objetos* aquelas quatro rodam.

Onde os dois divergem, a lista-mestra manda no escopo e o documento de entrega manda nos números
e nos textos de UI.

## Estado

| | |
| :-- | :-- |
| Implementado | 0001, 0002, 0003 |
| Planejado, não implementado | 0004 – 0024 |

## Planos de infraestrutura (compartilhados)

Cada um entrega mecânica + protocolo consumidos por vários planos de tarefa. **Implementar antes
dos planos de tarefa que dependem deles.**

| Plano | Entrega | Consumido por |
| :---- | :------ | :------------ |
| [0004](0004-infra-carregar-e-validar-itens.md) | Carregar/entregar item; `items`/`pairs` no catálogo; `itemId`/`slotId` no `task_progress`; evento `task_rejected` | 0005, 0006, 0007, 0008, 0011, 0013, 0018, 0019, 0021, 0022 |
| [0009](0009-infra-terminal-de-computador.md) | Terminal de computador (UI diegética); `steps[]` no catálogo; evento `task_step_submit` | 0012, 0013, 0014, 0015, 0017 |
| [0010](0010-infra-agenda-notificacoes-e-penalidades.md) | Agenda autoritativa do servidor; zonas AABB; notificação no celular; penalidade (tarefa extra); `world_event` | 0018, 0020, 0023, 0024 |
| [0011](0011-infra-slot-de-posicionamento.md) | Slot de posição-alvo com snap e ghost (o item fica visível no lugar certo, não some) | 0016, 0022 |

## Planos de tarefa

Ordem de implementação = ordem numérica. A coluna **Tarefa** é o número no PDF-mestre.

| Plano | Tarefa | Título | Depende de |
| :---- | :----- | :----- | :--------- |
| 0001–0003 | 1 | Passar o café (catálogo, sorteio, QTE) | — |
| [0005](0005-tarefa-16-organizar-recepcao.md) | 16 | Organizar a recepção | 0004 |
| [0006](0006-tarefa-18-limpar-recepcao-almoxarifado.md) | 18 | Limpar a recepção e o almoxarifado | 0004 |
| [0007](0007-tarefa-15-organizar-materiais-almoxarifado.md) | 15 | Organizar materiais do almoxarifado | 0004 |
| [0008](0008-tarefa-17-organizar-encomendas-almoxarifado.md) | 17 | Organizar encomendas no almoxarifado | 0004 |
| [0012](0012-tarefa-02-imprimir-documentos-e-levar-ao-chefe.md) | 2 | Imprimir documentos e levar ao chefe | 0004, 0009 |
| [0013](0013-tarefa-03-organizar-documentos.md) | 3 | Organizar documentos (gavetas por categoria, cadeado com senha) | 0004, 0009 |
| [0014](0014-tarefa-04-enviar-arquivos-e-emails.md) | 4 | Enviar arquivos/e-mails pelo computador | 0009 |
| [0015](0015-tarefa-05-remocao-de-virus.md) | 5 | Remoção de vírus do computador | 0009 |
| [0016](0016-tarefa-06-organizar-mesas-do-escritorio.md) | 6 | Organizar as mesas do escritório | 0004, 0011 |
| [0017](0017-tarefa-07-escrever-e-enviar-email.md) | 7 | Escrever e enviar e-mail | 0009 |
| [0018](0018-tarefa-08-reabastecer-impressora.md) | 8 | Reabastecer impressora | 0004, 0010 |
| [0019](0019-tarefa-09-transporte-cooperativo.md) | 9 | Transporte cooperativo de itens | 0004 |
| [0020](0020-tarefa-10-reuniao-com-o-chefe.md) | 10 | Reunião obrigatória com o chefe | 0010 |
| [0021](0021-tarefa-11-arremessar-papel-no-lixo.md) | 11 | Arremessar papel no lixo | 0004 |
| [0022](0022-tarefa-12-posicionar-item-especifico.md) | 12 | Posicionar item específico | 0004, 0011 |
| [0023](0023-tarefa-13-limpeza-de-vazamento-da-cafeteira.md) | 13 | Limpeza de vazamento da máquina de café | 0006, 0010 |
| [0024](0024-tarefa-14-bater-ponto.md) | 14 | Bater ponto | 0010 |

## Protocolo acumulado

Quadro consolidado do que cada plano acrescenta ao contrato cross-language
(`hora-extra-backend/docs/Networking/COMMUNICATION.md`). Todo campo novo de catálogo é
**opcional** — entradas antigas seguem funcionando.

### Campos do catálogo (`task_catalog_register`)

| Campo | Plano | Uso |
| :---- | :---- | :-- |
| `items?: string[]` | 0004 | ids de item que contam progresso nesta task |
| `pairs?: { itemId, slotId }[]` | 0004 | destino obrigatório por item |
| `steps?: TaskStep[]` | 0009 | passos ordenados validados pelo servidor |
| `locks?: { slotId, expects }[]` | 0013 | cadeado com senha num destino (portão, não progresso) |
| `deadlineSeconds?: number` | 0010 | prazo a partir da convocação |
| `zone?: { center, size }` | 0010 | AABB de presença, conferida contra a posição autoritativa |
| `penaltyTaskId?: string` | 0010 | task extra atribuída ao estourar o prazo |
| `schedule?: { atSeconds }[]` | 0010 | disparos programados pelo servidor |
| `coop?: { requiredCarriers }` | 0019 | nº de jogadores necessários |
| `meetingSeconds?`, `presenceToleranceSeconds?` | 0020 | janela de permanência contínua na zona |

### Tipos de `world_event`

Cada plano que cria um tipo registra o shape do seu `data` no `COMMUNICATION.md` — `data` é
`object` no contrato genérico, então sem isso cliente e servidor divergem em silêncio.

| `type` | `data` | Plano |
| :----- | :----- | :---- |
| `printer_out_of_supplies` | — | 0018 |
| `meeting_called`, `meeting_started` | — | 0020 |
| `coffee_leak` | `{ spots: string[] }` | 0023 |
| `punch_clock_in`, `punch_clock_out` | — | 0024 |

### Eventos novos

| Evento | Direção | Plano |
| :----- | :------ | :---- |
| `task_rejected` | S→C unicast | 0004 |
| `task_step_submit` | C→S | 0009 |
| `task_step_ack` | S→C unicast | 0009 |
| `slot_unlock_attempt` | C→S | 0013 |
| `slot_unlocked` | S→C unicast | 0013 |
| `world_event` | S→C broadcast | 0010 |
| `task_deadline` | S→C | 0010 |
| `task_expired` | S→C | 0010 |
| `penalty_assigned` | S→C unicast | 0010 |
| `coop_grab` / `coop_release` | C→S | 0019 |
| `coop_state` | S→C broadcast | 0019 |

### Códigos de `task_rejected`

`TaskRejectionCode` nasce em 0004 e é **estendido** pelos planos seguintes — nunca substituído.

| Código | Plano |
| :----- | :---- |
| `NOT_ASSIGNED`, `INVALID_STATUS`, `MISSING_ITEM`, `UNKNOWN_ITEM`, `WRONG_SLOT`, `ALREADY_COUNTED` | 0004 |
| `UNKNOWN_STEP`, `STEP_OUT_OF_ORDER`, `WRONG_VALUE`, `STEP_ALREADY_DONE` | 0009 |
| `LOCKED_SLOT` | 0013 |
| `DEADLINE_EXPIRED`, `OUT_OF_ZONE` | declarados em 0010; `DEADLINE_EXPIRED` só passa a ser **aplicado** em 0024, e a partir de lá vale para toda task com `deadlineSeconds` |
| `COOP_NOT_READY` | 0019 |

## Convenções destes planos

Todo plano segue a mesma estrutura de 8 seções:

1. Context · 2. Scope & target · 3. Approach · 4. Files to change · 5. TDD breakdown (backend)
· 6. Manual verification steps (client) · 7. Verification commands · 8. Out of scope

Regras que valem para todos, de `.agents/rules/`:

- **Backend é TDD estrito** (Red → Green → Refactor, Vitest). **Client não tem teste automatizado**
  — verificação é checklist de Play Mode (`no-unit-test-on-unity.md`).
- Qualquer evento/payload novo atualiza `COMMUNICATION.md` **no mesmo commit**
  (`communication-sync-rule.md`).
- Servidor é a fonte da verdade: o mundo visual só muda depois da confirmação do servidor.
- Cena de gameplay é **`Assets/Scenes/SCN_FirstFloor.unity`** (é a que está no
  `EditorBuildSettings`); `SCN_Main.unity` é stub legado.
