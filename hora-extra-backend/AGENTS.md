# Contexto Codex - Backend Hora-Extra

Estas instruções valem para `hora-extra-backend`.

## Desenvolvimento Backend

- Use TypeScript como fonte de verdade para contratos e payloads.
- Todo novo serviço, handler de socket ou lógica de negócio deve começar por testes unitários com Vitest quando for comportamento testável.
- Siga o ciclo TDD:
  - Red: escreva uma expectativa falhando.
  - Green: implemente o mínimo para passar.
  - Refactor: melhore mantendo os testes verdes.
- Execute `npm test` para validar testes e `npm run build` para validar TypeScript quando a tarefa alterar código.

## Arquitetura

- Controllers REST devem herdar de `BaseController`.
- Rotas ficam em `src/api/routes/` e devem ser registradas em `src/api/routes/index.ts`.
- Serviços ficam em `src/services/` e devem ser registrados pela `ServiceFactory` quando forem reutilizáveis.
- Evite instanciar serviços ou handlers complexos diretamente em controllers ou socket managers; use factories.
- Arquivos de factory devem terminar com `.Factory.ts`.

## Socket.IO e Game Server

- O backend é server-authoritative: valide entradas do cliente antes de persistir, processar ou fazer broadcast.
- Mantenha o `SocketManager` como orquestrador, sem concentrar lógica de domínio nele.
- Delegue eventos para handlers em `src/sockets/handlers/`.
- Use rooms do Socket.IO para isolar partidas e emitir apenas para quem precisa receber o evento.
- Para eventos de alta frequência, prefira payloads compactos quando isso já estiver documentado no contrato de comunicação.

## Logs e Diagnóstico

- Use exclusivamente `src/utils/Logger.ts` para logs estruturados.
- Inclua metadados com `module`, como `SERVER`, `SOCKET`, `HTTP`, `AUTH` ou `GAME`.
- Use níveis coerentes:
  - `error` para falhas críticas;
  - `warn` para entradas inválidas, comportamento inesperado ou falhas de segurança;
  - `info` para ciclo de vida;
  - `debug` para detalhes de desenvolvimento.

## Testes

- Testes devem usar sufixo `.test.ts` ou estrutura equivalente já existente.
- Priorize:
  - validação de pacotes de rede;
  - regras de salas;
  - cálculos de movimento e sanity checks;
  - lógica de ticks ou broadcast;
  - regras de autenticação e autorização.
- Escreva código testável, com dependências injetáveis quando isso evitar acoplamento com Socket.IO ou Prisma.

## Documentação

- Documente novas APIs, eventos, factories, serviços e mudanças arquiteturais em `hora-extra-backend/docs/`.
- Atualize guias existentes como `REST_API_GUIDE.md`, `AUTHENTICATION.md`, `COMMUNICATION.md` ou crie um guia específico quando a mudança não couber neles.
