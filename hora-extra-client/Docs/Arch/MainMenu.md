# Menu Principal

O menu da `MainMenuScene` é **gerado por código**, com o mesmo tema da HUD in-game
(`SO_HudTheme`). Na cena há um GameObject `MainMenu` com o componente `MainMenuRoot`. Ele cria
o Canvas, garante o `EventSystem` e o `SessionManager`, e monta todas as seções.

O menu antigo (Canvas com o `MainMenuController`) **continua na cena**. O `MainMenuRoot` só o
desativa em runtime; desmarque `_hideLegacyMenu` para trazê-lo de volta.

## Layout

```
 HORA·EXTRA                                         ┌ perfil ─────────┐
 O expediente acabou. Você ainda não.               │ Apelido [Ana  ] │
                                                    │ [Entrar][Criar] │
 ▌ JOGAR AGORA        como convidado                └─────────────────┘
   SALAS ONLINE       (requer login)                     ◷ relógio
   COMO JOGAR
   CONFIGURAÇÕES
   CRÉDITOS
   SAIR
 ● Servidor online · 32 ms · Homelab            v0.1.0 · TCC · PUC Minas
```

| Seção (`UI/Menu/Sections/`) | Papel |
|---|---|
| `MenuBackdrop` | Gradiente, faixas de persiana à deriva, lâmpada que falha, relógio de parede passando das 18:00 e vinheta. Tudo gerado em código. |
| `MainColumn` | Logo, tagline e as ações. *Jogar agora* fica bloqueado com o servidor offline ou durante a conexão. *Salas online* exige login. |
| `ProfileCard` | Sem login: campo de **apelido** e os botões Entrar / Criar conta. Com login: nome da conta e Sair da conta. |
| `MenuFooter` | Faz o health check do servidor a cada 5 s, com timeout de 3 s e sem spam de log. Mostra também a versão do jogo. |
| `HowToPlayPanel` / `SettingsPanel` / `CreditsPanel` | Painéis laterais (`MenuPanel`) que deslizam da direita e fecham com **Esc**, gamepad B, "Voltar" ou clique fora. |

## Arquivos

- `UI/Menu/MainMenuRoot.cs`: a raiz. Concentra as ações (`PlayAsGuest`, `LoadScene`,
  `Logout`, `Quit`), a pilha de painéis e o estado compartilhado (`ServerOnline`, `IsBusy`).
- `UI/Menu/MenuFactory.cs`: controles interativos (`Button`, `Slider`, `Toggle`, `Input`,
  `Cycler`, `FieldRow`, `SectionTitle`) construídos sobre o `HudFactory`.
- `UI/Menu/MenuButton.cs`: `Selectable` com destaque animado e o estado "indisponível + motivo".
  Passar o mouse por cima também seleciona o botão, para mouse e teclado não destacarem dois
  botões ao mesmo tempo.
- `UI/Menu/MenuPanel.cs`: base dos painéis laterais.
- `UI/Menu/MenuSprites.cs`: gradiente, vinheta e brilho, gerados em runtime.
- `Settings/GameSettings.cs`: preferências salvas em PlayerPrefs (ver abaixo).
- `Network/GuestPlayFlow.cs`: o fluxo "jogar como convidado", compartilhado com o
  `MainMenuController` legado.

## Preferências (`GameSettings`)

| Preferência | Onde vale |
|---|---|
| `Nickname` | `playerName` no `CONN` (`SocketManager.SendHandshake`). Prioridade: nome da conta logada → apelido → `Player_###`. É o nome visto no painel de equipe e nos toasts da HUD. |
| `MasterVolume` | `AudioListener.volume` |
| `MusicVolume` | Multiplica o volume de cada `BackgroundMusic` e é aplicada na hora. |
| `MouseSensitivity` | Multiplica o `_lookSensitivity` do `PlayerController` (1 = valor do prefab). |
| `Fullscreen` / `Resolution` | `Screen`. Só na build; no Editor são ignoradas. |
| `ServerPreset` | `BackendConfig.ApplyPreset`: homelab, demo ou local. Vale a partir da próxima conexão. |

Tudo é aplicado no boot (`RuntimeInitializeOnLoadMethod`). Quem precisa reagir em tempo real
assina `GameSettings.OnChanged`.

## Servidores

Os presets ficam em `BackendConfig.Presets`. Sem escolha salva, vale o `BackendConfig.Host` do
código. Para adicionar um servidor, inclua uma entrada nessa lista; ela aparece sozinha em
Configurações → Servidor.

## Adicionar um painel

1. Crie `UI/Menu/Sections/MeuPainel.cs` herdando de `MenuPanel`. Implemente `Title` e
   `BuildContent()`, preenchendo `Content` com `MenuFactory` / `HudFactory`.
2. Registre o painel em `MainMenuRoot.Awake` com `AddPanel<MeuPainel>("Panel_Meu")`.
3. Abra com `Menu.OpenPanel<MeuPainel>()`.

## Pendências conhecidas

- O `LobbyController` ainda carrega a `SampleScene` (o protótipo) ao entrar numa sala. O fluxo
  de usuário logado não prepara a rede que o `SCN_FirstFloor` espera, então só trocar a cena
  não basta. Fica para um plano próprio.
