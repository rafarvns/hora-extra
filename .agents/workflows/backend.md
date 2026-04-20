---
description: Workflow para execução de tarefas no backend do projeto Hora-Extra.
---

Este workflow assume que um plano de implementação (`implementation_plan.md`) já foi aprovado. O foco aqui é a codificação técnica e verificação.

## 1. Preparação
1. Crie o arquivo `task.md` para trackear o progresso da implementação técnica.
2. Certifique-se de que todas as interfaces necessárias em `src/types/` estão definidas.

## 2. Implementação de Lógica de Negócio (TDD)
1. **Red**: Crie o arquivo de teste em `src/services/<nome>Service.test.ts` com as expectativas falhando.
2. **Green**: Crie/Edite o arquivo em `src/services/<nome>Service.ts` e implemente o código mínimo para passar nos testes.
3. **Refactor**: Melhore o código e garanta que os testes continuam passando executando `npm test`.
// turbo
4. **Registro**: Registre o novo serviço na `ServiceFactory` em `src/core/factories/Service.Factory.ts`.

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
3. **Documentação (OBRIGATÓRIO)**: Adicione ou atualize a documentação em `e:\PUC\hora-extra\hora-extra-backend\docs`. Certifique-se de que novos endpoints, eventos de socket ou mudanças arquiteturais estão documentados.
4. **Relatório**: Crie o artefato `walkthrough.md` resumindo as alterações e testes realizados.
