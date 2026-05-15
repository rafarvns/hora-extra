---
name: cross-repo-docs-discipline
description: Aplicar quando adicionar/modificar feature que precisa de documentação. Define hierarquia espelhada `hora-extra-backend/docs/<Categoria>/` e `hora-extra-client/Docs/<Categoria>/` com categorias canônicas Networking/Mechanics/Arch/Infrastructure.
applies_to: both
---

# cross-repo-docs-discipline — Documentação espelhada por workspace

## Quando aplicar

- Toda **nova feature** (sistema, mecânica, endpoint, evento UDP) precisa de doc dedicada
- Toda **mudança que afeta API, payload, ou comportamento observável** precisa atualizar o doc existente
- Quando criar arquitetura nova (factory, pattern) — vai em `docs/Arch/`

## Quando NÃO aplicar

- Refator interno sem mudança comportamental — não inflar docs com detalhe de implementação que o código já mostra
- Hot-fix de bug — vai no commit, não em doc nova
- Skill (`.claude/skills/`), agent (`.claude/agents/`), CLAUDE.md — esses NÃO ficam em `docs/`

## Hierarquia obrigatória

Estrutura **espelhada** entre os dois workspaces:

```
hora-extra-backend/docs/
├── Networking/        ← Protocolos, UDP, REST, eventos
├── Mechanics/         ← Lógica de jogo (movimento, colisão, salas, NPC AI server-side)
├── Arch/              ← Padrões de design, factories, decorators
└── Infrastructure/    ← Setup, Docker, CI, envs, deploy

hora-extra-client/Docs/
├── Networking/        ← Mirror cliente do que o servidor expõe
├── Mechanics/         ← Mecânicas no cliente (animation, input, snapshot interpolation)
├── Arch/              ← Padrões no Unity (Singleton, Observer)
└── Infrastructure/    ← Build settings, Unity version, assets pipeline
```

> ⚠️ **Atenção ao casing.** Backend usa `docs/` (minúscula). Cliente usa `Docs/` (maiúscula — convenção Unity). Não inventar uma terceira.

## Onde colocar o quê — tabela de decisão

| Mudança                                       | Backend                              | Cliente                                  |
| :-------------------------------------------- | :----------------------------------- | :--------------------------------------- |
| Novo evento UDP                               | `docs/Networking/<Evento>.md` + atualizar `COMMUNICATION.md` | `Docs/Networking/<Evento>_Client.md` |
| Novo endpoint REST                            | `docs/Networking/<Endpoint>.md`      | `Docs/Networking/REST_Client_Usage.md` (atualizar) |
| Nova mecânica de jogo (sprint, dash, ataque)  | `docs/Mechanics/<Mecanica>.md` (server logic) | `Docs/Mechanics/<Mecanica>.md` (animation/input) |
| Novo padrão de design                         | `docs/Arch/<Pattern>.md`             | `Docs/Arch/<Pattern>.md`                 |
| Nova ScriptableObject ou prefab system        | N/A                                  | `Docs/Mechanics/<System>.md`             |
| Mudança em `.env` / Docker                    | `docs/Infrastructure/<Item>.md`      | N/A                                      |
| Build pipeline Unity (Addressables, etc.)     | N/A                                  | `Docs/Infrastructure/<Build>.md`         |

## Conteúdo mínimo de uma doc nova

A rule (`.agents/rules/docs-files.md`) exige 3 seções. Template:

```markdown
# <Nome da Feature>

## Descrição

<2-4 frases: o que é e por que existe. Foque no "para quê", não no "como".>

## Implementação

<Detalhes técnicos: arquivos envolvidos, fluxo de dados, decisões arquiteturais.
Cite paths absolutos quando ajudar (`src/sockets/handlers/PlayerMove.Handler.ts`).
Diagrama em ASCII ou descrição textual do fluxo se for não-trivial.>

## Uso

<Exemplo concreto: como invocar / acionar / testar. Snippet de código quando aplicável.
Se for evento UDP: pacote de exemplo no formato wire (`{e: ..., d: {...}}`).
Se for endpoint REST: `curl -X ...` ou exemplo no `ApiClient` do Unity.>
```

## Quando docs espelhadas valem o esforço

Para muitas features, **só um lado precisa de doc nova**. Espelhamento puro 1:1 é overkill. Heurística:

- **Backend-only**: novo endpoint admin, mudança interna de Prisma schema, lógica de validação server-side, factory nova → só `hora-extra-backend/docs/`.
- **Client-only**: animation system, UI prefab, input handling local → só `hora-extra-client/Docs/`.
- **Espelhada (ambos)**: protocolo de rede novo, mecânica que toca os 2 lados (player move, sprint, emote) → 1 doc em cada lado focando no seu ângulo. Nem repita conteúdo: cliente referencia backend para detalhes do protocolo.

## Documentos legados / avulsos

A rule menciona "documentos avulsos como `COMMUNICATION.md` na raiz devem ser movidos pra `docs/Networking/`". Status atual: **`COMMUNICATION.md` já está em `hora-extra-backend/docs/Networking/`** (correto). Cliente espelha em `hora-extra-client/Docs/Networking/COMMUNICATION.md` (que **pode** estar desatualizado — sempre verificar antes de duplicar).

> Decisão de design: o backend é o **owner** do `COMMUNICATION.md`. O cliente referencia/cita, não duplica conteúdo. Se ambos existem, mantenha o do cliente como "veja o backend pra detalhes" + diferenças específicas de parsing C#.

## Anti-patterns

- ❌ **Doc com só descrição, sem implementação ou uso** — não passa a checklist.
- ❌ **Doc detalhando linha por linha do código** — código muda; doc fica obsoleta. Foque em **decisões** e **fluxos**.
- ❌ **Categoria nova inventada** (`docs/Helpers/`, `docs/Misc/`) — usar as 4 canônicas. Se de fato não encaixa, pergunte ao usuário.
- ❌ **Doc dentro de `hora-extra-backend/src/`** — código tem JSDoc/comments; `docs/` é pra prosa.
- ❌ **Atualizar comportamento mas esquecer o doc** — rule explícita: "atualização DEVE acompanhar o mesmo commit".

## Checklist antes de fechar feature

- [ ] Doc nova OU update num arquivo existente em `<workspace>/docs|Docs/<Categoria>/`
- [ ] Categoria é uma das 4 canônicas (Networking / Mechanics / Arch / Infrastructure)
- [ ] Casing correto (`docs/` backend, `Docs/` cliente)
- [ ] Doc tem as 3 seções (Descrição, Implementação, Uso)
- [ ] Cross-link com `COMMUNICATION.md` se mexer em rede (ver `cross-repo-communication-sync`)
- [ ] Commit da doc usa prefixo `docs:` ou inclui no `feat(...)`/`fix(...)` quando é parte indissociável do change

## Gotchas

1. **`hora-extra-client/Docs/` vs `hora-extra-client/docs/`** — Unity tradicionalmente usa `Docs/` com maiúscula. Backend Node segue minúscula. Não unifique.
2. **`docs/Networking/COMMUNICATION.md`** é caso especial: vive só no backend (o cliente referencia). Não duplique sem coordenação.
3. **Doc de feature nova vs README** — README é entry point. Doc de feature é detail. Não inche o README global.
4. **Imagens em docs**: salve em `docs/<Categoria>/img/` (não na raiz). Backend pode embutir SVG/PNG; Unity tem Asset pipeline próprio, evite duplicar arte aqui.

## Referências

- `.agents/rules/docs-files.md` — rule humana
- `hora-extra-backend/docs/` — exemplos: `AUTHENTICATION.md`, `REST_API_GUIDE.md`, `PATTERN_FACTORY.md`, `TESTING_GUIDE.md`
- `hora-extra-client/Docs/Networking/` — exemplos: `UDP_Implementation.md`, `SESSION_MANAGEMENT.md`, `LOBBY_SYSTEM.md`
