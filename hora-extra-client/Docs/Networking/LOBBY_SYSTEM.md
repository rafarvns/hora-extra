# Sistema de Lobby e Salas

Este sistema permite que os jogadores visualizem salas abertas, criem suas próprias salas e entrem em partidas.

## Fluxo de Funcionamento

1.  **Listagem (REST)**: Ao entrar na `LobbyScene`, o `LobbyController` faz um GET em `/api/rooms` para buscar as salas com status `OPEN`.
2.  **Criação (REST)**: Ao clicar em "Criar Sala", o cliente faz um POST enviando o nome da sala (baseado no nome do jogador) e o `hostId`.
3.  **Navegação**: Após criar ou entrar em uma sala, o jogador é redirecionado para a cena de jogo (ex: `SampleScene`).

## Estrutura de Código (Client)

### Scripts
- **`RoomService.cs`**: Abstração das chamadas REST para salas.
- **`LobbyController.cs`**: Gerencia a lógica da tela (Refresh, Create, Join).
- **`RoomItemUI.cs`**: Script anexado ao Prefab que exibe as informações de cada sala.

### Modelos (`RoomModels.cs`)
Representam o contrato de dados entre Client e Backend.

## Estrutura de Código (Backend)

### Banco de Dados (Prisma)
- Tabela `salas` (Room): Armazena ID, Nome, Host, Quantidade de Jogadores e Status.

### Rotas
- `GET /api/rooms`: Lista salas abertas.
- `POST /api/rooms`: Cria uma nova sala.

---

## Como Configurar no Unity Editor

1.  Anexe o `LobbyController.cs` ao seu objeto de painel na `LobbyScene`.
2.  Arraste o **Content** do seu ScrollView para o campo `Rooms Container`.
3.  Arraste o seu **Prefab RoomItem** para o campo `Room Item Prefab`.
4.  No Prefab `RoomItem`, anexe o script `RoomItemUI.cs` e configure os textos (`RoomNameText`, `PlayerCountText`) e o botão (`JoinButton`).
