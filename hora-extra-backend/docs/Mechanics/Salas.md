# Salas e multiplayer (servidor)

Substitui o antigo Guest Mode (`guest-room` única) e o cadastro/login de jogadores.

## Entrada

1. `POST /api/session { nome }` — o jogador só digita o nome (2–16 caracteres). O servidor gera
   `playerId = "p-<uuid12>"` e devolve um JWT `{ id, n }` (`sessionService`).
2. `POST /api/rooms { nome }` — cria uma sala vazia (`RoomRegistry.create`). Quem quer entrar numa
   existente pega o `id` em `GET /api/rooms`.
3. UDP `CONN { token, d: { roomId } }` — `UdpSocketManager.handleConnection` confere token, sala
   e vaga (`RoomRegistry.canJoin`) e responde `CONN_SUCCESS` + `room_state`, ou `CONN_ERROR`
   (`INVALID_TOKEN`, `ROOM_NOT_FOUND`, `ROOM_FULL`).

Contrato completo: `docs/Networking/COMMUNICATION.md` §2.

## Regras da sala (`src/services/RoomRegistry.ts`)

| Regra | Valor |
| :---- | :---- |
| Máximo de jogadores | `MAX_PLAYERS_PER_ROOM = 2` (playerIds distintos) |
| Reconexão do mesmo jogador | substitui a sessão antiga, não ocupa vaga |
| Sala vazia | apagada após `EMPTY_ROOM_TTL_MS = 60 s`, com tarefas, relógio e NPCs |
| "Em andamento" | alguém já entrou alguma vez |
| Persistência | nenhuma (memória); reiniciar o servidor zera tudo |

## Tarefas compartilhadas (`src/services/TaskService.ts`)

- O catálogo é da sala e **só o primeiro registro vale** (`registerCatalog` devolve `false` depois).
- Cada tarefa é feita **uma vez pela sala**. O jogador recebe uma por vez, entre as que não foram
  concluídas e não têm dono. A 1ª entrada do catálogo tem prioridade; o resto é sorteado.
- Só o dono mexe na tarefa (`NOT_ASSIGNED` para os outros).
- Progresso e itens contados ficam na sala: se o dono sai (`releasePlayer`), a tarefa volta ao
  pool com o progresso, e o servidor a entrega a quem estiver sem tarefa.
- `failed` (QTE perdido) encerra a tarefa, como antes.
- Quem entra no meio recebe no `room_state` o estado de todas as tarefas (status, progresso,
  itens, dono) e os outros jogadores com posição e andar.

## Saída

`leave_room` (o cliente manda ao voltar ao menu e ao fechar o jogo) ou 30 s sem pacotes. O
`removeSession` faz tudo num lugar só: `player_disconnected` + `room_roster`, devolve a tarefa,
troca o mestre dos NPCs e libera a vaga.

## NPCs

Mestre = um jogador no mesmo andar do NPC (`npc_register.f`, `player_move.f`). Ao sair ou trocar
de andar, o mestre passa para outro jogador daquele andar (`NpcRegisterHandler.onPlayerGone` /
`onPlayerFloorChanged`).

## Testes

`RoomRegistry.test.ts`, `sessionService.test.ts`, `TaskService.test.ts` (modelo da sala) e
`UdpSocketManager.test.ts` (CONN, limite, reconexão, `room_state`, saída, tarefa devolvida,
mestre de NPC).
