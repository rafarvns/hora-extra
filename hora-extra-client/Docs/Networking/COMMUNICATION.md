# Documentação de Comunicação Socket (UDP Root) - Hora-Extra

Esta documentação define o protocolo de comunicação em tempo real via UDP entre o cliente (Unity) e o servidor (Node.js).

## 1. Visão Geral
- **Protocolo**: UDP Datagram (Raw)
- **Porta**: `3001` (UDP)
- **Endereço**: `127.0.0.1` (Local)
- **Formato**: JSON (dentro do datagrama)
- **Frequência de Tick (Sync)**: 20Hz (a cada 50ms)

## 2. Fluxo de Conexão (Handshake)
O UDP é connectionless, então implementamos um handshake manual:
1. O cliente envia o evento `CONN` contendo o **Token JWT** e dados iniciais.
2. O servidor valida o token. Se válido, cria uma sessão vinculada ao `IP:PORTA` do remetente.
3. O servidor responde com `CONN_SUCCESS` contendo o ID persistente do jogador.
4. O cliente emite `join_room` com o ID da sala.
5. O servidor responde with `room_joined` e associa a sessão à sala (room).

## 3. Estrutura do Pacote UDP
Cada datagrama deve conter um JSON no seguinte formato:
```json
{
  "e": "nome_do_evento",
  "d": { "payload": "..." },
  "token": "..." 
}
```
*(Nota: O campo `token` só é obrigatório no evento `CONN`).*

## 4. Eventos: Cliente -> Servidor (UDP)

| Evento | Payload | Descrição |
| :--- | :--- | :--- |
| `CONN` | `{ "token": string, "d": { "playerName": string, "resetRoom?": boolean } }` | Handshake inicial de autenticação. `resetRoom` reinicia o estado da sala (apenas Dev). |
| `join_room` | `{ "roomId": string, "playerName": string }` | Solicita entrada em uma sala. |
| `player_move` | `{ "p": [x, y, z], "r": rotation }` | Envia posição e rotação Y do jogador local. |
| `npc_register` | `{ "id": string, "type": string, "p": [x,y,z], "r": float }` | Registra um NPC/Boss na sala. |
| `npc_move_request` | `{ "id": string, "p": [x, y, z], "r": rotation }` | Solicita movimento de um NPC (Master apenas). |
| `ping` | `{ "timestamp": number }` | Cálculo de latência. |

## 5. Eventos: Servidor -> Cliente (UDP)

| Evento | Payload | Descrição |
| :--- | :--- | :--- |
| `CONN_SUCCESS` | `{ "id": string }` | Sucesso na autenticação inicial. |
| `CONN_ERROR` | `{ "message": string }` | Falha na autenticação ou token expirado. |
| `room_joined` | `{ "roomId": string, "playerId": string }` | Confirmar entrada na sala. |
| `player_move` | `{ "id": string, "p": [x, y, z], "r": number }` | Atualização de posição de outros jogadores. |
| `npc_move` | `{ "id": string, "p": [x, y, z], "r": number }` | Atualização de posição de NPCs/Bosses. |
| `npc_registered` | `{ "id": string, "type": string, "p": [x,y,z], "r": float }` | Notifica registro de NPC na sala. |
| `player_joined` | `{ "id": string, "name": string }` | Notifica novo jogador na sala. |

---

## 6. Boas Práticas (UDP Mode)
1. **Unreliable**: Espere que alguns pacotes de movimento se percam. O cliente deve estar preparado para lacunas.
2. **Heartbeat**: A sessão no servidor expira após 30 segundos sem pacotes. O cliente deve manter o envio de `ping` ou `move` constante.
3. **Ordem**: Implementar um campo de `sequence` ou `timestamp` se a ordem dos pacotes se tornar um problema crítico.
