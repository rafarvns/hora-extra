# Guia de Configuração: Jogador e Conexão Co-op

Este guia explica como configurar o Prefab do Jogador e a Cena do Unity para garantir que a movimentação sincronizada via Socket.IO funcione corretamente.

---

## 1. Configurando o Objeto do Jogador (Player)

Para criar um novo jogador funcional, siga a hierarquia e os componentes abaixo:

### Passo a Passo no Unity:
1. No menu superior: **GameObject > Create Empty**. Nomeie como `Player`.
2. Adicione os seguintes componentes ao objeto `Player`:
    - **`Character Controller`**: (Nativo do Unity) Ajuste o `Radius` (0.5) e `Height` (2.0) conforme necessário.
    - **`Player` (Script)**: Gerencia o estado de vida e integração lógica.
    - **`Player Controller` (Script)**: Gerencia os inputs (WASD + Mouse) e a física de movimento.
3. No componente `Player Controller` (Inspector):
    - Arraste o objeto da Câmera (veja abaixo) para o slot `_playerCamera`.
    - Ajuste a `_walkSpeed` (ex: 6.0) e a `_lookSensitivity` (ex: 0.1).

### Estrutura Visual:
O `Player` deve possuir uma câmera interna para a visão em PRIMEIRA PESSOA:
- **`Player`** (Raiz)
    - **`Main Camera`** (Posicionada na altura da cabeça, ex: Y = 1.6)

---

## 2. Configurando o Jogador Remoto (NetworkPlayer)

Para que outros jogadores apareçam na sua tela, crie um **Prefab** separado chamado `PF_NetworkPlayer`:
1. Crie um GameObject simples (pode ser uma Cápsula para teste).
2. Adicione o componente:
    - **`Network Player` (Script)**: Gerenciará a interpolação suave das posições recebidas do servidor.
3. Salve como um **Prefab** na pasta `Assets/Prefabs/`.

---

## 3. Configurando a Cena para Conexão Backend

Para que o jogo se conecte ao servidor Node.js e sincronize o movimento:

### Passo 1: SocketManager
Certifique-se de que a cena possua o objeto de conexão global:
1. Verifique na pasta `Assets/Scripts/Network/` o script `SocketManager.cs`.
2. Adicione um GameObject na cena (ex: `[NetworkManager]`) e anexe o script `SocketManager`.
3. Configure a URL no Inspector: `http://localhost:5000` (API) e a Porta UDP: `5001`.

### Passo 2: Login e Entrada em Sala
A sincronização só começa após o jogador entrar em uma sala:
1. O fluxo deve seguir: **Conectar** -> **Enviar JoinRoom** (com `roomId`).
2. Uma vez na sala, o servidor passará a aceitar e retransmitir o evento `player_move`.

---

## 4. Checklist de Verificação

Se a movimentação não estiver sincronizando, verifique:
- [ ] O backend está rodando? (`npm run dev`)
- [ ] O `SocketManager` inicializou com sucesso (veja o Console do Unity)?
- [ ] O `PlayerController` local tem uma referência válida para a `PlayerCamera`?
- [ ] O `roomId` e `playerName` foram enviados no evento `join_room`?

---
> [!IMPORTANT]
> **Dica Co-op**: Para testar sozinho, faça uma **Build** do projeto (File > Build and Run) e use a janela da Build + a janela do Editor do Unity simultaneamente conectadas na mesma sala.
