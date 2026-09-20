# Plan 0023 — tarefa-13-limpeza-de-vazamento-da-cafeteira

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 13 — Limpeza de vazamento da máquina de café**.
> **Depende de [0006](0006-tarefa-18-limpar-recepcao-almoxarifado.md)** (`DirtSpot`, ferramenta,
> limpar segurando [E]) e de [0010](0010-infra-agenda-notificacoes-e-penalidades.md)
> (`world_event`, prazo, penalidade). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> a máquina de café apresenta defeito e **derrama líquido no chão** → surge uma área de sujeira
> que **dificulta a movimentação** dos jogadores → o jogador deve buscar materiais de limpeza →
> após limpar completamente a área, o local volta ao estado normal → **se ignorado por muito
> tempo**, outros jogadores podem escorregar ou receber penalidades

É a Tarefa 18 (plano 0006) com três diferenças, e cada uma vem de um plano que já existe:

| Diferença | De onde vem |
| :-------- | :---------- |
| A sujeira **aparece em runtime**, não está autorada na cena | `world_event` (plano 0010) |
| A sujeira **atrapalha** quem passa por cima | novo aqui — `SlipperyArea` |
| **Ignorar tem custo** (prazo + penalidade) | `deadlineSeconds` + `penaltyTaskId` (plano 0010) |

O ato de limpar em si — pegar o rodo, chegar perto, segurar [E], barra de progresso, sujeira
some — é **exatamente** o `DirtSpot` do plano 0006, sem alteração. Foi por isso que aquele plano
pede o `DirtSpot` configurável por Inspector e sem dependência de estado de cena: para poder ser
instanciado por prefab aqui.

### Decisão: o servidor manda **onde**, o cliente instancia

O `world_event` carrega `data: { spots: ["vazamento-01", "vazamento-02"] }` — ids, não
coordenadas. Cada cliente tem âncoras autoradas na cena com esses ids e instancia o prefab de
poça ali.

Mandar coordenadas seria dar ao servidor conhecimento de geometria da cena, que ele não tem em
nenhum outro lugar do projeto (o catálogo é declarado pelo cliente desde o plano 0001). Ids mantêm
a mesma divisão de responsabilidade: o servidor decide **se** e **quando**, o cliente sabe
**onde**.

### Decisão: escorregar é local e não causa dano

*"Outros jogadores podem escorregar ou receber penalidades"* tem duas leituras. Este plano
implementa a primeira como **efeito local** (reduz velocidade e dá um tropeço curto) e a segunda
como a **penalidade já existente do plano 0010** aplicada a quem tem a task e deixa o prazo
vencer.

Punir um jogador *que não tem a task* por escorregar na poça de outro seria frustrante e não está
claramente pedido. O efeito de movimento já cumpre o papel de "vazamento incomoda todo mundo",
que é o que faz a tarefa ter urgência social.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. Catálogo combinando `items` (plano 0004), `schedule`,
`deadlineSeconds` e `penaltyTaskId` (plano 0010); o backend só recebe documentação.

**Phase client** — `CoffeeLeakSpawner.cs`; `SlipperyArea.cs`; `PlayerController` usa o
`SpeedMultiplier`; entrada de catálogo; textos; setup de cena (âncoras + prefab de poça).

### Contratos cross-repo

Nenhum evento novo. Tipo novo de `world_event`: `coffee_leak`, com
`data: { spots: string[] }`.

| taskId | type | targetCount | items | agenda |
| :----- | :--- | :---------- | :---- | :----- |
| `task-vazamento-cafe-01` | `clean_leak` | 2 | `vazamento-01`, `vazamento-02` | `schedule: [{ atSeconds: 90 }]`, `deadlineSeconds: 180`, `penaltyTaskId: "task-recep-papeis-01"` |

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md` como **exemplo de catálogo que combina
`items` com a agenda completa do plano 0010**, acrescentar `clean_leak` aos valores conhecidos de
`type` e `coffee_leak` aos tipos de `world_event` (com o shape do `data`).

Documentar o `data` do `world_event` é o ponto que mais escapa: `data` é `object` no contrato
genérico do plano 0010, então cada tipo de evento precisa do seu shape escrito, senão o cliente e
o servidor divergem em silêncio (`.agents/rules/communication-sync-rule.md`).

### Client

#### `Interactions/CoffeeLeakSpawner.cs` (NEW)

Na cafeteira. Assina `coffee_leak` pelo `WorldEventRouter` (plano 0010).

- `[SerializeField] private GameObject _puddlePrefab` — `PFB_Dirt_CoffeePuddle`, contendo
  `DirtSpot` (plano 0006, `_requiredTool = "Rodo"`) + `SlipperyArea`;
- `[SerializeField] private List<LeakAnchor> _anchors` — pares `spotId → Transform`;
- ao receber o evento, instancia uma poça em cada âncora listada no `data.spots`, passando o
  `spotId` como `_itemId` do `DirtSpot` — é o que o `task_progress` vai enviar;
- **idempotente**: um `world_event` repetido (UDP) não pode gerar poça em cima de poça. Guardar
  os `spotId` já instanciados;
- quando o `task_updated` marca a task como `completed`, destrói o que sobrar e dispara o efeito
  de "voltou ao normal" na cafeteira (a máquina para de pingar).

O `DirtSpot` do plano 0006 conta progresso por `itemId` como qualquer entrega — nenhuma alteração
naquele componente. Se ele tiver sido escrito assumindo `_itemId` fixo de Inspector, expor um
`Initialize(string itemId)` é a única mudança necessária, e é pequena.

#### `Interactions/SlipperyArea.cs` (NEW)

No mesmo prefab da poça, trigger ligeiramente maior que o decal.

- `OnTriggerStay` no jogador local → `PlayerController.SpeedMultiplier = 0.6f`;
- correr (sprint) dentro da área → **tropeço**: bloqueia o input de movimento por ~0,6 s e
  dispara um `Debug.Log("[GAMEPLAY] jogador escorregou")`. Só ao correr — andar devagar é a forma
  de atravessar com segurança, o que dá ao jogador uma escolha em vez de um castigo aleatório;
- `OnTriggerExit` e `OnDisable` restauram `SpeedMultiplier = 1f`. Restaurar em `OnDisable`
  importa porque a poça é **destruída** ao ser limpa, possivelmente com o jogador em cima dela —
  sem isso, ele fica lento para sempre.

`SpeedMultiplier` é o mesmo campo introduzido pelo plano 0019. Se aquele plano ainda não tiver
sido implementado, adicioná-lo aqui (é uma propriedade); se já tiver, **não** criar um segundo —
dois sistemas escrevendo no mesmo multiplicador sem coordenação é como se perde a velocidade
normal. Com os dois, usar o menor valor ativo em vez de sobrescrever.

#### Catálogo e apresentação

`TaskSystemBridge.EnsureCleanLeakEntry()`.

`TaskPresentation` ganha `TYPE_CLEAN_LEAK`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Limpar o vazamento da cafeteira" |
| `GetHowTo` | "A máquina de café vazou. Pegue o rodo no almoxarifado e segure [E] em cima de cada poça até limpar." |
| `GetActionPrompt` | "Segure [E] para limpar o café" |

Notificação do `world_event` (plano 0010): *"A máquina de café está vazando na copa."* — enviada
a **todos** na sala, com ou sem a task, porque a poça atrapalha todo mundo.

#### Cena `SCN_FirstFloor.unity`

Duas `LeakAnchor` no chão em volta da cafeteira (`SM_Env_CoffeeMachine`), com os ids
`vazamento-01` e `vazamento-02`; o `CoffeeLeakSpawner` na máquina; o prefab
`PFB_Dirt_CoffePuddle` reaproveitando o decal de mancha de café que já existe na cena
(`Liquido_Cafe`). O rodo continua sendo o do plano 0006, no almoxarifado — **não** duplicar.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 13 + type clean_leak + world_event coffee_leak com shape do data)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/CoffeeLeakSpawner.cs    NEW
hora-extra-client/Assets/Scripts/Interactions/SlipperyArea.cs         NEW
hora-extra-client/Assets/Scripts/Interactions/DirtSpot.cs             MODIFY (Initialize(itemId) — criado no plano 0006)
hora-extra-client/Assets/Scripts/Characters/PlayerController.cs       MODIFY (SpeedMultiplier, se o plano 0019 não o tiver criado)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs       MODIFY (EnsureCleanLeakEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs               MODIFY (TYPE_CLEAN_LEAK)
hora-extra-client/Assets/Prefab/PFB_Dirt_CoffeePuddle.prefab          NEW (asset, Editor — DirtSpot + SlipperyArea)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                  MODIFY (asset, Editor — spawner + 2 âncoras na copa)
hora-extra-client/Docs/Mechanics/TASK-13-VAZAMENTO.md                 NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo.** Esta tarefa não adiciona lógica de servidor — é catálogo sobre o
`items` do plano 0004 e a agenda do plano 0010.

```bash
cd hora-extra-backend && npm test
```

Regressão relevante: os ciclos de `scheduleWorldEvent` e `assignPenaltyTask` do plano 0010 são os
que sustentam esta tarefa. Se estiverem falhando, **parar**.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play". Reduzir `atSeconds` e `deadlineSeconds` durante os testes (ex.: 15 / 60).

### 1. Copa começa limpa

- Ação: entrar em Play Mode e ir à copa imediatamente.
- Esperado: **nenhuma** poça; a cafeteira normal; velocidade normal ao passar.

### 2. O vazamento acontece

- Ação: esperar o `schedule`.
- Esperado: `world_event — type=coffee_leak data.spots=[vazamento-01,vazamento-02]`; notificação
  no celular; **duas** poças aparecem nas âncoras; a cafeteira começa a pingar.

### 3. Evento repetido não duplica

- Ação: forçar um segundo `coffee_leak` (reconectar sem `resetRoom`, ou reenviar pelo backend).
- Esperado: continuam **duas** poças, não quatro. Idempotência do spawner (§3).

### 4. A poça atrapalha

- Ação: **andar** por cima de uma poça.
- Esperado: velocidade cai visivelmente; sair da poça restaura.

### 5. Correr faz escorregar

- Ação: **correr** por cima.
- Esperado: tropeço curto com perda de controle; `[GAMEPLAY] jogador escorregou`; volta ao normal
  sozinho. Andar devagar **não** faz escorregar.

### 6. Afeta quem não tem a task

- Ação: com o `_autoRequestTasksOnConnect` desmarcado (sem tasks), passar pela poça.
- Esperado: a lentidão e o escorregão acontecem igual; **nenhuma** penalidade é aplicada
  (decisão da §1).

### 7. Limpar

- Ação: pegar o rodo no almoxarifado, chegar na poça e **segurar** [E].
- Esperado: prompt "Segure [E] para limpar o café"; barra de progresso do `DirtSpot` (plano 0006);
  ao completar, `task_progress … itemId=vazamento-01` → `task_updated … 1/2` → **só então** a poça
  some.

### 8. Sumir a poça com o jogador em cima

- Ação: ficar em cima da poça no momento em que ela é limpa.
- Esperado: a velocidade volta ao normal imediatamente. Se o jogador ficar lento para sempre, o
  `OnDisable` do `SlipperyArea` não restaura (§3) — é falha.

### 9. Concluir volta ao normal

- Ação: limpar a segunda.
- Esperado: `status=completed progress=2`; a cafeteira para de pingar; a copa volta ao estado
  original.

### 10. Ignorar tem custo

- Ação: reiniciar, receber a task e **não** limpar até o prazo vencer.
- Esperado: `task_expired`; `penalty_assigned`; tarefa extra no `MissionListHud`; notificação.
  As poças **continuam lá** (o prazo pune, não limpa).

### 11. Não conflita com a Tarefa 18

- Ação: se o plano 0006 estiver implementado, limpar uma mancha autorada da recepção com o mesmo
  rodo.
- Esperado: funciona igual; as duas tasks contam separado; nenhum `LogError`.

### 12. Cleanup

- Ação: sair do Play Mode com uma poça ativa e o jogador em cima.
- Esperado: nenhum `LogError`; nenhuma poça órfã na hierarquia ao reentrar.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo nesta tarefa
npx tsc --noEmit
```

### Client

Seguir os 12 passos da §6 em Play Mode.

## 8. Out of scope

- **Punir quem escorrega** sem ter a task (ver a decisão na §1). Escorregar é incômodo, não
  penalidade.
- Poça que **cresce** com o tempo, ou vazamento contínuo até ser consertado.
- Consertar a máquina (a tarefa é limpar o chão; a cafeteira volta ao normal ao fim da limpeza).
- Posição aleatória do vazamento — as âncoras são autoradas e o servidor escolhe entre elas por
  id, não por coordenada (§1).
- Sincronizar a poça entre jogadores como estado autoritativo: cada cliente instancia a partir do
  mesmo `world_event`, então convergem — mas se um cliente entrar **depois** do evento, ele não vê
  as poças. Resolver isso exigiria estado de mundo persistente no servidor; está fora desta
  entrega e é o furo conhecido deste plano.
- Dano, queda com animação de ragdoll, ou tempo parado maior que o tropeço curto.
- Rastro de pegadas de café saindo da poça.
- Limpar com pano/vassoura (o rodo é a ferramenta, como no plano 0006).
- Prazo visível separado do `TaskDeadlineHud` genérico do plano 0010.
