# Plan 0005 — tarefa-16-organizar-recepcao

> Origem: `Explicação das tarefas para essa entrega.pdf` — **Tarefa 16 — Organizar a recepção**.
> **Depende do plano [0004](0004-infra-carregar-e-validar-itens.md)** (carregar/entregar + `itemId`
> no `task_progress`). Não iniciar antes dele.

## 1. Context

A Tarefa 16 do documento de entrega é composta por quatro coletas independentes na recepção,
todas com a mesma mecânica: itens espalhados pelo cenário, um recipiente de destino, e o texto
de interação "Aperte [E] para guardar o &lt;item&gt;" que faz o item sumir da mão do jogador e
atualiza a UI da tarefa.

| Sub-tarefa | Itens no cenário | Recipiente |
| :--------- | :--------------- | :--------- |
| Coletar papéis | 4 | caixa de papéis |
| Coletar canetas | 3 | porta-canetas |
| Coletar revistas | 2 | mesa das revistas |
| Coletar latas/chips | 4 | máquina de vendas |

O documento confirma que **todos** os itens e recipientes já têm `BoxCollider` com `isTrigger`
ativado (com a ressalva de que talvez seja preciso aumentar o collider dos itens soltos).

Esta é a primeira tarefa a usar a infra do plano 0004 de ponta a ponta, e ela **substitui** o
coletável legado `MissionPaperCollectible`: hoje esse script conta progresso no instante do
pickup, sem recipiente — exatamente o comportamento que a Tarefa 16 troca por pegar → carregar →
guardar.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma mudança de código. As quatro sub-tarefas são **dados de catálogo**,
não lógica nova: a validação por `items` e a deduplicação por `itemId` já vêm do plano 0004. O
backend só recebe a documentação das entradas novas.

**Phase client** — quatro entradas novas de catálogo no `TaskSystemBridge`; quatro tipos novos em
`TaskPresentation` com título/how-to/prompt em pt-BR; setup de cena dos 13 itens
(`CarryableItem`) e 4 recipientes (`TaskDepositPoint`); `WorldTaskMarker` nos recipientes;
remoção do `MissionPaperCollectible` e da entrada `task-collect-papers-01`.

### Contratos cross-repo

Nenhum evento ou payload novo. As entradas de catálogo abaixo passam a ser enviadas em
`task_catalog_register` usando o campo `items` já documentado pelo plano 0004:

| taskId | type | targetCount | items |
| :----- | :--- | :---------- | :---- |
| `task-recep-papeis-01`   | `collect_papers`    | 4 | `papel-01` … `papel-04` |
| `task-recep-canetas-01`  | `collect_pens`      | 3 | `caneta-01` … `caneta-03` |
| `task-recep-revistas-01` | `collect_magazines` | 2 | `revista-01`, `revista-02` |
| `task-recep-latas-01`    | `collect_cans`      | 4 | `lata-01` … `lata-04` |

`pairs` não é usado aqui: cada sub-tarefa tem um único destino, então basta `items` +
o `_acceptedKind` do recipiente. Os `slotId` (`slot-caixa-papeis`, `slot-porta-canetas`,
`slot-mesa-revistas`, `slot-maquina-vendas`) vão no `task_progress` para log/telemetria, mas
não são validados pelo servidor nesta tarefa.

## 3. Approach

### Backend

Só documentação. Registrar em `COMMUNICATION.md` (seção de catálogo) a tabela das quatro
entradas da recepção como exemplo canônico de task com `items`, e atualizar a lista de valores
conhecidos de `type`.

Os 13 `itemId` são declarados pelo cliente no `task_catalog_register` — o servidor não tem
conhecimento prévio da cena, o que mantém a regra de catálogo-por-sala do plano 0001.

### Client

#### Catálogo (`TaskSystemBridge.cs`)

`EnsurePaperCollectEntry()` (que cria hoje `task-collect-papers-01` com `type="collect"`) é
**removido** e substituído por `EnsureReceptionEntries()`, que garante as quatro entradas acima
com seus `items` preenchidos. `PAPER_COLLECT_TYPE` (`"collect"`) é removido da classe — nenhum
outro script passa a referenciá-lo depois da remoção do `MissionPaperCollectible`.

Manter o padrão atual: as entradas são adicionadas em `Awake` **só se ainda não existirem** no
`_initialCatalog` do Inspector, para que a configuração manual continue tendo precedência.

#### Apresentação (`TaskPresentation.cs`)

Constantes novas e ramos nos quatro métodos, com os textos **literais do documento**:

```csharp
public const string TYPE_COLLECT_PAPERS    = "collect_papers";
public const string TYPE_COLLECT_PENS      = "collect_pens";
public const string TYPE_COLLECT_MAGAZINES = "collect_magazines";
public const string TYPE_COLLECT_CANS      = "collect_cans";
```

| type | GetTitle | GetHowTo | GetActionPrompt (no recipiente) |
| :--- | :------- | :------- | :------------------------------ |
| `collect_papers` | "Organizar os papéis" | "Pegue os 4 papéis espalhados pela recepção e guarde-os na caixa." | "Aperte [E] para guardar o papel (X/4)" |
| `collect_pens` | "Organizar as canetas" | "Pegue as 3 canetas espalhadas pela recepção e coloque-as no porta-canetas." | "Aperte [E] para guardar a caneta (X/3)" |
| `collect_magazines` | "Organizar as revistas" | "Pegue as 2 revistas espalhadas pela recepção e coloque-as de volta na mesa." | "Aperte [E] para guardar a revista (X/2)" |
| `collect_cans` | "Organizar as latas" | "Pegue as 4 latas espalhadas pela recepção e coloque-as na máquina de vendas." | "Aperte [E] para guardar a lata (X/4)" |

O prompt de **pegar** ("Aperte [E] para pegar o papel") é configurado por item no
`CarryableItem._pickupMessage`, não no `TaskPresentation` — o item do chão não conhece a task.

#### Cena `SCN_Main.unity`

13 itens recebem `CarryableItem`:

| GameObject | `_itemId` | `_kind` | `_pickupMessage` |
| :--------- | :-------- | :------ | :--------------- |
| papéis ×4 | `papel-01`…`papel-04` | `papel` | "Aperte [E] para pegar o papel" |
| canetas ×3 | `caneta-01`…`caneta-03` | `caneta` | "Aperte [E] para pegar a caneta" |
| revistas ×2 | `revista-01`, `revista-02` | `revista` | "Aperte [E] para pegar a revista" |
| latas ×4 | `lata-01`…`lata-04` | `lata` | "Aperte [E] para pegar a lata" |

`_isTool = false` em todos (são consumidos na entrega). Conferir e, se necessário, **aumentar o
`BoxCollider`** de cada item conforme a observação do documento — o trigger precisa ser
alcançável com o personagem em pé ao lado.

4 recipientes recebem `TaskDepositPoint` + `WorldTaskMarker`:

| Recipiente | `_slotId` | `_acceptedKind` | `_taskType` | `WorldTaskMarker._taskType` |
| :--------- | :-------- | :-------------- | :---------- | :-------------------------- |
| caixa de papéis | `slot-caixa-papeis` | `papel` | `collect_papers` | `collect_papers` |
| porta-canetas | `slot-porta-canetas` | `caneta` | `collect_pens` | `collect_pens` |
| mesa das revistas | `slot-mesa-revistas` | `revista` | `collect_magazines` | `collect_magazines` |
| máquina de vendas | `slot-maquina-vendas` | `lata` | `collect_cans` | `collect_cans` |

O `WorldTaskMarker` no recipiente resolve o "onde entregar" sem código novo — ele já liga/desliga
sozinho conforme a task do tipo estiver `pending`/`in_progress` (plano 0003).

#### Remoção do coletável legado

`MissionPaperCollectible.cs` é deletado. Antes de deletar, verificar em `SCN_Main.unity` quais
GameObjects o referenciam (são os mesmos 4 papéis) e trocar o componente por `CarryableItem`.
`PaperPromptByDistance.cs` deve ser conferido no mesmo passo: se ele existe só para o prompt do
papel legado, é substituído pelo `InteractionPrompt` do plano 0004; se tiver outro uso na cena,
permanece.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (tabela das 4 entradas de catálogo da recepção + types novos)
```

### Client

```
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs   MODIFY (remove EnsurePaperCollectEntry/PAPER_COLLECT_TYPE; add EnsureReceptionEntries com items)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs           MODIFY (4 TYPE_*, ramos de GetTitle/GetHowTo/GetActionPrompt/GetStatusLabel)
hora-extra-client/Assets/Scripts/UI/MissionPaperCollectible.cs    DELETE (substituído por CarryableItem + TaskDepositPoint)
hora-extra-client/Assets/Scripts/UI/PaperPromptByDistance.cs      REVIEW (deletar se só servia ao papel legado)
hora-extra-client/Assets/Scenes/SCN_Main.unity                    MODIFY (asset, Editor — 13 CarryableItem + 4 TaskDepositPoint + 4 WorldTaskMarker)
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo.** Esta tarefa não adiciona lógica de servidor — é configuração de
catálogo sobre a validação já coberta pelos ciclos 1–12 do plano 0004.

Rodar a suíte completa mesmo assim, como regressão obrigatória da remoção da entrada
`task-collect-papers-01`:

```bash
cd hora-extra-backend && npm test
```

Se durante a implementação aparecer necessidade de lógica nova no servidor, **parar** e abrir um
ciclo TDD — a regra `.agents/rules/backend-unit-tests.md` não abre exceção para "é só dado".

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev`), `SCN_Main.unity` aberta, `SocketManager.UseTestToken = true`,
Console com "Clear on Play". Como o servidor sorteia **3 de N** tasks, pode ser preciso reiniciar
o Play Mode algumas vezes até receber a sub-tarefa desejada — alternativamente, esvaziar
temporariamente o `_initialCatalog` deixando só a entrada em teste.

### 1. Catálogo registrado com items

- Ação: entrar em Play Mode.
- Esperado: `[NETWORK] task_catalog_register enviado — N tarefa(s)` com N incluindo as 4 entradas
  da recepção. Backend não loga warn de shape de `items`. Nenhum `ERROR`.

### 2. Lista de missões explica o que fazer

- Ação: observar o painel `MissionListHud` após o `task_assigned`.
- Esperado: para a sub-tarefa sorteada, título e how-to conforme a tabela da §3 (ex.: "Pegue os 4
  papéis espalhados pela recepção e guarde-os na caixa."), com progresso `0/4`.

### 3. Marcador no recipiente

- Ação: olhar na direção do recipiente da task sorteada.
- Esperado: `WorldTaskMarker` visível **só** no recipiente daquele tipo; os outros 3 permanecem
  ocultos.

### 4. Pegar um item

- Ação: andar até um papel e pressionar [E].
- Esperado: prompt "Aperte [E] para pegar o papel" aparece ao entrar no trigger;
  `[GAMEPLAY] PlayerCarrier — item 'papel-01' pego`; o papel vai para a mão do personagem.

### 5. Recipiente errado não aceita

- Ação: carregando o papel, chegar no porta-canetas.
- Esperado: prompt de guardar **não** aparece (`_acceptedKind` = `caneta` ≠ `papel`). Nenhum
  pacote enviado, nenhum `LogError`.

### 6. Guardar na caixa — progresso autoritativo

- Ação: carregando o papel, chegar na caixa de papéis e pressionar [E].
- Esperado, nesta ordem:
  - prompt "Aperte [E] para guardar o papel (0/4)";
  - `[NETWORK] task_progress enviado — taskId=task-recep-papeis-01 itemId=papel-01 slotId=slot-caixa-papeis`;
  - `[GAMEPLAY] task_updated recebido — … status=in_progress progress=1`;
  - **só então** o papel some da mão;
  - `MissionListHud` mostra `1/4` e `MissionHud` segue coerente.

### 7. Completar a sub-tarefa

- Ação: repetir o ciclo com os 4 papéis.
- Esperado: no quarto, `[GAMEPLAY] task_updated … status=completed progress=4`; mensagem
  "Tarefa concluída com sucesso!" do `MissionHud`; `WorldTaskMarker` da caixa **some**; o prompt
  de guardar deixa de aparecer.

### 8. Repetir as outras três sub-tarefas

- Ação: repetir os passos 4–7 para canetas (3, porta-canetas), revistas (2, mesa) e latas (4,
  máquina de vendas), com os textos do documento em cada prompt.
- Esperado: mesmo comportamento, contadores `3`, `2` e `4` respectivamente.

### 9. Coletável legado removido

- Ação: procurar no Console por logs de `MissionPaperCollectible` e por
  `taskId=task-collect-papers-01`.
- Esperado: **nenhuma** ocorrência. Nenhum `MissingComponent`/`MissingReference` na cena.

### 10. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError` residual; nenhum item preso no `_handAnchor`.

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

- Animação de pegar/guardar, som e partícula de feedback.
- Ordem obrigatória entre as sub-tarefas (as 4 são independentes e sorteáveis isoladamente).
- Agrupar as 4 sub-tarefas como uma única "Tarefa 16" com barra de progresso combinada — cada
  bullet do documento é uma entrada de catálogo autônoma, para que o sorteio de N=3 continue
  fazendo sentido. Se o agrupamento for exigido depois, é um plano de UI separado.
- Itens reaparecerem no cenário após a task ser concluída (respawn).
- Sincronizar entre jogadores quais itens já foram recolhidos — o servidor conta por jogador, e
  dois jogadores na mesma sala coletam a mesma cena independentemente.
- Validação de `slotId` pelo servidor nesta tarefa (só `items`); pareamento é exercitado nos
  planos 0007 e 0008.
- Limpeza, almoxarifado e encomendas — planos 0006, 0007 e 0008.
