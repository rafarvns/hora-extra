---
description: Workflow para ações no backend do projeto Hora-Extra.
---

Este workflow deve ser seguido para qualquer modificação ou adição de novas funcionalidades no backend (Node.js/Express/Socket.io).

## 1. Planejamento e Arquitetura
Antes de codar, identifique se a funcionalidade é **REST (HTTP)** ou **Real-time (Websocket)**.
- Consulte `hora-extra-backend/docs/REST_API_GUIDE.md` para novas rotas.
- Consulte `.agents/rules/backend-design-pattern.md` para lógica de game server.

## 2. Implementação de Lógica de Negócio (Service)
Toda a lógica complexa ou acesso ao banco (Prisma) deve residir em um **Service**.
1. Crie o arquivo em `src/services/<nome>Service.ts`.
2. Implemente as funções necessárias utilizando o cliente do Prisma.
// turbo
3. Registre o novo serviço na `ServiceFactory` em `src/core/factories/Service.Factory.ts`.

## 3. Exposição de Funcionalidade REST (API)
Se a funcionalidade requer um endpoint HTTP:
1. **Controller**: Crie/Edite em `src/api/controllers/`. Deve herdar de `BaseController` e usar a `ServiceFactory` para obter serviços.
2. **Routes**: Defina o endpoint em `src/api/routes/` e associe-o ao método do controller.
3. **Agregador**: Registre a nova rota no `src/api/routes/index.ts`.

## 4. Implementação de Eventos Web Socket (Game Server)
Se a funcionalidade faz parte do gameplay em tempo real:
1. **Handler**: Crie um novo handler em `src/sockets/handlers/`.
2. **Mensagens**: Utilize chaves curtas para payloads de alta frequência conforme o padrão de otimização de banda.
3. **SocketManager**: Registre o evento no `SocketManager.ts`, delegando a execução para o handler correspondente.
4. **Estado**: Se houver mudança no estado do jogo, use o `StateManager` centralizado.

## 5. Práticas Obrigatórias
- **Tipagem**: Use interfaces TypeScript em `src/types/` para todos os payloads de eventos de socket e requests de API.
- **Erros**: Utilize a classe `ApiError` para lançar exceções controladas.
- **Logs**: Use os prefixos padrão: `[SERVER]`, `[SOCKET]`, `[GAME]`.
- **Validação**: Valide sempre os dados recebidos do cliente (Server-Authoritative).

## 6. Verificação e Testes
1. Execute `npm run build` para garantir que o TypeScript compila sem erros.
2. Verifique os logs do servidor em execução para garantir que não há crashes.
3. Documente o novo recurso em um arquivo `.md` correspondente na pasta `docs/`.
