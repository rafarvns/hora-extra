# HUD in-game

A HUD da partida é **gerada por código** a partir de um tema editável. Na cena há só um
GameObject `HUD` com o componente `HudRoot`. Ele cria o próprio Canvas, os componentes de
dados e todos os widgets.

## Layout (referência 1920×1080, `CanvasScaler` match 0,5)

```
┌──────────────────────────────────────────────────────────────────────┐
│ [Tarefa atual]            [ 14:35 ]                 [● 42 ms]        │
│  TAREFA 2 DE 4 [ORG.]   faltam 3h 25min            [EQUIPE        ]  │
│  Organizar os papéis    ▬▬▬▬▬▬▬░░░░░               [ Ana (você) 1/4]  │
│  ▬▬▬▬░░░░ 2/4                                      [ Bia        2/4]  │
│  passo a passo…                                                       │
│  ▬ ▬ ▬ ▬                                            [toast ✓       ]  │
│                              ◉ 12 m (marcador)      [toast i       ]  │
│                                                                       │
│                      ( [E] Aperte para pegar )                        │
│                           FÔLEGO ▬▬▬▬▬░░                [P] Papéis   │
│ [E] Interagir [Q] Soltar [Shift] Correr [Tab] Tarefas      3/6       │
└──────────────────────────────────────────────────────────────────────┘
```

| Widget (`UI/Hud/Widgets/`) | Mostra | Fonte de dados |
|---|---|---|
| `ShiftClockWidget` | Hora do expediente, tempo restante e barra do dia. Fica âmbar perto do fim e vermelho no fim. | `ShiftClock` (evento `shift_clock`) |
| `MissionCardWidget` | Tarefa atual, progresso, passo a passo, "X de N" e lista completa com [Tab] | `TaskSystemBridge` |
| `TeamWidget` | Jogadores da sala, com a tarefa atual e o placar de cada um | `RoomRoster` (`room_roster`) + `TaskSystemBridge.GetTasksOf` |
| `ToastWidget` | Avisos: tarefa nova, concluída ou falha; colega entrou ou saiu; expediente; conexão | eventos acima + `SocketManager.IsConnected` |
| `PromptWidget` | Pílula "[E] …" com anel de progresso ao segurar | `InteractionPrompt.OnPromptChanged` |
| `CarrySlotWidget` | Item na mão + "3/6" (âmbar quando cheio), ferramenta | `PlayerCarrier` |
| `StaminaWidget` | Barra de fôlego, que some quando está cheia | `PlayerController` |
| `KeyHintsWidget` | Teclas. O [Q] só aparece com algo na mão. | `PlayerCarrier` |
| `ObjectiveMarkerWidget` | Marcador ou seta na borda até o próximo alvo | cena (ver `Docs/Mechanics/Indicadores_de_Tarefa.md`) |
| `NetworkStatusWidget` | Ponto de conexão + latência | `SocketManager.Latency` |

O console de log de debug (`NetworkUI`) começa escondido. **[F3]** mostra e esconde.

## Arquivos

- `UI/Hud/HudRoot.cs` é a raiz. No Inspector cada widget pode ser ligado ou desligado, e há a
  opção `_hideLegacyHud`.
- `UI/Hud/HudTheme.cs` define o tema (ScriptableObject). O asset fica em
  `Assets/Resources/UI/SO_HudTheme.asset`.
- `UI/Hud/HudFactory.cs` reúne os blocos de construção (painel, label TMP, barra, keycap,
  layouts).
- `UI/Hud/HudSprites.cs` gera os sprites em runtime (retângulo arredondado 9-slice, círculo e
  anel). Não há arte de UI no projeto.
- `UI/Hud/HudWidget.cs` é a base dos widgets.
- `UI/Hud/ShiftClock.cs` e `UI/Hud/RoomRoster.cs` são componentes de dados de rede.
- `UI/Hud/HudInput.cs` trata o [Tab] e o [F3] (Input System novo e legado).

## Mudar o visual

Selecione `Assets/Resources/UI/SO_HudTheme` e edite cores, fonte TMP, tamanhos, arredondamento
(`CornerSharpness`) e margem. Nenhum widget usa cor fixa no código. Para trocar a fonte,
atribua um `TMP_FontAsset` em `Font`. Vazio, o tema usa o LiberationSans SDF.

## Adicionar um widget

1. Crie `UI/Hud/Widgets/MeuWidget.cs` herdando de `HudWidget`.
2. Monte a UI em `Build()` usando `HudFactory`. `Root` é o RectTransform do widget e `Theme`
   é o tema.
3. Assine eventos em `OnEnable` e cancele em `OnDisable`. O `HudRoot` só ativa o widget depois
   do `Build()`, então a UI já existe quando o `OnEnable` roda.
4. Registre o widget em `HudRoot.Awake` com `Add<MeuWidget>("Nome")` (e um toggle
   `[SerializeField]`).

Para publicar um aviso de qualquer script, use
`HudRoot.Toast(ToastWidget.Kind.Info, "Título", "texto opcional")`.

## HUD legada

O `HudRoot.Start` chama:
- `MissionHud.HideLegacyVisuals()`;
- `StaminaBarUI.HideLegacyVisuals()`;
- `NetworkUI.SetPingVisible(false)`.

Ele também liga `InteractionPrompt.UseHudRenderer`.

O `InteractionPrompt` **continua ativo**, porque `CarryableItem`, `DirtSpot` e
`TaskDepositPoint` o encontram com `FindFirstObjectByType` e usam as coroutines dele. Só o
`Text` antigo deixa de ser exibido. Desmarcar `_hideLegacyHud` traz a HUD antiga de volta.

## Ver também

- [`MainMenu.md`](MainMenu.md): o menu principal usa o mesmo tema (`SO_HudTheme`) e os mesmos blocos (`HudFactory`, `HudSprites`).
