# Tarefas do Financeiro (3º andar)

Origem: `Explicação das tarefas para essa entrega.pdf`, págs. 12–14. Cena: `SCN_ThirdFloor.unity`
(sala `Props_SalaCofre` e escritório `Props_Escritório`).

| Tarefa | `type` | `id` no catálogo | Progresso |
| :----- | :----- | :--------------- | :-------- |
| 6 — Desbloquear os cofres e recolher o dinheiro | `open_safe_collect_money` | `task-cofres-01` | 2 itens: `cofre-direito`, `cofre-esquerdo` |
| 7 — Enviar arquivos/e-mails pelo computador | `send_email_upload` | `task-enviar-email-01` | start → complete (1/1) |

As duas entradas ficam no `_initialCatalog` do `TaskSystemBridge` da `SCN_FirstFloor` (o único
catálogo do jogo; ele persiste entre andares). Nenhum evento de rede novo — ver
`hora-extra-backend/docs/Networking/COMMUNICATION.md`, §5 "Exemplos: tarefas do Financeiro".

## Tarefa 6 — Cofres

Componentes (em `Interacao_Cofre_Senha*_Gerado`, criados pelo setup do Editor):

- **`SafeInteraction`** — o fluxo inteiro:
  1. Trancado + `[E]` → abre o **`SafeKeypadPanel`** (teclado: `[0-9]`, `[Backspace]`,
     `[Enter]`, `[Esc]`). O personagem fica parado enquanto digita.
  2. Senha errada → "Senha incorreta", limpa e deixa tentar de novo (sem limite, sem rede).
  3. Senha certa → a porta abre (**`SafeDoor`**) e o prompt vira "Aperte [E] para recolher o
     dinheiro".
  4. `[E]` → **~10 s ocupado** (`_segundosRecolhendo`): barra no prompt, sem andar, sem câmera,
     sem `[E]/[Q]`/clique em outros objetos.
  5. Fim da barra → `task_progress { itemId: cofre-direito|cofre-esquerdo, slotId: "cofre" }`.
     O dinheiro (`_dinheiroVisual`) só some quando o servidor confirma.
- **`SafeDoor`** — gira a porta (filho `Door` do FBX) em torno da dobradiça `Dobradica`.
  Se abrir para o lado errado, inverta o sinal de `_angulo` ou mova o `Dobradica` no Inspector
  (gizmo amarelo com o objeto selecionado). Objetos de cena colados na porta (o teclado
  numérico, por exemplo) ficam em `_acompanham` e giram junto. O setup os encontra sozinho
  pela posição na frente da porta.

Senhas: **direita = 1512**, **esquerda = 2803** (campo `_codigo`). **Temporário:** as senhas
aparecem também no "como fazer" da tarefa (`TaskPresentation.GetHowTo`), porque os post-its
estão ilegíveis na cena (cadeira na frente). Tirar quando os post-its forem reposicionados. O setup decide qual cofre é
"direito" olhando de frente para a porta. Se ficar invertido em relação aos post-its, troque
`_itemId` e `_codigo` entre os dois no Inspector.

**Decisão de escopo:** a senha e os 10 s são conferidos só no cliente. O servidor garante que
cada cofre conta uma vez e que só os dois cofres contam. Validar senha no servidor é o caminho
do plano `0009` (etapas com `expects`), se um dia for preciso.

## Tarefa 7 — Enviar e-mail

- **`EmailComputer`** (em `Interacao_Email_PC9_Gerado`, junto do `PC (9)`, o da tela de e-mail):
  `[E]` abre a tela, solta o cursor e trava o personagem. Se a tarefa estiver `pending`, envia
  `task_start_interaction`.
- **`EmailUploadPanel`** — estilo "Upload Data": pastas *Financeiro* → *Diretoria*, botão
  **"Enviar email"** (ou `[Enter]`), barra de ~6 s (`_segundosEnvio`) com o documento voando entre
  as pastas. Barra cheia → `task_complete_attempt { success: true }`; "E-mail enviado!" só depois
  do `task_updated` com `completed`, e a tela fecha sozinha.
- `[Esc]` ou o `X` no meio do envio **cancela e zera a barra**. A tarefa continua
  `in_progress`; reabrir não reenvia o start.
- Sem confirmação do servidor em 5 s (`_segundosTimeout`), a tela volta ao início com um aviso.
- O painel cria um `EventSystem` em runtime se a cena não tiver (os andares 2 e 3 não têm).

## Trava de ação — `PlayerActionLock`

`Interactions/PlayerActionLock.cs`. Um dono por vez (`Acquire(owner, lockMovement, freeCursor)`
/ `Release(owner)`). Enquanto travado:

- `PlayerController.SetMovementLocked(true)` — sem andar e sem câmera, e o cursor continua como
  está (o `ToggleControl` antigo soltava o cursor);
- `InteractionInput.InteractPressed/Held/Released`, `DropPressed` e `ThrowPressed` devolvem
  `false` — todos os interagíveis do mundo ficam mudos sem precisar checar nada;
- as leituras de UI (`DigitPressed`, `BackspacePressed`, `SubmitPressed`, `CancelPressed`) não são
  bloqueadas, porque é o dono da trava quem as usa.

Todo dono libera a trava no `OnDisable`, então sair do Play Mode ou trocar de cena não deixa o
jogador preso.

## Sons

| Momento | Clipe | Campo |
| :------ | :---- | :---- |
| Cada dígito digitado no teclado do cofre | `SFX_Password` | `SafeInteraction._somSenha` (repassado ao `SafeKeypadPanel.Open`) |
| Início do recolhimento do dinheiro | `SFX_Cash` | `SafeInteraction._somRecolher` |
| Clique em "Enviar email" (ou `[Enter]`) | `SFX_Click` | `EmailComputer._somClique` (repassado ao `EmailUploadPanel.Open`) |

Os painéis são criados em runtime e não têm Inspector, por isso o clipe vem do componente da cena
que os abre. Regras gerais em [AUDIO.md](AUDIO.md).

## HUD

- Categoria **FINANCEIRO** (verde) no card de tarefa (`TaskPresentation.TaskCategory.Finance`).
- O marcador de objetivo aponta para o cofre ainda não esvaziado mais próximo e para o PC do e-mail.

## Setup da cena

`Tools > Hora-Extra > Configurar 3º andar (Financeiro)` (`Assets/Editor/HoraExtraFinanceiroSetup.cs`).
É idempotente: recria só os objetos `*_Gerado`. Ver também [ELEVADOR.md](ELEVADOR.md).
