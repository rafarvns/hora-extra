# Documentação de Comunicação Socket (UDP Root) - Hora-Extra

Esta documentação define o protocolo de comunicação em tempo real via UDP entre o cliente (Unity) e o servidor (Node.js).

## 1. Visão Geral
- **Protocolo**: UDP Datagram (Raw)
- **Porta**: `3001` (UDP)
- **Endereço**: `127.0.0.1` (Local)
- **Formato**: JSON (dentro do datagrama)
- **Frequência de Tick (Sync)**: 20Hz (a cada 50ms)

---

## 2. API REST (HTTP)
Além da comunicação em tempo real via UDP, o projeto utiliza uma API REST para operações de baixa frequência e persistência.

- **Base URL**: `http://localhost:5000/api`
- **Formato**: JSON

### 2.1. Autenticação
| Recurso | Método | Endpoint | Descrição |
| :--- | :--- | :--- | :--- |
| **Cadastro** | `POST` | `/auth/register` | Cria uma nova conta de jogador. |
| **Login** | `POST` | `/auth/login` | Autentica e retorna o JWT. |

---

## 3. Fluxo de Conexão (Handshake)
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
| `npc_registered` | `{ "id": string, "type": string, "p": [x,y,z], "r": float, "isMaster": boolean }` | Notifica registro de NPC na sala e define autoridade. |
| `player_joined` | `{ "id": string, "name": string }` | Notifica novo jogador na sala. |

---

## 4. Exemplos Implementados
- **HealthCheck**: Veja o arquivo `HealthService.cs` para um exemplo real de verificação de status.
- **Autenticação**: Veja o arquivo `AuthService.cs` para exemplos de cadastro e login.

---

## 5. Boas Práticas
1. **Async/Await**: SEMPRE utilize chamadas assíncronas para não travar a UI (Thread Principal) do Unity enquanto aguarda a rede.
2. **Gerenciamento de Erros**: Sempre verifique o campo `Success` na resposta antes de tentar acessar os `Data`. Caso ocorra uma falha, o campo `Error` trará os detalhes.
3. **Serialização**: As chaves do seu arquivo C# devem bater EXATAMENTE com as chaves JSON retornadas pelo backend, a menos que use o atributo `[JsonProperty("outra_chave")]`.
