# Workflows Hora-Extra

Este arquivo converte os workflows do Antigravity para uso no Codex.

## Backend - Planejamento

Use para mudanças em `hora-extra-backend`.

1. Explore o requisito contra o servidor atual sem alterar arquivos.
2. Classifique a mudança como REST, real-time Socket.IO, banco/Prisma, autenticação, serviço interno ou combinação.
3. Consulte `hora-extra-backend/docs/REST_API_GUIDE.md`, `hora-extra-backend/prisma/schema.prisma` e `hora-extra-backend/AGENTS.md`.
4. Mapeie serviços, controllers, routes, handlers, factories, types e docs afetados.
5. Defina testes TDD esperados para serviços e regras server-authoritative.
6. Entregue plano com banco, endpoints/eventos, payloads, validação, testes e documentação.
7. Aguarde aprovação antes de codificar.

## Backend - Execução

Use após aprovação de implementação backend.

1. Preserve mudanças locais não relacionadas e crie acompanhamento de tarefa somente se necessário.
2. Crie ou ajuste interfaces em `src/types/` quando houver contrato novo.
3. Para lógica de negócio, siga TDD em `src/services/*.test.ts` e implemente em `src/services/*.ts`.
4. Registre serviços reutilizáveis em `src/core/factories/Service.Factory.ts`.
5. Para REST, atualize controller, route e agregador de rotas.
6. Para Socket.IO, crie handler, registre no socket manager/factory e valide entradas antes de broadcast.
7. Atualize documentação em `hora-extra-backend/docs/`.
8. Rode `npm test` e `npm run build` quando aplicável.
9. Finalize com resumo das alterações e validações.

## Client Unity - Planejamento

Use para mudanças em `hora-extra-client`.

1. Explore scripts, cenas, prefabs e docs relevantes sem alterar arquivos.
2. Mapeie impacto visual, gameplay, UI, assets e rede.
3. Consulte `hora-extra-client/AGENTS.md`, `Assets/Scripts/Network/NetworkEvents.cs` e docs em `hora-extra-client/Docs/`.
4. Liste scripts C# a criar/modificar, componentes do Inspector e assets/prefabs/cenas afetados.
5. Se houver rede, defina eventos, DTOs, threading e padrão Observer.
6. Planeje validação manual no Unity Editor/Play Mode; não planeje testes unitários.
7. Entregue plano e aguarde aprovação antes de alterar scripts ou assets.

## Client Unity - Execução

Use após aprovação de implementação no Unity.

1. Siga a organização em `Assets/Scripts/` por domínio.
2. Use padrões C# do projeto: `PascalCase`, `_camelCase`, `[SerializeField] private`, cache em `Awake`/`Start`.
3. Para rede, atualize `NetworkEvents.cs`, DTOs em `Models/` e chamadas/listeners no `SocketManager`.
4. Use `OnUnityThread` antes de manipular objetos Unity em callbacks.
5. Não crie testes unitários, pastas `Tests/` nem arquivos `Tests.cs`.
6. Atualize documentação em `hora-extra-client/Docs/`.
7. Valide por compilação no Unity, Play Mode, logs e teste manual conforme a mudança permitir.
8. Finalize com resumo das alterações e validações manuais.

## Backend-Client - Planejamento

Use para mudanças que cruzam servidor e Unity.

1. Explore o contrato de rede atual em docs, backend e client.
2. Classifique o evento como alta frequência ou baixa frequência.
3. Defina evento, direção, payload, validação server-side e resposta/broadcast.
4. Mapeie impacto em TypeScript, C# DTOs, `NetworkEvents.cs`, handlers e documentação.
5. Para eventos de alta frequência, decida se chaves compactas são necessárias e documente essa escolha.
6. Defina testes unitários apenas para a lógica de backend.
7. Entregue plano com contrato de rede, impacto nos dois lados, validação, docs e testes.
8. Aguarde aprovação antes de implementar.

## Backend-Client - Execução

Use após aprovação de uma integração.

1. Atualize primeiro a documentação do contrato em `COMMUNICATION.md` ou guia equivalente.
2. Implemente backend com teste, type/interface, handler, validação server-authoritative e build.
3. Implemente client com constante em `NetworkEvents.cs`, DTO em `Models/` e emissão/escuta via `SocketManager`.
4. Verifique correspondência exata entre nomes de eventos, campos e tipos dos dois lados.
5. Valide com logs cruzados (`[SOCKET]`, `[NETWORK]`) e teste manual.
6. Finalize com resumo da integração e validações.

## Product Owner - Planejamento

Use para backlog, sprint, épicos, histórias e critérios de aceite.

1. Explore `documents/`, GDD, pitch, guias e histórico de sprints quando existir.
2. Classifique a demanda como MVP, melhoria importante ou nice-to-have.
3. Identifique dependências entre Arte, Backend, Client e documentação.
4. Defina épicos, histórias, critérios de aceite, DoD e teste de aceitação.
5. Entregue plano e aguarde aprovação antes de criar ou alterar tarefas externas.

## Product Owner - Execução

Use após aprovação de organização de backlog/sprint.

1. Refine tarefas aprovadas com contexto técnico real do repo.
2. Remova duplicidades apenas quando o usuário aprovar ou quando a ferramenta permitir arquivamento seguro.
3. Agrupe tarefas em sprint de 1 a 2 semanas quando solicitado.
4. Use labels como `Backend`, `Client`, `Arte` e `Bug`.
5. Cada tarefa deve ter título, contexto, critérios de aceite, referências e teste de aceitação.
6. Finalize com resumo e links/artefatos criados.
