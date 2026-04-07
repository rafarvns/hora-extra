---
description: Workflow para execução de tarefas no backend do projeto Hora-Extra.
---

Este workflow assume que um plano de implementação (`implementation_plan.md`) já foi aprovado. O foco aqui é a codificação técnica e verificação.

## 1. Preparação
1. Crie o arquivo `task.md` para trackear o progresso da implementação técnica.
2. Certifique-se de que todas as interfaces necessárias em `src/types/` estão definidas.

## 2. Implementação de Lógica de Negócio (Service)
1. Crie/Edite o arquivo em `src/services/<nome>Service.ts`.
2. Implemente as funções utilizando o cliente do Prisma.
// turbo
3. Registre o novo serviço na `ServiceFactory` em `src/core/factories/Service.Factory.ts`.

## 3. Exposição REST (API)
Se o plano prevê endpoints HTTP:
1. **Controller**: Crie/Edite em `src/api/controllers/`. Deve herdar de `BaseController`.
2. **Routes**: Defina o endpoint em `src/api/routes/` vinculando ao controller.
3. **Agregador**: Registre a nova rota no `src/api/routes/index.ts`.

## 4. Implementação SocketIO (Game Server)
Se o plano prevê eventos em tempo real:
1. **Handler**: Crie um novo handler em `src/sockets/handlers/`.
2. **SocketManager**: Registre o evento no `SocketManager.ts`, delegando para o handler.
3. **Estado**: Utilize o `StateManager` se houver alteração no estado global do jogo.

## 5. Verificação e Testes
1. **Build**: Execute `npm run build` para validar o TypeScript.
2. **Logs**: Monitore os logs do servidor (`[SERVER]`, `[SOCKET]`).
3. **Documentação**: Atualize os guias em `docs/` se houver mudanças na API ou protocolos.
4. **Relatório**: Crie o artefato `walkthrough.md` resumindo as alterações e testes realizados.
