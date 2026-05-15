# Guest Mode — Mecânica de Acesso Anônimo

## Motivação

O Hora-Extra precisa demonstrar dois jogadores na mesma cena para a entrega do TCC.
O fluxo completo de cadastro/login/lobby ainda está em desenvolvimento paralelo.

Para destravar a entrega, o **Guest Mode** permite que qualquer jogador entre diretamente no
mundo sem criar conta — basta um clique em "Jogar como Convidado".

---

## Arquitetura de sala única

Todos os guests compartilham **uma única sala in-memory**: `"guest-room"`.

- Sem persistência em banco (Prisma não é envolvido).
- A sala existe enquanto o servidor está rodando; reiniciar o backend limpa tudo.
- Limite prático: tantos guests quanto o servidor suportar em memória (sem limite implementado para TCC).

### Por que sala única?

Simplicidade para o TCC. Se no futuro forem necessárias múltiplas salas simultâneas, basta
gerar um `guestRoomId` aleatório no `POST /api/auth/guest` e passá-lo para o cliente — a
lógica de `guest-` prefix detection no handshake já está pronta para isso.

---

## Algoritmo "primeiro = host" (NPC Mastership)

Não existe conceito de "host" explícito no Hora-Extra. A autoridade sobre os NPCs é
distribuída por NPC, não por sala:

1. Quando o cliente A entra e a cena carrega, ele registra cada NPC via `npc_register`.
2. O servidor checa `NpcRegisterHandler.npcMasters.has("guest-room:<npcId>")`:
   - Se vazio → atribui A como master → envia `npc_registered { isMaster: true }` para A.
3. Quando o cliente B entra e registra o mesmo NPC:
   - Mapa já tem master (A) → envia `npc_registered { isMaster: false }` para B.
4. Apenas o master envia `npc_move_request`; o servidor broadcast `npc_move` para os demais.

**Efeito prático**: o primeiro guest a entrar na sala se torna master de todos os NPCs
automaticamente — sem nenhuma eleição explícita, sem código novo.

---

## Lazy Reset — sala vazia → próximo guest reaviva

Quando **todos** os guests se desconectam (timeout 30 s + cleanup tick 10 s):

1. `cleanupSessions` detecta que `guest-room` ficou vazia.
2. Chama `NpcRegisterHandler.clearRoomState("guest-room")` — apaga o mapa de masters.

Na próxima CONN de um guest:

1. `handleConnection` verifica `getRoomSessionCount("guest-room") === 0`.
2. Chama `clearRoomState` novamente como segurança (lazy reset).
3. Cria a sessão do novo guest.
4. Novo guest se torna master de todos os NPCs ao registrá-los.

Isso garante que NPCs órfãos (cujo master desconectou) não ficam travados indefinidamente.

---

## Diagrama de estados da sala guest

```
[VAZIA]
   │
   │ novo guest conecta
   ▼
[COM SESSIONS] ──── mais guests entram ────► [COM SESSIONS]
   │
   │ todos os guests desconectam / timeout
   ▼
[VAZIA + cleanup de NPCs]
   │
   │ próximo guest conecta → lazy reset
   ▼
[COM SESSIONS] (ciclo recomeça)
```

---

## Limitações conhecidas (fora do escopo TCC)

- Sem reconexão automática: se o guest cair, precisa clicar "Jogar como Convidado" de novo.
- Sem persistência de progresso: tudo em memória.
- Sem rate limit no endpoint `POST /api/auth/guest` (sem captcha, sem IP throttle).
- Sem múltiplas salas guest simultâneas.

---

## Referências

- Implementação do handshake: `src/sockets/UdpSocketManager.ts` — `handleConnection`
- Limpeza pós-timeout: `src/sockets/UdpSocketManager.ts` — `cleanupSessions`
- NPC mastership: `src/sockets/handlers/NpcRegister.Handler.ts`
- Endpoint REST: `src/api/controllers/GuestController.ts`
- Protocolo completo: `docs/Networking/COMMUNICATION.md` §7
