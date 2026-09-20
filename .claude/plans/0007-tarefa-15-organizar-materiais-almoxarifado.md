# Plan 0007 — tarefa-15-organizar-materiais-almoxarifado

> Origem: `Explicação das tarefas para essa entrega.pdf` — **Tarefa 15 — Organizar materiais do
> almoxarifado**. **Depende do plano [0004](0004-infra-carregar-e-validar-itens.md)**.
> Mapa completo das 18 tarefas: [README.md](README.md).
>
> **Atualização (lista-mestra `TAREFAS DOS JOGADORES.pdf`).** A lista-mestra descreve a Tarefa 15
> por **categoria** — *"cada material possui uma categoria específica… cada prateleira aceita
> apenas determinados tipos"* — enquanto este plano modela por **par item→slot** (`pairs`). Os
> dois coincidem aqui porque cada categoria tem exatamente um destino; `pairs` é a forma mais
> estrita e não precisa mudar. A modelagem por categoria propriamente dita (várias categorias,
> vários destinos, gaveta com cadeado) aparece na Tarefa 3 — plano
> [0013](0013-tarefa-03-organizar-documentos.md), que generaliza este mesmo campo.
>
> Duas notas de rota:
>
> 1. **Cena.** Referências a `SCN_Main.unity` trocadas por **`SCN_FirstFloor.unity`** (a que está
>    no `EditorBuildSettings`, com `Props_Almoxarifado` e `Estrutura_Almoxarifado` já montados).
> 2. **Reuso do destaque.** O componente de destaque de slot vazio ("ficar chamativo", pedido
>    literal do documento) nasce **aqui** e é reusado pelo `TaskPlacementSlot` do plano
>    [0011](0011-infra-slot-de-posicionamento.md). Mantê-lo como componente próprio, desacoplado
>    do `TaskDepositPoint`, é o que torna esse reuso possível — não embutir a lógica de destaque
>    dentro do destino.

## 1. Context

A Tarefa 15 é a primeira a usar o **pareamento item→destino** (`pairs`) do plano 0004: cada
material espalhado pelo almoxarifado tem um lugar específico onde deve ser guardado, e guardar no
lugar errado não pode contar progresso.

O documento descreve quatro grupos de materiais, todos já com box collider `isTrigger` — **tanto
os itens soltos quanto as caixas de destino**:

| Material | Itens espalhados | Destino |
| :------- | :--------------- | :------ |
| Chave inglesa | 1 (colocar ao lado da que já está lá) | suporte/painel de ferramentas |
| Envelopes | 3 | caixa de envelopes |
| Fitas | 2 | caixa de fitas |
| Toner | 2 | caixa de toner |

O documento acrescenta um requisito explícito de affordance visual:

> *"Se possível para essa tarefa 15, todo lugar que tiver faltando um item ficar chamativo."*

Ou seja, além da mecânica, esta tarefa entrega um **destaque de slot vazio**: enquanto a task
estiver ativa, os lugares que ainda esperam um item pulsam/brilham, e apagam conforme são
preenchidos.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. As quatro entradas usam `pairs`, cuja validação
(`UNKNOWN_ITEM` / `WRONG_SLOT` / `ALREADY_COUNTED`) já vem dos ciclos 6–8 do plano 0004. Só
documentação.

**Phase client** — novo `EmptySlotHighlight.cs` (o "ficar chamativo"); quatro entradas de
catálogo com `pairs`; quatro tipos em `TaskPresentation`; setup de cena dos 8 itens e dos 4
destinos.

### Contratos cross-repo

Nenhum evento novo. Entradas de catálogo — aqui usando **`pairs`**, não `items`:

| taskId | type | targetCount | pairs (itemId → slotId) |
| :----- | :--- | :---------- | :---------------------- |
| `task-almox-chave-01`     | `store_wrench`    | 1 | `chave-inglesa-02` → `slot-chave-inglesa` |
| `task-almox-envelopes-01` | `store_envelopes` | 3 | `envelope-01..03` → `slot-caixa-envelopes` |
| `task-almox-fitas-01`     | `store_tape`      | 2 | `fita-01..02` → `slot-caixa-fitas` |
| `task-almox-toner-01`     | `store_toner`     | 2 | `toner-01..02` → `slot-caixa-toner` |

Quando uma entrada declara `pairs`, o `task_progress` **precisa** mandar `itemId` **e** `slotId`
— sem eles o servidor responde `task_rejected` com `MISSING_ITEM`.

## 3. Approach

### Backend

Só documentação em `COMMUNICATION.md`: as quatro entradas como exemplo canônico de catálogo com
`pairs`, e os `type` novos. Vale registrar na doc a diferença de contrato entre `items` (destino
livre) e `pairs` (destino obrigatório), já que esta é a primeira tarefa a exercitar o segundo.

**Por que `pairs` e não `items` aqui:** com `items`, entregar um toner na caixa de envelopes
contaria progresso — o servidor não teria como saber que o destino estava errado. Com `pairs`, o
servidor rejeita com `WRONG_SLOT`, e o cliente não precisa duplicar a regra de acerto. É o mesmo
mecanismo que o plano 0008 usa para as encomendas.

### Client

#### `EmptySlotHighlight.cs` (NEW, `Assets/Scripts/Interactions/`)

Componente no GameObject do destino (ou num filho visual dele). Faz o lugar "ficar chamativo"
enquanto ainda falta item:

```csharp
[SerializeField] private string _taskType;        // "store_envelopes"
[SerializeField] private GameObject _highlightVisual; // halo/outline/seta — ligado e desligado
[SerializeField] private float _pulseSpeed = 2f;
[SerializeField] private float _minAlpha = 0.25f;
[SerializeField] private float _maxAlpha = 1f;
```

- Segue o padrão do `WorldTaskMarker` (plano 0003): subscribe em
  `TaskSystemBridge.OnTaskAssigned` / `OnTaskUpdated` no `OnEnable`, unsubscribe no `OnDisable`,
  e `Reevaluate()` já no enable para cobrir a task atribuída antes do objeto habilitar.
- `Reevaluate()`: liga `_highlightVisual` quando existe task de `_taskType` em
  `pending`/`in_progress` **e** `task.CurrentProgress < task.TargetCount`. Desliga quando a task
  for `completed`/`failed` ou não existir.
- Em `Update`, com o visual ligado, pulsa o alpha do material entre `_minAlpha` e `_maxAlpha`
  via `Mathf.PingPong` — cache do `Renderer`/`MaterialPropertyBlock` no `Awake`.

**Granularidade do destaque:** o servidor informa *quantos* itens faltam (`currentProgress` vs.
`targetCount`), não *quais* slots foram preenchidos. Para as caixas (envelopes/fitas/toner) isso
basta — é um destino só, que apaga quando a task completa. Para a chave inglesa, `targetCount=1`,
então o destaque também apaga na hora certa. Se no futuro houver N slots visuais distintos
dentro da mesma task, o destaque por slot individual precisará de estado local
(`EmptySlotHighlight` marcando o próprio slot como preenchido ao confirmar a entrega) — anotado,
mas não necessário nesta tarefa.

#### Catálogo (`TaskSystemBridge.cs`)

`EnsureStorageEntries()` no mesmo padrão dos planos 0005/0006 — só adiciona o que não existir no
`_initialCatalog` do Inspector. Aqui as entradas preenchem `pairs` (`List<TaskPairData>`) em vez
de `items`.

#### Apresentação (`TaskPresentation.cs`)

```csharp
public const string TYPE_STORE_WRENCH    = "store_wrench";
public const string TYPE_STORE_ENVELOPES = "store_envelopes";
public const string TYPE_STORE_TAPE      = "store_tape";
public const string TYPE_STORE_TONER     = "store_toner";
```

| type | GetTitle | GetHowTo | GetActionPrompt |
| :--- | :------- | :------- | :-------------- |
| `store_wrench` | "Guardar a chave inglesa" | "Pegue a chave inglesa que está solta no almoxarifado e coloque-a ao lado da outra." | "Aperte [E] para guardar a chave inglesa (X/1)" |
| `store_envelopes` | "Guardar os envelopes" | "Pegue os 3 envelopes espalhados pelo almoxarifado e coloque-os na caixa de envelopes." | "Aperte [E] para guardar o envelope (X/3)" |
| `store_tape` | "Guardar as fitas" | "Pegue as 2 fitas espalhadas pelo almoxarifado e coloque-as na caixa de fitas." | "Aperte [E] para guardar a fita (X/2)" |
| `store_toner` | "Guardar os toners" | "Pegue os 2 toners espalhados pelo almoxarifado e coloque-os na caixa de toner." | "Aperte [E] para guardar o toner (X/2)" |

#### Feedback de destino errado

`TaskDepositPoint` (plano 0004) já trata `OnTaskRejected` devolvendo o item para a mão e exibindo
`payload.Message`. Para esta tarefa, o `_acceptedKind` do destino **já filtra** a maioria dos
erros no cliente (não dá nem para abrir o prompt de guardar um toner na caixa de envelopes). O
`WRONG_SLOT` do servidor é a rede de segurança — testado no passo 7 da §6 desligando
temporariamente o filtro de `_kind`.

#### Cena `SCN_FirstFloor.unity`

8 itens recebem `CarryableItem` (`_isTool = false`):

| GameObject | `_itemId` | `_kind` | `_pickupMessage` |
| :--------- | :-------- | :------ | :--------------- |
| chave inglesa solta | `chave-inglesa-02` | `chave-inglesa` | "Aperte [E] para pegar a chave inglesa" |
| envelopes ×3 | `envelope-01`…`-03` | `envelope` | "Aperte [E] para pegar o envelope" |
| fitas ×2 | `fita-01`, `fita-02` | `fita` | "Aperte [E] para pegar a fita" |
| toners ×2 | `toner-01`, `toner-02` | `toner` | "Aperte [E] para pegar o toner" |

4 destinos recebem `TaskDepositPoint` + `EmptySlotHighlight` + `WorldTaskMarker`:

| Destino | `_slotId` | `_acceptedKind` | `_taskType` |
| :------ | :-------- | :-------------- | :---------- |
| suporte da chave | `slot-chave-inglesa` | `chave-inglesa` | `store_wrench` |
| caixa de envelopes | `slot-caixa-envelopes` | `envelope` | `store_envelopes` |
| caixa de fitas | `slot-caixa-fitas` | `fita` | `store_tape` |
| caixa de toner | `slot-caixa-toner` | `toner` | `store_toner` |

A chave inglesa que **já está** na cena (a de referência) **não** recebe `CarryableItem` — só a
que falta é pegável, conforme o documento ("colocar mais uma chave inglesa ao lado dessa").

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (4 entradas com pairs + types novos + nota items vs pairs)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/EmptySlotHighlight.cs   NEW
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs       MODIFY (EnsureStorageEntries — 4 entradas com pairs)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs               MODIFY (4 TYPE_* + ramos)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                        MODIFY (asset, Editor — 8 CarryableItem + 4 destinos com highlight)
hora-extra-client/Assets/Graphics/MAT_SlotHighlight.mat               NEW (asset, Editor — material do halo pulsante)
```

O prefixo `MAT_` segue `.agents/rules/unity-asset-management.md`.

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo** — sem lógica de servidor. O caminho `pairs` → `WRONG_SLOT` usado aqui é
coberto pelos ciclos 6–8 do plano 0004; `MISSING_ITEM` pelo ciclo 3.

Regressão obrigatória: `cd hora-extra-backend && npm test`.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`, "Clear on Play".
Para isolar uma task, esvaziar temporariamente o `_initialCatalog` deixando só ela.

### 1. Catálogo com pairs

- Ação: entrar em Play Mode.
- Esperado: `[NETWORK] task_catalog_register enviado — N tarefa(s)` incluindo as 4 entradas do
  almoxarifado. Backend **não** loga warn de shape de `pairs` (ciclo 22 do plano 0004).

### 2. Destaque do slot vazio ("ficar chamativo")

- Pré-condição: task `store_envelopes` atribuída, nenhum envelope guardado.
- Ação: olhar para a caixa de envelopes.
- Esperado: o halo de `EmptySlotHighlight` está **ligado e pulsando**. Os destinos das tasks
  **não** atribuídas estão apagados.

### 3. Pegar um envelope

- Ação: chegar num envelope e pressionar [E].
- Esperado: `"Aperte [E] para pegar o envelope"`;
  `[GAMEPLAY] PlayerCarrier — item 'envelope-01' pego`; envelope na mão.

### 4. Caixa errada não abre prompt

- Ação: carregando o envelope, chegar na caixa de toner.
- Esperado: prompt de guardar **não** aparece (`_acceptedKind` diverge). Nenhum pacote enviado.

### 5. Guardar no destino certo

- Ação: levar o envelope até a caixa de envelopes e pressionar [E].
- Esperado, nesta ordem:
  - `"Aperte [E] para guardar o envelope (0/3)"`;
  - `[NETWORK] task_progress enviado — taskId=task-almox-envelopes-01 itemId=envelope-01 slotId=slot-caixa-envelopes`;
  - `[GAMEPLAY] task_updated recebido — … progress=1`;
  - **só então** o envelope some da mão;
  - `MissionListHud` mostra `1/3`.

### 6. Destaque apaga ao concluir

- Ação: guardar os 3 envelopes.
- Esperado: no terceiro, `status=completed`; o halo de `EmptySlotHighlight` **apaga**; o
  `WorldTaskMarker` da caixa some; o prompt deixa de aparecer.

### 7. WRONG_SLOT — rede de segurança do servidor

- Pré-condição: **temporariamente** setar `_acceptedKind` da caixa de toner para `envelope`, para
  furar o filtro local de propósito.
- Ação: guardar um envelope na caixa de toner.
- Esperado: `[NETWORK] task_rejected — code=WRONG_SLOT taskId=task-almox-envelopes-01`; o
  envelope **volta para a mão**; o contador **não** muda; a mensagem do servidor aparece no prompt.
- Pós-condição: **reverter** o `_acceptedKind` da caixa de toner.

### 8. ALREADY_COUNTED

- Ação: reativar pelo Inspector um envelope já guardado, pegar e guardar de novo.
- Esperado: `task_rejected — code=ALREADY_COUNTED`; item volta para a mão; contador inalterado.

### 9. Chave inglesa (targetCount = 1)

- Ação: pegar a chave inglesa solta e colocá-la no suporte.
- Esperado: `"Aperte [E] para guardar a chave inglesa (0/1)"`; a task vai direto de `pending` a
  `completed` num único `task_progress`; o destaque apaga. A chave de **referência** que já
  estava lá continua não-interativa.

### 10. Fitas e toners

- Ação: repetir os passos 3–6 para as 2 fitas e os 2 toners.
- Esperado: mesmo comportamento com contadores `2` e `2`.

### 11. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError` residual; nenhum item preso no `_handAnchor`; nenhum halo ligado.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test
npx tsc --noEmit
```

### Client

Seguir os 11 passos da §6 em Play Mode.

## 8. Out of scope

- Destaque por slot **individual** dentro de uma mesma task (ver "Granularidade do destaque" na
  §3) — só é necessário se um destino passar a ter N posições visuais distintas.
- Snap visual do item na posição exata do slot (o item é desativado ao ser guardado, não
  reposicionado dentro da caixa).
- Animação de guardar, som e partícula.
- Ordem obrigatória entre os 4 grupos de material.
- Itens reaparecerem após a conclusão da task.
- Rotacionar o item carregado para inspecioná-lo — plano 0008.
- Validação server-side de proximidade ao destino (só identidade e pareamento são autoritativos).
- Recepção, limpeza e encomendas — planos 0005, 0006 e 0008.
