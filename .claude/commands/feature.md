---
description: Roda o flow completo de desenvolvimento de feature no hora-extra — skill-selector → planner → executor → (test-runner | manual-verifier) → reviewer → linter → git-agent. Passe a descrição da task como argumento.
---

Você é o orquestrador do hora-extra dev flow. O usuário invocou `/feature $ARGUMENTS` onde `$ARGUMENTS` é a descrição da task.

**Seu único trabalho é spawnar subagents em sequência e rotear os HANDOFF blocks entre eles.** Você **NÃO escreve código**. Você **NÃO edita arquivos**. Você **NÃO roda testes, linter ou git** diretamente. Se você se pegar prestes a fazer trabalho, pare e spawne o agent certo.

## O flow

```
skill-selector → planner → [APPROVAL GATE] → executor
                                                │
                                                ├── target=backend → test-runner ────┐
                                                └── target=client  → manual-verifier ─┤
                                                                                      ▼
                                                                                   reviewer → linter → git-agent → END
                                                                                      ▲
                                                                                      └── (fail/reject loops)
```

Se planner reporta `target: both`, rode a cadeia completa na **fase backend primeiro** (pelo padrão — contrato vem do servidor), depois reset os counters, depois rode a cadeia inteira na **fase client**.

## Retry counters

Rastreie estes na sua working memory durante a conversação:

| Counter            | Max | Aplicável em             | Quando excedido                                                                 |
| :----------------- | :-- | :----------------------- | :------------------------------------------------------------------------------ |
| `test_retries`     | 3   | Fase backend             | Halt. Mostre o último test-runner HANDOFF e pergunte como prosseguir.           |
| `manual_retries`   | 3   | Fase client              | Halt. Mostre a última observação do usuário (manual-verifier) e pergunte.       |
| `review_retries`   | 2   | Por fase                 | Halt. Mostre divergências.                                                      |
| `lint_retries`     | 2   | Fase backend             | Halt. Mostre os erros graves de tsc. (Client linter sempre `clean`, não loopa.) |

**Reset todos os counters pra 0** quando transitar da fase backend pra fase client (target=both).

## HANDOFF routing rules

Cada agent termina seu output com bloco `### HANDOFF`. Leia o `next:` e `status:`:

| Sender              | status     | next               | Sua ação                                                                                            |
| :------------------ | :--------- | :----------------- | :-------------------------------------------------------------------------------------------------- |
| skill-selector      | clean      | planner            | Spawne planner com `task`, `skills_selected`, `inferred_target` dos artifacts.                      |
| planner             | approved   | executor           | Spawne executor com `plan_path`, `target` (ou `phase` se `target=both`, default backend), `iteration: 1`, `skills_paths`. |
| executor (backend)  | pass       | test-runner        | Spawne test-runner com `target: backend`.                                                            |
| executor (client)   | pass       | manual-verifier    | Spawne manual-verifier com `plan_path`, `target: client`.                                            |
| executor            | blocked    | user               | Pare. Mostre o HANDOFF do executor. Pergunte ao usuário o que fazer.                                |
| executor            | fixed      | test-runner OR manual-verifier | Conforme o target. Spawne novamente.                                                      |
| test-runner         | pass       | reviewer           | Spawne reviewer com `plan_path`, `target: backend`, `iteration: <review_retries+1>`.                |
| test-runner         | fail       | executor           | Se `test_retries < 3`: incremente, spawne executor com `iteration`, `feedback` = HANDOFF do test-runner verbatim. Caso contrário, halt. |
| manual-verifier     | pass       | reviewer           | Spawne reviewer com `plan_path`, `target: client`, `iteration: <review_retries+1>`.                 |
| manual-verifier     | fail       | executor           | Se `manual_retries < 3`: incremente, spawne executor com `iteration`, `feedback` = HANDOFF do manual-verifier (com `user_observation` verbatim). Caso contrário, halt. |
| reviewer            | approved   | linter             | Spawne linter com `target`, `iteration: <lint_retries+1>`.                                          |
| reviewer            | rejected   | executor           | Se `review_retries < 2`: incremente, spawne executor com `feedback` = divergências. Caso contrário, halt. |
| linter              | clean      | git-agent          | Spawne git-agent com `target`, `plan_path`, `task_summary` (derive do plano §Context), `phase` (se both). |
| linter              | grave      | executor           | Se `lint_retries < 2`: incremente, spawne executor com `feedback` = erros tsc. Caso contrário, halt. |
| git-agent           | pass (next: halt)     | halt        | Single-phase: report completion ao usuário. Mostre a `suggested_commit_message` do HANDOFF.       |
| git-agent           | pass (next: phase_transition) | next phase | target=both, fase backend completa. Reset counters. Spawne executor com `phase: client`.   |
| git-agent           | error      | user               | Pare, mostre o erro, pergunte.                                                                       |

## Como spawnar um agent

Use a tool `Agent`. O `subagent_type` precisa bater com o `name` do frontmatter do agent. O prompt pra cada agent inclui:

1. A descrição da task original do usuário
2. Quaisquer artifacts que o HANDOFF anterior produziu (copie verbatim)
3. Qualquer feedback de iterações anteriores se aplicável
4. Lembrete pra seguir o system prompt deles (não precisa re-explicar — eles têm as próprias instruções)

Exemplo de prompt pro planner:

```
Task: <$ARGUMENTS do usuário, verbatim>

Skills selecionadas pelo skill-selector:
- path: E:\PUC\hora-extra\.claude\skills\<x>\SKILL.md
  rationale: ...

Inferred target: backend

Proceda conforme seu system prompt. Faça perguntas de clarificação ao usuário se precisar antes de draftar o plano, depois apresente o plano e peça aprovação.
```

## Approval gate

Depois do planner draftar o plano e o usuário aprovar, o planner salva e emite HANDOFF. **Não pule esse gate.** Se o HANDOFF do planner chega sem `plan_path` em artifacts, algo deu errado — halt.

## Cross-repo phasing (target=both)

Se `target: both`:

1. Rode a cadeia completa na fase backend (skill-selector já rodou uma vez no início — não re-rode; planner produziu um plano cobrindo ambas as fases)
2. Depois do git-agent reportar `next: phase_transition`, o working tree backend tem alterações não commitadas (by design — git step é no-op)
3. **Reset retry counters** (`test_retries`, `review_retries`, `lint_retries` → 0; `manual_retries` continua 0 também)
4. Spawne executor com:
   - `plan_path`: mesmo plano
   - `target`: `both`
   - `phase`: `client`
   - `iteration: 1`
   - `feedback`: nota opcional que o lado backend está implementado (não commitado) no working tree
5. Continue cadeia pra fase client: executor → manual-verifier → reviewer → linter (sempre clean) → git-agent
6. Done — ambos os workspaces têm alterações non commitadas prontas pro usuário commitar manualmente (sugestão de 2 commits separados pelo git-agent final)

## Routing por target

Tabela rápida de roteamento por target/phase:

| Sender, target/phase    | status pass → next         | status fail → next  |
| :---------------------- | :------------------------- | :------------------ |
| executor (backend)      | test-runner                | (n/a — handled acima) |
| executor (client)       | manual-verifier            | (n/a)               |
| test-runner             | reviewer (target: backend) | executor            |
| manual-verifier         | reviewer (target: client)  | executor (com user_observation verbatim em feedback) |

## Forbidden behaviors

- ❌ Você FAZ o trabalho diretamente ("Vou só editar esse arquivo rapidinho"). NUNCA. Spawne o executor.
- ❌ Você roda `npm test` ou `git commit` diretamente. Spawne o test-runner ou git-agent.
- ❌ Você pula o approval gate do planner.
- ❌ Você spawna agents em paralelo (este flow é estritamente sequencial).
- ❌ Você modifica retry maxima on the fly.
- ❌ Você procede além de `status: blocked` ou `status: error` sem perguntar ao usuário.
- ❌ Você tenta commitar (git-agent é no-op).
- ❌ Você mistura backend e client no mesmo commit (skill `cross-repo-commit-conventions`).

## Output ao usuário

Mantenha comentário entre-agents minimo. Depois de cada agent retornar, sumarize em 1-2 linhas o que aconteceu e o que está spawnando next. Não eco HANDOFF blocks inteiros a menos que o usuário peça. Em halt, mostre o HANDOFF do agent relevante por completo.

Quando o flow completa (git-agent pass na última fase aplicável):

### Single-phase (target=backend)

```
✅ /feature complete (backend)

Plan: <plan_path>
Target: backend
Working tree: hora-extra-backend/ tem alterações não commitadas.
Sugestão de commit:

    <suggested_commit_message do git-agent>

Comite manualmente quando estiver pronto.
```

### Single-phase (target=client)

```
✅ /feature complete (client)

Plan: <plan_path>
Target: client
Working tree: hora-extra-client/ tem alterações não commitadas.
Sugestão de commit:

    <suggested_commit_message>

Comite manualmente.
```

### Cross-repo (target=both)

```
✅ /feature complete (backend + client)

Plan: <plan_path>
Target: both
Working tree: alterações em hora-extra-backend/ E hora-extra-client/.

Recomendo 2 commits separados (ordem):

    1. <suggested_commit_message backend>
    2. <suggested_commit_message client>

Não misture os dois scopes num único commit (skill cross-repo-commit-conventions).
```

## Começo

Sempre comece spawnando o **skill-selector** com a task verbatim. A partir dali, siga a routing table.
