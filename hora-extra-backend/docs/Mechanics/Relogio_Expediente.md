# Relógio do Expediente

Relógio de jogo **autoritativo, por sala**, que marca a hora do expediente (08:00 → 18:00)
em tempo acelerado. Hoje é informativo (HUD); serve de base para prazos, eventos agendados
e "hora extra" de planos futuros (ver plano 0010).

## Fluxo

1. A intro do cliente termina → cliente envia `shift_clock_request`.
2. Primeiro pedido da sala: `ShiftClockService.startShift` registra o instante e o handler faz
   **broadcast** de `shift_clock` para todos da sala.
3. Pedidos seguintes (outros jogadores entrando, resync a cada ~30 s) recebem `shift_clock`
   **só para o remetente**.
4. O cliente extrapola localmente `m + k·Δt` entre sincronizações.
5. Ao atingir 18:00 o snapshot passa a `st: "ended"`, `k: 0`, `m: 1080`. Sem consequência
   de gameplay por enquanto.

## Configuração

| Variável | Padrão | Efeito |
| :------- | :----- | :----- |
| `SHIFT_DURATION_SECONDS` | `900` | Duração **real** do expediente inteiro. `60` é útil para testar o fim rapidamente. |

Lida no momento do uso (não na importação), então basta reiniciar o servidor após mudar o `.env`.

## Ciclo de vida

- Estado só em memória (`Map<roomId, startedAtMs>`).
- Zerado em `resetRoomState` (dev `resetRoom: true`) e quando a `guest-room` esvazia por timeout.

## Por que `m` e não timestamps?

Mandar o minuto já calculado elimina a dependência de os relógios do cliente e do servidor
estarem sincronizados: o cliente só mede Δt local desde o recebimento. O erro máximo fica no
atraso de rede (uma latência), corrigido a cada resync.

## Referências

- `src/services/ShiftClockService.ts` (+ `.test.ts`)
- `src/sockets/handlers/ShiftClockRequest.Handler.ts` (+ `.test.ts`)
- Contrato: `docs/Networking/COMMUNICATION.md` (§3 `shift_clock_request`, §4 `shift_clock`)
