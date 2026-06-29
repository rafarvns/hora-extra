# Contexto Codex - Hora-Extra

Estas instruções valem para todo o monorepo. Instruções mais específicas em subpastas, como `hora-extra-backend/AGENTS.md` e `hora-extra-client/AGENTS.md`, complementam este arquivo.

## Idioma e Colaboração

- Responda preferencialmente em português, com tom profissional, direto e colaborativo.
- Antes de implementar mudanças relevantes, investigue o estado atual do repo e respeite os padrões existentes.
- Não reverta mudanças locais que você não fez. Se encontrar alterações não relacionadas, preserve-as.
- Quando uma tarefa envolver planejamento formal, entregue um plano claro e aguarde aprovação antes de alterar código.

## Monorepo e Separação de Responsabilidades

- Mantenha a separação entre backend Node.js, cliente Unity, documentação e assets.
- Não coloque scripts C# no backend nem utilitários Node.js dentro do cliente Unity.
- Mudanças compartilhadas entre client e backend devem ser documentadas de forma explícita.
- Arquivos de arte, modelos, texturas e sons devem permanecer nas áreas apropriadas de assets.

## Git

- Faça commits atômicos e focados, sem misturar backend, client, docs e assets quando isso puder ser separado.
- Use mensagens de commit preferencialmente em português, no imperativo.
- Prefixos recomendados:
  - `feat(client):`
  - `feat(backend):`
  - `fix(client):`
  - `fix(backend):`
  - `docs:`
  - `assets:`
  - `chore:`

## Documentação Obrigatória

- Toda nova feature deve vir acompanhada de documentação técnica no mesmo fluxo de trabalho.
- Atualize a documentação quando uma mudança alterar comportamento, API, payload, arquitetura ou dados.
- Use estes destinos preferenciais:
  - Backend: `hora-extra-backend/docs/`
  - Client: `hora-extra-client/Docs/`
  - Contratos de rede: documentos `COMMUNICATION.md` existentes em client/backend ou guias equivalentes em `Docs/Networking` e `docs/Networking`.
- A documentação deve explicar o que mudou, como usar, detalhes técnicos relevantes e critérios de validação.

## Contratos Client-Backend

- Qualquer alteração em eventos de rede, payloads ou nomes de campos deve ser refletida nos dois lados e na documentação.
- Antes de concluir trabalho de rede, confira se:
  - constantes em `NetworkEvents.cs` correspondem aos eventos do backend;
  - DTOs C# correspondem às interfaces/types TypeScript;
  - chaves compactas de payload, quando usadas, estão documentadas.

## Workflows Codex

- Os workflows convertidos do Antigravity ficam em `codex/skills/hora-extra-workflows/`.
- Use essa skill quando a tarefa pedir planejamento ou execução nas frentes Backend, Client Unity, integração Backend-Client ou Product Owner.
- A pasta `.agents` permanece como fonte legada do Antigravity e não deve ser removida sem pedido explícito.
