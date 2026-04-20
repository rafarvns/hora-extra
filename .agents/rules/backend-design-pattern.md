# Padrão de Design Backend (Node.js/Socket.io) - Hora-Extra

Este documento define as diretrizes arquiteturais para o servidor de jogo, garantindo escalabilidade, sincronização de tempo real e separação de responsabilidades para o backend do "Hora-Extra".

## 1. Arquitetura do SocketManager (Singleton)
- O gerenciamento do servidor de WebSockets (`socket.io`) deve ser feito exclusivamente através da classe Singleton `SocketManager`.
- **Initialization**: O servidor é inicializado no `src/index.ts` e exportado para outros módulos via `SocketManager.getInstance()`.

## 2. Separação de Responsabilidades (Handlers)
Evite que o `SocketManager` se torne uma "God Class".
- **Handler Pattern**: Delegue a lógica de eventos para funções ou classes específicas em `src/sockets/handlers/`.
- **Exemplos**: `connectionHandler.ts` para autenticação/conexão, `roomHandler.ts` para trocas de sala, e `inputHandler.ts` para processar comandos de gameplay.
- **Orquestração**: O `SocketManager` deve apenas registrar os eventos e chamar os handlers correspondentes.

## 3. Gerenciamento de Estado (StateManager)
- **Memory Storage**: O estado global das partidas (jogadores, itens, salas) deve ser centralizado em uma classe `StateManager` ou similar.
- **Imutabilidade/Sincronia**: Garanta que as atualizações de estado sejam processadas de forma síncrona dentro do loop de processamento para evitar condições de corrida (Race Conditions).

## 4. Loop de Sincronização (Tick-Rate)
- **Servidor Autoritativo**: O backend é o dono da verdade. Ele deve processar os inputs e emitir o estado final.
- **Fixed Update**: Implemente um loop de rede fixo de **20Hz** (um broadcast a cada 50ms) usando `setInterval` ou um loop de alta precisão para enviar o evento `state_update`.

## 5. Validação de Dados e Tipagem
- **TypeScript First**: Todos os eventos e payloads devem ter interfaces/tipos correspondentes.
- **Sanity Checks**: Valide todos os inputs do cliente. Nunca confie na posição informada pelo jogador se o jogo for competitivo; valide limites de velocidade e colisões no servidor.

## 6. Gerenciamento de Salas (Rooms)
- **Socket.io Rooms**: Utilize `socket.join(roomId)` e `socket.leave(roomId)` para gerenciar partidas isoladas.
- **Broadcast**: Use `io.to(roomId).emit()` para economizar processamento, enviando dados apenas para quem precisa deles.

## 7. Oimização de Banda
- **Compact Payloads**: Conforme definido no `COMMUNICATION.md`, utilize chaves encurtadas (ex: `p` em vez de `position`) em eventos de alta frequência para reduzir o overhead de rede.
- **Delta Updates**: No futuro, envie apenas as mudanças (deltas) de estado em vez do estado completo a cada tick.

## 8. Logs e Diagnósticos
- **Logger Centralizado**: Use exclusivamente o `Logger` em `src/utils/Logger.ts` em vez de `console.log`.
- **Níveis de Log**:
    - `error`: Falhas críticas e exceções não tratadas.
    - `warn`: Comportamentos inesperados, falhas de validação ou segurança (ex: token inválido).
    - `info`: Eventos de ciclo de vida (startup, conexões, entrada em salas).
    - `debug`: Informações detalhadas de processamento (apenas em desenvolvimento).
- **Módulos**: Sempre forneça o campo `{ module: 'NOME' }` no metadata para facilitar a filtragem (Ex: `SERVER`, `SOCKET`, `HTTP`, `AUTH`, `GAME`).
- **Endpoint de Saúde**: Mantenha o endpoint `/api/health` para monitoramento básico.

---
*Este documento é uma Regra de Agente (Rule) e deve ser seguido em todas as implementações do backend.*
