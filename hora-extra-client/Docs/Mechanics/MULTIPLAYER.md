# Multiplayer e salas (cliente)

Substitui o Guest Mode e as telas de login/cadastro/lobby (removidas).

## Fluxo do menu

`MainMenuRoot` → **JOGAR** abre o `PlayPanel` (`UI/Menu/Sections/PlayPanel.cs`):

- **Seu nome** (obrigatório, 2–16; fica salvo em `GameSettings.Nickname` só para pré-preencher).
- **Criar sala** (nome opcional; padrão "Sala de <nome>").
- **Salas abertas**: `Sala do Rafa — 1/2 · em andamento`. Sala cheia aparece bloqueada.
- Erros aparecem no painel ("A sala está cheia (2 de 2).", servidor offline...).

`Network/MatchJoinFlow.cs` faz o resto: `POST /session` → (`POST /rooms`) → `SocketManager.Connect`
→ espera `CONN_SUCCESS`/`CONN_ERROR` (8 s) → carrega `SCN_FirstFloor`. **Todos começam no 1º
andar**, inclusive quem entra no meio. Dados da partida ficam só em memória (`MatchSession`).

## Conexão (`SocketManager`)

- Nasce desconectado; reenvia o `CONN` até 3×.
- Sem nenhum pacote do servidor por 10 s → `OnConnectionLost` → `ConnectionWatcher` volta ao menu
  com o aviso "A conexão com o servidor caiu.".
- `Disconnect()` manda `leave_room` (vaga liberada na hora). Chamado ao voltar ao menu e no
  `OnApplicationQuit`.
- Handler com erro não derruba os outros handlers do mesmo evento.

## Outros jogadores (`RemotePlayerSpawner`, `NetworkPlayer`)

- Cápsula colorida com o **nome em cima da cabeça** (TextMeshPro + `Billboard`).
- Persistem entre trocas de andar e só aparecem **no mesmo andar** (`FloorTracker`: cena → 1/2/3;
  o andar vai no `player_move.f`).
- Quem entra no meio recebe os colegas pelo `room_state`, já na posição real.
- O `room_roster` é a lista oficial: atualiza nomes e remove quem saiu.

## Tarefas e mundo compartilhados (`TaskSystemBridge`)

- Cada tarefa é feita uma vez pela sala; cada jogador recebe uma por vez.
- A ponte guarda o **estado da sala** (`RoomTaskState` por tarefa: status, itens contados, dono),
  vindo do `room_state` e de todo `task_assigned`/`task_updated`, e dispara
  `OnRoomStateChanged`.
- Consultas: `IsItemDone(itemId)`, `IsTaskDone(taskId)`, `IsItemHeldByTeammate(itemId)`,
  `TeamDoneCount`/`TeamTotalCount`, `IsWaitingForTeammate`.
- Objetos que refletem o estado (no `Start` e a cada mudança, de qualquer jogador):
  - `DirtSpot` — mancha limpa some;
  - `CarryableItem` — item entregue some (papéis, canetas, revistas, papéis de lixo); item de
    tarefa que está com o colega não pode ser pego ("Isto é tarefa do colega");
  - `SafeInteraction` — cofre esvaziado aparece aberto (`SafeDoor.AbrirInstantaneo`) e sem dinheiro.
- Isso também corrige "voltar ao andar desfaz o que eu fiz".
- HUD: o card mostra "TAREFA ATUAL · EQUIPE X/N"; sem tarefa livre, "Sem tarefa livre agora"; o
  painel de equipe mostra quantas cada um fez.

## NPCs (`NetworkEntity`)

- O registro manda o andar (`f`). O mestre muda quando o anterior sai ou troca de andar
  (`npc_registered { isMaster: true }`); o novo mestre continua de onde o NPC está.
- Corrigido: o segundo jogador registrar o NPC não rebaixa mais o mestre.

## Limitações conhecidas

- Item **na mão** do colega e a porta do cofre aberta mas ainda não esvaziada não são
  sincronizados (só o resultado final).
- Não há botão de "sair da partida" no jogo: sair = fechar o jogo (a vaga é liberada na hora).
- O Editor sempre usa o servidor local 127.0.0.1 (`GameSettings.ApplyOnBoot`).

## Testar com 2 jogadores na mesma máquina

1. Servidor: `cd hora-extra-backend && npm run dev`.
2. Gere um build Windows (`File > Build Profiles > Build`) e, antes, deixe o servidor do build em
   "Local" (Configurações → Servidor) — o Editor já usa 127.0.0.1.
3. Abra o build e dê Play no Editor (cena `MainMenuScene`). Um cria a sala, o outro entra.
