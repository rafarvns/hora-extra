# Tarefas da recepção e do almoxarifado (1º andar)

Cena: `SCN_FirstFloor.unity`. Todas as entradas ficam no `_initialCatalog` do `TaskSystemBridge`
dessa cena (o único catálogo do jogo). Nenhum evento de rede novo: tudo usa `task_progress` com
`itemId`, validado pelo `TaskService` do servidor (item pertence à tarefa, sem repetição).

| Tarefa | `type` | `id` no catálogo | Itens | Destino / ferramenta | Som |
| :----- | :----- | :--------------- | :---- | :------------------- | :-- |
| Organizar os papéis | `collect_papers` | `task-collect-papers-01` | `papel-01..04` | `slot-caixa-papeis` | `SFX_PaperCollect` |
| Organizar as canetas | `collect_pens` | `task-collect-pens-01` | `caneta-01..02` | `slot-porta-canetas` | `SFX_Pen` |
| Organizar as revistas | `collect_magazines` | `task-collect-magazines-01` | `revista-01..02` | `slot-mesa-revistas` | `SFX_Magazine` |
| Limpar as manchas (rodo) | `clean_floor` | `task-limpar-chao-01` | `mancha-agua-01..04`, `mancha-cafe-01..02` | rodo (`ferramenta-rodo`) | `SFX_Squeegee` |
| Varrer terra e chips | `sweep_floor` | `task-varrer-chao-01` | `sujeira-terra-01..04`, `sujeira-chips-01..03` | vassoura (`ferramenta-vassoura`) | `SFX_Broom` |
| Latas na máquina de vendas | `return_cans` | `task-maquina-vendas-01` | `lata-01..04` | `slot-maquina-vendas` | `SFX_Machine` |
| Caixas na estante | `shelve_boxes` | `task-caixas-estante-01` | `caixa-01..03` | `slot-estante-almoxarifado` | `SFX_CardboardBox` |

## Varrer (vassoura)

- A vassoura (`Vassoura`, modelo `SM_Env_Broom`) é um `CarryableItem` com `_isTool: 1` e
  `_kind: vassoura`, igual ao rodo: não é consumida e fica na mão entre uma sujeira e outra.
  Rodo e vassoura não empilham: com uma ferramenta na mão, o prompt da outra não aparece, e é
  preciso largar a atual com `[Q]` antes.
- As 7 sujeiras com a tag `Vassoura` (`Sujeira_Terra`, `Sujeira_Terra2..4`, `Sujeira_Chips`,
  `Sujeira_Chips2..3`) têm `DirtSpot` com `_requiredKind: vassoura`. É preciso segurar `[E]` por
  1,8 s. Com o rodo na mão, o prompt avisa "Voce precisa da vassoura para limpar isso".
- As 12 `Sujeira_Pegada` (tag `Rodo`) ainda não fazem parte de nenhuma tarefa.

## Latas na máquina de vendas

- `Lata`, `Lata2..4` ganharam um gatilho de ~1,2 m (o modelo tem escala 0,015, por isso o
  `BoxCollider` local tem 80 unidades) e um `CarryableItem` (`_kind: lata`). Dá para empilhar
  várias latas na mão.
- A `Máquina_Vendas` recebeu gatilho, `TaskDepositPoint` (`slot-maquina-vendas`) e o losango
  `DepositHighlight`.
- Os `Sujeira_Chips` são migalhas de chão e entram na tarefa da vassoura, não na da máquina,
  como a tag da Alice indica.

## Caixas na estante

- Três caixas soltas no chão do almoxarifado (`SM_Env_CardboardBox4`, `SM_Env_CardboardBox3` e
  `SM_Env_CardboardBox5`, as que ficam no meio da sala) são `CarryableItem` (`_kind: caixa`).
  O `Static` delas foi desligado, porque objeto estático não se move ao ser carregado.
- O destino é a `SM_Env_MetalShelf (4)`, na parede do fundo, embaixo da placa do almoxarifado.
- A estante tem três caixas desligadas na prateleira (`Caixa_Estante_01..03`), ligadas por
  `TaskDepositPoint._placedVisuals`: a n-ésima aparece quando a sala tem n caixas entregues
  (`TaskSystemBridge.CountCollectedOfType`). Como lê o estado da sala, o colega e quem entra no
  meio da partida veem o mesmo.

## Textos

Título, resumo, "como fazer" e categoria de cada tipo ficam em `UI/TaskPresentation.cs`. As
tarefas novas usam as categorias LIMPEZA (`sweep_floor`) e ORGANIZAÇÃO (`return_cans`,
`shelve_boxes`).
