# Documentação de Comunicação Socket - Hora-Extra

Esta documentação define o protocolo de comunicação em tempo real entre o cliente (Unity) e o servidor (Node.js).

## 1. Visão Geral
- **Protocolo**: Socket.io (Engine.IO v4)
- **Formato**: JSON
- **Endereço**: `http://localhost:3000` (Local)
- **Frequência de Tick (Sync)**: 20Hz (a cada 50ms)

## 2. Fluxo de Conexão
1. O cliente se conecta ao namespace padrão `/`.
2. O servidor emite `connection_success`.
3. O cliente emite `join_room` com o ID da sala.
4. O servidor responde com `room_joined` contendo o estado inicial.

## 3. Eventos: Cliente -> Servidor

| Evento | Payload | Descrição |
| :--- | :--- | :--- |
| `join_room` | `{ "roomId": string, "playerName": string }` | Solicita entrada em uma sala específica. |
| `player_input` | `{ "direction": { "x": number, "y": number }, "actions": string[] }` | Envia comandos de movimento e ações (ex: 'interact'). |
| `ping` | `{ "timestamp": number }` | Usado para medir a latência (RTT). |

## 4. Eventos: Servidor -> Cliente

| Evento | Payload | Descrição |
| :--- | :--- | :--- |
| `room_joined` | `{ "roomId": string, "players": Player[], "gameState": object }` | Confirmar entrada e enviar estado inicial. |
| `state_update` | `{ "tick": number, "players": PlayerUpdate[] }` | Broadcast periódico com a posição e estado de todos os jogadores na sala. |
| `player_disconnected` | `{ "playerId": string }` | Notifica que um jogador saiu da partida. |
| `pong` | `{ "timestamp": number }` | Resposta ao ping para cálculo de latência. |

## 5. Estruturas de Dados (Schemas)

### Player
```json
{
  "id": "socket_id",
  "name": "nome_do_jogador",
  "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "rotation": 0.0,
  "status": "idle | walking | interacting"
}
```

### PlayerUpdate
```json
{
  "id": "socket_id",
  "p": [x, y, z],
  "r": rotation
}
```
*(Nota: Nomes de chaves encurtados para 'p' e 'r' para economizar banda em atualizações frequentes).*

---

## 6. Boas Práticas (Co-op Profissional)
1. **Interpolação de Snapshot**: O cliente Unity não deve "snappar" o jogador para a posição recebida, mas sim interpolar suavemente entre o último e o penúltimo estado recebido.
2. **Timeout**: O cliente deve tentar reconectar automaticamente em caso de perda de sinal cardíaco (heartbeat).
3. **Validação**: O servidor ignora pacotes com timestamps muito antigos ou velocidades impossíveis.
