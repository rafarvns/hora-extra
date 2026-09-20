# Indicadores de Tarefa (UI de Missões)

Conjunto de indicadores no cliente que comunicam ao jogador **quais** são suas tarefas,
**como** executá-las e **onde** ir. Todos os dados vêm do que o servidor já envia em
`task_assigned` / `task_updated` — **nenhuma mudança de rede** foi necessária.

## Componentes

| Script | Local | Papel |
|--------|-------|-------|
| `TaskPresentation` | `Assets/Scripts/UI/TaskPresentation.cs` | Classe estática: fonte única dos textos (título, "como fazer", progresso, prompt) por `type`. Centraliza as constantes de `type`/`status`. |
| `MissionListHud` | `Assets/Scripts/UI/MissionListHud.cs` | Painel sempre visível com uma linha por tarefa do jogador local. Observer de `OnTaskAssigned`/`OnTaskUpdated`. |
| `MissionRowView` | `Assets/Scripts/UI/MissionRowView.cs` | View de uma linha (título, instrução, progresso/status), com cor por status. |
| `WorldTaskMarker` | `Assets/Scripts/Characters/WorldTaskMarker.cs` | Ícone flutuante sobre o objeto-alvo enquanto a task do tipo configurado está ativa. |

Edições: `CoffeeMakerInteraction` e `MissionPaperCollectible` agora exibem prompts
descritivos (`"Pressione E para preparar o café"`, `"Pressione E para coletar documento (2/4)"`)
via `TaskPresentation.GetActionPrompt`.

## Como adicionar instrução para um novo tipo de tarefa

Edite `TaskPresentation` e adicione o `case` do novo `type` em `GetHowTo`, `GetTitle` e
`GetActionPrompt`. O painel e os prompts passam a refletir automaticamente.

## Setup no editor Unity

- **Painel:** Panel com `VerticalLayoutGroup` no Canvas do HUD; prefab `PFB_MissionRow`
  (com `MissionRowView` e os 3 `Text`); objeto com `MissionListHud` ligando
  `_rowsContainer` + `_rowPrefab` (e, opcional, `_panelRoot`).
- **Marcadores:** sprite `SPR_TaskMarker`; `WorldTaskMarker` + filho com o ícone no
  `PFB_Interactable_CoffeeMaker` (`_taskType = "coffee_maker"`) e em cada documento /
  prefab do papel (`_taskType = "collect"`).
- **Prompt da cafeteira:** ligar `_promptText` do `CoffeeMakerInteraction` ao `Text`
  dentro do `_promptUI`.

## Verificação (Play Mode)

Com o backend ativo (`npm run dev`), checar logs `[UI]/[GAMEPLAY]`:
1. Painel aparece ao entrar, listando as tasks com título + como fazer + progresso.
2. Marcadores visíveis sobre cafeteira/documentos enquanto pendentes; somem ao concluir.
3. Coletar documento → prompt com `(X/4)` e a linha do painel atualiza o progresso.
4. Concluir o café → linha vira "Concluída" (verde) e o marcador some.
