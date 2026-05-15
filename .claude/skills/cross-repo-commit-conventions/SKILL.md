---
name: cross-repo-commit-conventions
description: Aplicar quando estiver organizando working tree changes para commit no monorepo hora-extra. Define prefixos pt-BR (`feat(backend):`, `feat(client):`, `docs:`, etc.), atomicidade (1 concern por commit) e split obrigatório entre backend e client.
applies_to: both
---

# cross-repo-commit-conventions — Mensagens de commit no monorepo

## Quando aplicar

- Sempre que for **sugerir** uma mensagem de commit ao usuário
- Sempre que organizar a working tree para commit (decidir quais arquivos vão juntos)
- Antes de descrever a um humano o que precisa ser commitado depois de uma sequência de mudanças

## Quando NÃO aplicar

- O git-agent é **no-op** no flow `/feature` — quem comita é o usuário, manualmente. Nunca rode `git commit` automaticamente.

## Prefixos obrigatórios

Lista canônica (de `.agents/rules/git-monorepo-workflow.md`):

| Prefixo          | Quando usar                                                      |
| :--------------- | :--------------------------------------------------------------- |
| `feat(backend):` | Nova funcionalidade no servidor Node.js                          |
| `feat(client):`  | Nova funcionalidade no cliente Unity                             |
| `fix(backend):`  | Correção de bug no servidor                                      |
| `fix(client):`   | Correção de bug no Unity                                         |
| `docs:`          | Apenas docs (`COMMUNICATION.md`, `README.md`, `Docs/`)           |
| `assets:`        | Sprites, prefabs, materiais, ScriptableObjects, scenes, áudio    |
| `chore:`         | Configs (`package.json`, `tsconfig.json`, `.gitignore`, scripts) |

## Atomicidade — regra dura

**Um commit, um concern.** Se a sua working tree tem mudanças em ambos `hora-extra-backend/` E `hora-extra-client/`, **divida em 2+ commits**.

Caso típico: feature de rede que toca os dois lados:

```
1. git add hora-extra-backend/src/sockets/handlers/PlayerEmote.Handler.ts \
           hora-extra-backend/src/sockets/handlers/PlayerEmote.Handler.test.ts \
           hora-extra-backend/src/sockets/factories/SocketHandler.Factory.ts \
           hora-extra-backend/docs/Networking/COMMUNICATION.md
   git commit -m "feat(backend): adiciona handler UDP de player_emote"

2. git add hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs \
           hora-extra-client/Assets/Scripts/Network/Models/PlayerEmote.cs \
           hora-extra-client/Assets/Scripts/Characters/EmoteController.cs \
           hora-extra-client/Docs/Networking/Emotes.md
   git commit -m "feat(client): adiciona dispatcher de emote sincronizado"
```

> `COMMUNICATION.md` vive **dentro de `hora-extra-backend/docs/`**. Como ele é parte do contrato backend, sobe junto com o commit do backend (não como `docs:` separado). Exceção: edição puramente textual sem mudança de payload → `docs:`.

## Idioma e tom

- **Português, imperativo.** "adiciona", "corrige", "remove", "renomeia", "implementa", "refatora".
- Não usar passado ("adicionou") nem inglês ("add").
- Conciso: 50–72 chars no título.
- Corpo opcional para detalhar **o "porquê"** (não o "o quê" — o diff já diz).

### Exemplos bons

```
feat(backend): adiciona validação anti-cheat de velocidade em PlayerMove
feat(client): adiciona snapshot interpolation no PlayerController
fix(backend): corrige timeout de sessão UDP de 30s para 60s
fix(client): corrige NRE em SocketManager quando token expira
docs: documenta evento player_emote em COMMUNICATION.md
assets: adiciona sprite SPR_EmoteBubble_Wave
chore: atualiza vitest para 1.6.0
```

### Exemplos ruins (não fazer)

```
fix: corrige bugs                        ← sem escopo
feat: adicionados emotes                 ← passado, sem escopo
feat(backend e client): implementa emote ← misturou dois scopes
feat(backend): adds emote handler        ← inglês
update: refactor                         ← prefixo inválido
```

## Cross-commit metadata (opcional)

Se uma feature precisa de 2 commits (backend + client) mas você quer linkar:

```
feat(backend): adiciona handler UDP de player_emote

Parte 1/2 da feature de emotes (ver feat(client): … no commit seguinte).
COMMUNICATION.md atualizado com tabela §3 e §4.
```

E no segundo:

```
feat(client): adiciona dispatcher de emote sincronizado

Parte 2/2. Depende do handler do servidor (commit anterior).
```

Mas só faça isso quando o split for não-óbvio. Em features típicas, o usuário já entende que `feat(backend):` veio com `feat(client):` no mesmo PR.

## Anti-patterns

- ❌ **`git add .`** — pega arquivos não intencionais (`.env`, `node_modules` se `.gitignore` falhar, lockfiles parciais). Sempre liste arquivos.
- ❌ **Commit gigante "implementa feature X completa"** — perde rastreabilidade. Divida por concern.
- ❌ **Misturar refator com feature** — `feat:` pra coisa nova, `chore:` ou `refactor:` (se aceito pelo time) pra cleanup.
- ❌ **Commitar `prisma/schema.prisma.bak`** acidentalmente — esse é regenerado por `scripts/db-setup.ts`, vai pra `.gitignore`. Confira antes.
- ❌ **Commitar `dev.db`** (SQLite local) — também é local, ignorado.

## Branch naming (referência rápida)

Não obrigatório pela rule, mas convenção observada nos commits recentes:

```
feat/implementa-cadastro-login-lobby
feature/blocagem-props-escritorio
fix/timeout-sessao-udp
docs/communication-emote-event
```

Mesmo padrão pt-BR/imperativo do commit, com prefixo de branch type (`feat/`, `feature/`, `fix/`, `docs/`).

## Checklist antes de sugerir commit

- [ ] Verifiquei `git status` — sei quais arquivos estão modificados
- [ ] Cada commit toca **apenas** `hora-extra-backend/` OU `hora-extra-client/` OU root (não 2+ simultâneos sem motivo)
- [ ] Prefixo bate com o conteúdo (`feat` pra novo, `fix` pra bug, `docs` pra só texto, etc.)
- [ ] Mensagem em pt-BR, imperativo, ≤ 72 chars
- [ ] Nenhum arquivo sensitive (`.env`, credenciais, `dev.db`) listado
- [ ] Se a mudança é cross-repo (backend+client), planejei N≥2 commits

## Gotchas

1. **Repo monorepo único**: `git add hora-extra-backend/foo.ts` funciona da raiz. Não use `git -C hora-extra-backend add ...` — não é necessário aqui (é 1 repo, não 2).
2. **`prisma/schema.prisma` muda em todo `npm run dev`** se `USE_SQLITE` ≠ baseline do `.bak`. Não commite o flip — só commite mudanças deliberadas no schema. Inspecione o diff antes.
3. **Unity asset metadata (`*.meta`)** sempre acompanha o arquivo. Se commita `PFB_X.prefab`, commita `PFB_X.prefab.meta` junto. Reviewer flagga `.meta` órfão.
4. **Mensagens longas via heredoc** se for usar `gh` ou multi-linha — single-line `-m "..."` é mais seguro pra prefixos curtos.

## Referências

- `.agents/rules/git-monorepo-workflow.md` — rule humana
- `git log --oneline -20` no repo — exemplos reais recentes
