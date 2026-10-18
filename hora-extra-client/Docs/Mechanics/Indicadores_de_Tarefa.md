# Indicadores de Tarefa (UI de Missões)

Conjunto de indicadores no cliente que comunicam ao jogador **quais** são suas tarefas,
**como** executá-las e **onde** ir.

> Desde a HUD nova (ver [`Docs/Arch/HUD.md`](../Arch/HUD.md)) os indicadores são widgets do
> `HudRoot`. `MissionListHud`, `MissionRowView` e `WorldTaskMarker` foram removidos, porque
> nunca chegaram a ser montados em cena. O `MissionHud` antigo continua na cena, mas é
> escondido pelo `HudRoot`.

## Componentes

| Script | Papel |
|--------|-------|
| `UI/TaskPresentation.cs` | Classe estática e fonte única dos textos por `type`: resumo, título, "como fazer", progresso, prompt, categoria (badge) e nome dos itens carregados. Centraliza as constantes de `type`/`status` e os helpers `FindActive`/`IsFinished`. |
| `UI/Hud/Widgets/MissionCardWidget.cs` | Card da tarefa atual com "TAREFA X DE N", badge de categoria, barra de progresso, passo a passo e barra segmentada com todas as tarefas da partida. Segurar **[Tab]** mostra a lista completa. |
| `UI/Hud/Widgets/ObjectiveMarkerWidget.cs` | Marcador do próximo objetivo, com distância. Fora da tela vira seta na borda. |
| `Interactions/DepositHighlight.cs` | Losango dourado sobre o destino que aceita o item na mão (inalterado). |

## De onde vem cada dado

- O progresso e o status são os de `task_assigned` / `task_updated`, como o servidor
  informa (autoritativo).
- O **N** de "Tarefa X de N" vem do campo `totalTasks` de `task_assigned` (atribuídas + fila
  do servidor).
- O **alvo do marcador** é derivado da cena, sem dado de rede:
  1. `DirtSpot.TaskType`: se a tarefa atual é de limpeza, o marcador aponta para a
     ferramenta (`RequiredKind`) e, quando ela já está na mão, para a mancha mais próxima.
  2. `TaskDepositPoint.TaskType`: em tarefa de entrega, aponta para o item solto da
     categoria `AcceptedKind` e, com o item na mão, para o destino.
  3. Na tarefa de café, aponta para a cafeteira.

## Como adicionar um novo tipo de tarefa

1. Em `TaskPresentation`, adicione o `case` do novo `type` em `GetSummary`, `GetTitle`,
   `GetHowTo` e `GetCategory`. Se houver item novo, inclua o nome dele em `GetItemLabel`.
2. Na cena, configure `_taskType` no `TaskDepositPoint` ou no `DirtSpot`. O marcador e o card
   passam a funcionar sozinhos.

## Verificação (Play Mode)

Com o backend ativo (`npm run dev`), acompanhe os logs `[UI]`/`[GAMEPLAY]`:
1. O card mostra "TAREFA 1 DE 4", o passo a passo e a barra de progresso.
2. O marcador aponta para o item. Depois de pegá-lo, passa a apontar para o destino.
3. Cada entrega faz a barra avançar. Ao concluir, o card pulsa em verde, aparece o toast e
   entra a próxima tarefa.
4. Na última tarefa, o card mostra "Todas as tarefas concluídas!".
