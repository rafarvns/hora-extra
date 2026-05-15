---
name: hora-extra-workflows
description: Workflows Codex para o projeto Hora-Extra. Use quando a tarefa pedir planejamento ou execução de backend Node.js, client Unity/C#, integração backend-client com contratos de rede, ou atividades de Product Owner como backlog, sprint, histórias e critérios de aceite.
---

# Hora-Extra Workflows

## Overview

Use esta skill para selecionar e seguir o workflow correto antes de planejar ou executar tarefas no projeto Hora-Extra.

## Seleção de Workflow

- **Backend**: use para APIs REST, serviços, Prisma, autenticação, Socket.IO server-side, handlers, factories e testes Vitest.
- **Client Unity**: use para scripts C#, UI Unity, gameplay, prefabs, cenas, assets e networking do cliente.
- **Backend-Client**: use quando a tarefa muda contrato de rede, evento Socket.IO, payload, DTO, sincronização ou comportamento que precise dos dois lados.
- **Product Owner**: use para backlog, épicos, histórias, sprint, critérios de aceite, DoD e organização de tarefas.

## Modo Planejamento

Quando o usuário pedir plano, arquitetura, análise de impacto, organização de sprint ou aprovação antes da implementação:

1. Faça exploração não mutante do repo antes de perguntar.
2. Use raciocínio estruturado para mapear requisitos, arquivos afetados, contratos, riscos e validações.
3. Consulte os `AGENTS.md` aplicáveis e os documentos do projeto.
4. Entregue um plano decisivo e aguarde aprovação explícita antes de alterar código.

Consulte `references/workflows.md` para os passos de planejamento por área.

## Modo Execução

Quando o usuário aprovar ou pedir implementação:

1. Preserve mudanças locais não relacionadas.
2. Siga o workflow de execução da área correta.
3. Atualize documentação obrigatória junto da mudança.
4. Execute validações adequadas à área.
5. Resuma alterações, testes e pendências.

Consulte `references/workflows.md` para os passos de execução por área.

## Regras de Conversão

- `.agents/` é legado Antigravity; não edite nem remova a menos que o usuário peça.
- Use caminhos relativos do repo, nunca caminhos absolutos antigos.
- Para planejamento no Codex, prefira plano no chat; só crie `implementation_plan.md` quando o usuário pedir arquivo.
- Para execução, só crie `task.md` ou `walkthrough.md` quando fizer sentido para a tarefa ou quando o usuário pedir esse artefato.
