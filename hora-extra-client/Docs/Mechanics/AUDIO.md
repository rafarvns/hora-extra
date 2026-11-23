# Efeitos sonoros (SFX)

Clipes em `Assets/Musics/SFX/`, separados por área: `Menu/`, `Recepção_Almoxarifado/`, `RH/` e
`Financeiro/`. A música de fundo é outro sistema (`Audio/BackgroundMusic.cs`, volume em
`GameSettings.MusicVolume`).

## Como toca

Todo SFX passa por `Audio/InteractionAudio.PlayOneShot(clip, volume)`:

- toca em 2D, porque é feedback da ação do jogador e não um som posicionado no mundo;
- usa um GameObject próprio, criado no primeiro uso e mantido entre cenas
  (`DontDestroyOnLoad`). O item que gerou o som costuma ser desativado no mesmo frame, e o clique
  de "Criar sala" toca junto com a troca para o 1º andar;
- clipe vazio não faz nada. Todo campo de som é opcional.

**Só quem fez a ação ouve.** Os sons tocam na confirmação do servidor **do jogador local**
(`task_updated` com o `playerId` local). Quando o colega conclui algo, o mundo muda (o item some,
o cofre aparece aberto, a caixa aparece na estante), mas sem som.

## Mapa de sons

| Clipe | Quando toca | Onde está ligado |
| :---- | :---------- | :--------------- |
| `SFX_ClickMenu` | Clique em qualquer botão do menu | `MainMenuRoot._clickSound` → `MenuButton.ClickSound` (MainMenuScene) |
| `SFX_Broom` | Cada sujeira varrida (terra e chips) | `DirtSpot._somLimpeza`, 7 sujeiras com `_requiredKind: vassoura` (SCN_FirstFloor) |
| `SFX_Squeegee` | Cada mancha limpa com o rodo | `DirtSpot._somLimpeza`, 6 manchas (SCN_FirstFloor) |
| `SFX_Machine` | Cada lata colocada na máquina de vendas | `TaskDepositPoint._depositSound` em `slot-maquina-vendas` |
| `SFX_PaperCollect` | Cada papel guardado na caixa | `TaskDepositPoint._depositSound` em `slot-caixa-papeis` |
| `SFX_Pen` | Cada caneta guardada no porta-canetas | `TaskDepositPoint._depositSound` em `slot-porta-canetas` |
| `SFX_Magazine` | Cada revista devolvida à mesa | `TaskDepositPoint._depositSound` em `slot-mesa-revistas` |
| `SFX_CardboardBox` | Cada caixa guardada na estante | `TaskDepositPoint._depositSound` em `slot-estante-almoxarifado` |
| `SFX_Paper` | Cada bolinha de papel que cai na lixeira | `TrashBin._hitSound`, 4 lixeiras (SCN_SecondFloor) |
| `SFX_Password` | Cada dígito no teclado do cofre | `SafeInteraction._somSenha` (SCN_ThirdFloor) |
| `SFX_Cash` | Início do recolhimento do dinheiro | `SafeInteraction._somRecolher` (SCN_ThirdFloor) |
| `SFX_Click` | Clique em "Enviar email" | `EmailComputer._somClique` (SCN_ThirdFloor) |

O som de cada tarefa toca quando ela **avança** (entrega ou limpeza confirmada). Pegar um item
não tem som (`CarryableItem._pickupSound` vazio), para a mesma ação não tocar duas vezes.

`SFX_Coffee` (raiz de `SFX/`) não está ligado: a tarefa da cafeteira só existe na `SampleScene`,
que não entra na build. O elevador tem som próprio (`ElevatorTransition`, ver
[ELEVADOR.md](ELEVADOR.md)).

## Ligar um som novo

1. Coloque o clipe na pasta da área, com o prefixo `SFX_`.
2. Se o interagível já tem campo de som (`_pickupSound`, `_depositSound`, `_somLimpeza`,
   `_hitSound`, `_somRecolher`, `_somSenha`, `_somClique`), basta arrastar o clipe no Inspector.
3. Se não tem, adicione `[SerializeField] private AudioClip _som...;` e chame
   `InteractionAudio.PlayOneShot(_som..., _volume)` no momento confirmado pelo servidor, não no envio.
   Painéis criados em runtime recebem o clipe por parâmetro de quem os abre.
4. Atualize a tabela acima.

Não existe volume separado para SFX. Os efeitos seguem o `MasterVolume` (`AudioListener.volume`).
