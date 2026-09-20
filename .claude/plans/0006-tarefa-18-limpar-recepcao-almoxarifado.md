# Plan 0006 — tarefa-18-limpar-recepcao-almoxarifado

> Origem: `Explicação das tarefas para essa entrega.pdf` — **Tarefa 18 — Limpar a recepção e o
> almoxarifado**. **Depende do plano [0004](0004-infra-carregar-e-validar-itens.md)**.

## 1. Context

A Tarefa 18 introduz a primeira mecânica de **ferramenta** do projeto: o jogador precisa pegar
um utensílio no almoxarifado (rodo ou vassoura) e **segurar [E]** em cima de cada sujeira até
limpá-la. Diferente da Tarefa 16, o item carregado **não é consumido** — a ferramenta continua
na mão e serve para todas as sujeiras compatíveis.

O documento descreve três grupos:

| Grupo | Ferramenta | Sujeiras | Prompt |
| :---- | :--------- | :------- | :----- |
| Manchas de café | rodo (tag `Rodo`) | 2 na recepção | "Pressione [E] para limpar o café" |
| Manchas de água | rodo | 2 na recepção + 2 no almoxarifado | "Pressione [E] para limpar a água" |
| Pegadas de sapato | rodo | recepção + almoxarifado (**quantidade não informada**) | "Pressione [E] para limpar a pegada" |
| Sujeira de chips | vassoura (tag `Vassoura`) | 3 na recepção | "Pressione [E] para varrer o chips" |
| Sujeira de terra | vassoura | recepção + almoxarifado (**quantidade não informada**) | "Pressione [E] para varrer a terra" |
| Bolinhas de papel | — (coleta normal) | 3 na recepção + 5 no almoxarifado | "Aperte [E] para jogar fora o papel" |

As sujeiras são **imagens com box collider `isTrigger`** já posicionadas na cena. As tags `Rodo`
e `Vassoura` **já existem** em `ProjectSettings/TagManager.asset` — foram criadas justamente
para esta tarefa, conforme a observação do documento.

As bolinhas de papel são o caso fácil: é pegar → carregar → jogar no lixo da recepção (inclusive
as 5 do almoxarifado), ou seja, exatamente o `CarryableItem` + `TaskDepositPoint` do plano 0004.

**Contagens a confirmar na cena antes de implementar:** o documento não informa o número de
pegadas nem de manchas de terra, e se contradiz nas bolinhas ("3 na recepção e 5 no almoxarifado"
vs. "Espalhei 5 papéis pela recepção"). O `targetCount` de cada entrada **deve** ser conferido
contando os GameObjects na cena — se o `targetCount` for maior que o número real de sujeiras, a
task fica impossível de concluir.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. As seis entradas são catálogo com `items`, validadas
pelo mecanismo do plano 0004. Só documentação.

**Phase client** — novo `HoldToCleanTarget.cs` (segurar [E] com barra de progresso + gate por tag
de ferramenta); `PlayerCarrier` ganha `CarriedTag` para o gate; seis entradas de catálogo; seis
tipos em `TaskPresentation`; setup de cena das sujeiras, das duas ferramentas, das bolinhas e
do lixo.

### Contratos cross-repo

Nenhum evento novo. Entradas de catálogo (`items` — `targetCount` = `items.Length` sempre):

| taskId | type | targetCount | ferramenta | items |
| :----- | :--- | :---------- | :--------- | :---- |
| `task-limpar-cafe-01`    | `clean_coffee`      | 2 | `Rodo`     | `mancha-cafe-01`, `mancha-cafe-02` |
| `task-limpar-agua-01`    | `clean_water`       | 4 | `Rodo`     | `mancha-agua-01` … `mancha-agua-04` |
| `task-limpar-pegadas-01` | `clean_footprints`  | **TBD** | `Rodo` | `pegada-01` … `pegada-NN` |
| `task-varrer-chips-01`   | `sweep_chips`       | 3 | `Vassoura` | `sujeira-chips-01` … `-03` |
| `task-varrer-terra-01`   | `sweep_dirt`        | **TBD** | `Vassoura` | `sujeira-terra-01` … `-NN` |
| `task-lixo-bolinhas-01`  | `collect_paperballs`| **8 (confirmar)** | — | `bolinha-01` … `bolinha-08` |

## 3. Approach

### Backend

Só documentação em `COMMUNICATION.md`: as seis entradas na tabela de exemplos de catálogo e os
`type` novos. Nenhum `items` precisa de `pairs` — cada sujeira só tem um jeito de ser limpa, e o
gate de ferramenta é decisão de gameplay local (ver "Nota sobre autoridade" abaixo).

**Nota sobre autoridade:** o servidor valida *qual sujeira* foi limpa (`itemId` ∈ `items`, sem
repetição). Ele **não** valida que o jogador estava com o rodo na mão — isso exigiria estado de
inventário no servidor, que está fora do escopo do plano 0004. É uma limitação consciente e
consistente com o resto da entrega: identidade e unicidade são autoritativas; posse de ferramenta
e proximidade são locais.

### Client

#### `PlayerCarrier.cs` (MODIFY)

Expor a tag do item carregado para o gate de ferramenta:

```csharp
public string CarriedTag => Carried != null ? Carried.tag : string.Empty;
public bool IsCarryingTag(string tagName) => Carried != null && Carried.CompareTag(tagName);
```

`CompareTag` e não `tag ==`, conforme `.agents/rules/csharp-coding-standards.md`.

Rodo e vassoura são `CarryableItem` com `_isTool = true` — o `Consume()` do plano 0004 já os
preserva na mão. Trocar de ferramenta usa o `DropCurrent()` já existente: ao pegar a vassoura
com o rodo na mão, o rodo volta para o chão na posição atual.

#### `HoldToCleanTarget.cs` (NEW, `Assets/Scripts/Interactions/`)

`MonoBehaviour` na sujeira. Campos de Inspector:

```csharp
[SerializeField] private string _itemId;          // "mancha-cafe-01"
[SerializeField] private string _taskType;        // "clean_coffee"
[SerializeField] private string _requiredTag;     // "Rodo" | "Vassoura"
[SerializeField] private string _slotId;          // "slot-limpeza-recepcao" (telemetria)
[SerializeField] private string _promptMessage;   // "Pressione [E] para limpar o café"
[SerializeField] private string _missingToolMessage; // "Pegue o rodo no almoxarifado"
[SerializeField] private float  _holdSeconds = 1.5f;
[SerializeField] private Image  _progressFill;    // barra radial/linear do hold
```

Comportamento:

- `OnTriggerEnter`/`OnTriggerExit` controlam `_playerNearby` (o collider `isTrigger` já existe
  no asset).
- Gate para exibir o prompt: `_playerNearby` **e** existe task de `_taskType` em
  `pending`/`in_progress` **e** `PlayerCarrier.IsCarryingTag(_requiredTag)`. Sem a ferramenta,
  exibe `_missingToolMessage` em vez do prompt de limpar — feedback que o documento implica
  ("o jogador pegar o rodo lá no almoxarifado").
- Em `Update`, com o gate aberto: `InteractionInput.InteractHeld()` acumula
  `_heldTime += Time.deltaTime` e atualiza `_progressFill.fillAmount`. Soltar [E] antes do fim
  **zera** o acúmulo (o documento: "para ele limpar precisa ficar pressionando o E").
- Ao atingir `_holdSeconds`: envia
  `TaskSystemBridge.Instance.SendProgress(task.Id, _itemId, _slotId)` e entra em estado
  `_awaitingServer` (não aceita novo hold).
- A mancha só é **desativada** quando chegar `OnTaskUpdated` com esse `taskId` e progresso maior
  que o último visto — mesmo ida-e-volta autoritativo do `TaskDepositPoint`. Em `OnTaskRejected`
  para esse `taskId`, o hold é liberado de novo e a mensagem do servidor é exibida.

Unsubscribe obrigatório em `OnDisable` para `OnTaskUpdated`/`OnTaskRejected`
(`.agents/rules/client-design-pattern.md`).

#### Catálogo e apresentação

`TaskSystemBridge.EnsureCleaningEntries()` no mesmo padrão do plano 0005 (só adiciona o que não
existir no Inspector).

`TaskPresentation` ganha os seis `TYPE_*` e seus ramos:

| type | GetTitle | GetHowTo |
| :--- | :------- | :------- |
| `clean_coffee` | "Limpar as manchas de café" | "Pegue o rodo no almoxarifado e segure [E] sobre cada mancha de café da recepção." |
| `clean_water` | "Limpar as manchas de água" | "Pegue o rodo no almoxarifado e segure [E] sobre cada mancha de água da recepção e do almoxarifado." |
| `clean_footprints` | "Limpar as pegadas" | "Pegue o rodo no almoxarifado e segure [E] sobre cada pegada de sapato." |
| `sweep_chips` | "Varrer os chips" | "Pegue a vassoura no almoxarifado e segure [E] sobre cada sujeira de chips da recepção." |
| `sweep_dirt` | "Varrer a terra" | "Pegue a vassoura no almoxarifado e segure [E] sobre cada sujeira de terra." |
| `collect_paperballs` | "Jogar o lixo fora" | "Recolha as bolinhas de papel da recepção e do almoxarifado e jogue no lixo da recepção." |

`GetActionPrompt` devolve os textos literais do documento, com progresso: ex.
`"Pressione [E] para limpar o café (1/2)"`, `"Aperte [E] para jogar fora o papel (3/8)"`.

#### Cena

- **Ferramentas** (almoxarifado): rodo e vassoura recebem `CarryableItem` com
  `_itemId = "ferramenta-rodo"` / `"ferramenta-vassoura"`, `_kind = "ferramenta"`,
  `_isTool = true`, `_pickupMessage = "Aperte [E] para pegar o rodo"` / `"…a vassoura"`, e as
  tags `Rodo` / `Vassoura` **no próprio GameObject**. Os `_itemId` das ferramentas **não** entram
  em nenhum `items` de catálogo — elas não geram progresso.
- **Sujeiras**: cada imagem recebe `HoldToCleanTarget` com `_itemId` único, `_taskType`,
  `_requiredTag` e `_promptMessage` da tabela. Conferir/aumentar o collider conforme necessário.
- **Bolinhas de papel**: `CarryableItem` (`_kind = "bolinha"`, `_isTool = false`,
  `_pickupMessage = "Aperte [E] para pegar a bolinha de papel"`), incluindo as do almoxarifado.
- **Lixo da recepção**: `TaskDepositPoint` com `_slotId = "slot-lixo-recepcao"`,
  `_acceptedKind = "bolinha"`, `_taskType = "collect_paperballs"`,
  `_promptMessage = "Aperte [E] para jogar fora o papel"`, + `WorldTaskMarker`.
- `WorldTaskMarker` também no rodo e na vassoura, com `_taskType` da task de limpeza
  correspondente — resolve o "onde está a ferramenta" que o documento pressupõe.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (6 entradas de catálogo de limpeza + types novos)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/HoldToCleanTarget.cs   NEW
hora-extra-client/Assets/Scripts/Interactions/PlayerCarrier.cs       MODIFY (CarriedTag, IsCarryingTag)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs      MODIFY (EnsureCleaningEntries — 6 entradas com items)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs              MODIFY (6 TYPE_* + ramos)
hora-extra-client/Assets/Scenes/SCN_Main.unity                       MODIFY (asset, Editor — sujeiras, ferramentas, bolinhas, lixo, markers)
hora-extra-client/Assets/Prefab/PFB_Interactable_Rodo.prefab         NEW (asset, Editor — opcional se o rodo já existir solto na cena)
hora-extra-client/Assets/Prefab/PFB_Interactable_Vassoura.prefab     NEW (asset, Editor — idem)
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo** — sem lógica de servidor. A validação de `items`/dedup usada aqui é
coberta pelos ciclos 1–12 do plano 0004.

Regressão obrigatória: `cd hora-extra-backend && npm test`.

Se a implementação exigir validação de posse de ferramenta no servidor (ver "Nota sobre
autoridade" na §3), **parar** e abrir um plano com ciclos TDD próprios — seria estado de
inventário server-side, mudança estrutural.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_Main.unity` aberta, `UseTestToken = true`, "Clear on Play".
Para isolar uma task específica, esvaziar temporariamente o `_initialCatalog` deixando só ela.

### 0. Conferência de contagem (fazer ANTES de testar)

- Ação: no Hierarchy, contar os GameObjects de pegada, de terra e de bolinha de papel.
- Esperado: o `targetCount` de `task-limpar-pegadas-01`, `task-varrer-terra-01` e
  `task-lixo-bolinhas-01` bate **exatamente** com a contagem, e `items` lista um `itemId` por
  objeto. Se não bater, corrigir o catálogo antes de seguir — a task ficaria impossível.

### 1. Catálogo registrado

- Ação: entrar em Play Mode.
- Esperado: `[NETWORK] task_catalog_register enviado — N tarefa(s)` incluindo as 6 entradas de
  limpeza; nenhum warn de shape no backend.

### 2. Sujeira sem ferramenta

- Pré-condição: task de limpeza atribuída, mãos vazias.
- Ação: chegar perto de uma mancha de café.
- Esperado: prompt exibe `"Pegue o rodo no almoxarifado"`. Segurar [E] **não** faz nada: a barra
  de progresso não enche, nenhum pacote é enviado.

### 3. Pegar o rodo

- Ação: ir ao almoxarifado, chegar no rodo e pressionar [E].
- Esperado: `"Aperte [E] para pegar o rodo"`; `[GAMEPLAY] PlayerCarrier — item 'ferramenta-rodo' pego`;
  o rodo fica na mão.

### 4. Limpar segurando [E]

- Ação: voltar à mancha de café, segurar [E].
- Esperado:
  - prompt `"Pressione [E] para limpar o café (0/2)"`;
  - a barra de progresso enche durante o hold;
  - ao completar: `[NETWORK] task_progress enviado — taskId=task-limpar-cafe-01 itemId=mancha-cafe-01 …`;
  - `[GAMEPLAY] task_updated recebido — … progress=1`;
  - **só então** a mancha some.

### 5. Soltar [E] no meio zera o progresso

- Ação: começar a limpar a segunda mancha e soltar [E] na metade.
- Esperado: a barra **volta a zero**; a mancha continua na cena; nenhum pacote enviado.

### 6. Ferramenta não é consumida

- Ação: após limpar a primeira mancha, olhar para a mão do personagem.
- Esperado: o rodo **continua** na mão (`_isTool = true`); dá para limpar a segunda mancha
  em seguida sem voltar ao almoxarifado.

### 7. Ferramenta errada

- Ação: com o rodo na mão, chegar perto de uma sujeira de chips (exige `Vassoura`).
- Esperado: prompt exibe `"Pegue a vassoura no almoxarifado"`; segurar [E] não limpa.

### 8. Trocar de ferramenta

- Ação: com o rodo na mão, pegar a vassoura.
- Esperado: o rodo volta para o chão na posição atual (`DropCurrent`), a vassoura vai para a mão.
  Nenhum item duplicado, nenhum `LogError`.

### 9. Varrer com a vassoura

- Ação: segurar [E] sobre uma sujeira de chips.
- Esperado: prompt `"Pressione [E] para varrer o chips (0/3)"`; mesmo fluxo do passo 4.

### 10. Bolinhas de papel → lixo

- Ação: pegar uma bolinha (recepção), levar até o lixo da recepção e pressionar [E].
- Esperado: prompt `"Aperte [E] para jogar fora o papel (0/8)"`; `task_progress` com o `itemId`
  da bolinha e `slotId=slot-lixo-recepcao`; bolinha some da mão só após o `task_updated`.

### 11. Bolinhas do almoxarifado contam no mesmo lixo

- Ação: pegar uma bolinha no almoxarifado e levá-la ao lixo **da recepção**.
- Esperado: progresso incrementa normalmente (mesma task, mesmo `slotId`) — comportamento
  explícito no documento.

### 12. Conclusão

- Ação: completar uma das tasks de limpeza até o `targetCount`.
- Esperado: `status=completed`, mensagem do `MissionHud`, `WorldTaskMarker` da ferramenta some,
  prompts da sujeira deixam de aparecer.

### 13. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError` residual; nenhuma ferramenta presa no `_handAnchor`.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test
npx tsc --noEmit
```

### Client

Seguir os passos 0–13 da §6 em Play Mode.

## 8. Out of scope

- Validação server-side de posse de ferramenta e de proximidade (ver "Nota sobre autoridade").
- Animação de varrer/passar o rodo, som e partícula de sujeira.
- Sujeira "parcialmente limpa" persistente entre tentativas — o hold zera ao soltar [E].
- Sujeiras reaparecerem depois de limpas.
- Carregar duas ferramentas ao mesmo tempo (`PlayerCarrier` é single-slot por decisão do plano 0004).
- Ver a ferramenta na mão de outro jogador (sem sincronização de carregamento nesta entrega).
- Sujeiras dinâmicas geradas em runtime.
- Recepção, almoxarifado e encomendas — planos 0005, 0007 e 0008.
