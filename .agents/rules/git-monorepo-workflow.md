---
description: Fluxo de Trabalho Git & Monorepo - Hora-Extra
---

# Fluxo de Trabalho Git & Monorepo - Hora-Extra

Este manual define como o versionamento deve ser conduzido no monorepo para manter um histórico limpo e facilitar o rastreio de mudanças entre Backend, Cliente e Documentos.

## 1. Convenção de Mensagens de Commit
Cada commit deve indicar claramente para qual parte do projeto a mudança se destina usando prefixos:

- `feat(client):` Nova funcionalidade no Unity.
- `feat(backend):` Nova funcionalidade no Node.js.
- `fix(client):` Correção de bug no Unity.
- `fix(backend):` Correção de bug no Node.js.
- `docs:` Mudanças em documentos (README, COMMUNICATIONS, etc).
- `assets:` Adição ou modificação de arquivos de arte/som/modelos.
- `chore:` Mudanças em configurações (build, dependências, etc).

**Exemplo:** `feat(client): implementado sistema de movimentação básica`

## 2. Commits Atômicos
- Realize commits focados. Evite misturar mudanças de lógica do servidor com mudanças de sprites do Unity no mesmo commit.
- Se você terminou uma funcionalidade de rede, faça um commit para o backend e outro para as mudanças correspondentes no cliente.

## 3. Idioma e Tom
- Mensagens de commit devem ser preferencialmente em **Português**, seguindo o tom profissional e direto do projeto.
- Use o imperativo (ex: "adiciona", "corrige", "implementa").

## 4. Estrutura do Monorepo
Mantenha a separação rígida:
- Não coloque scripts de utilidade geral do C# na pasta do Node.js e vice-versa.
- Referências compartilhadas devem ser documentadas ou colocadas em local comum se necessário.

---
*Este documento é uma Regra de Agente (Rule). Seguir o fluxo de commit ajuda a manter a sanidade do projeto em escala acadêmica e profissional.*
