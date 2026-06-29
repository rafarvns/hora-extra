# Contexto Codex do Hora-Extra

Este repositório mantém as antigas especificações do Antigravity em `.agents/` e adiciona contexto nativo para Codex.

## Como o Codex Carrega Contexto

- `AGENTS.md` na raiz define regras globais do monorepo.
- `hora-extra-backend/AGENTS.md` complementa as regras quando a tarefa envolve backend.
- `hora-extra-client/AGENTS.md` complementa as regras quando a tarefa envolve Unity/C#.
- Instruções de subpastas devem ser tratadas como mais específicas que as instruções da raiz.

## Skill Versionada

A skill convertida fica em:

`codex/skills/hora-extra-workflows/`

Ela contém workflows acionáveis para:

- planejamento de backend;
- execução de backend;
- planejamento de client Unity;
- execução de client Unity;
- planejamento e execução de integração backend-client;
- planejamento e execução de atividades de Product Owner.

Por estar versionada no repo, a skill não é instalada automaticamente em `~/.codex/skills`. Para uso local automático, copie ou instale essa pasta no diretório de skills do Codex conforme o fluxo da máquina.

## Relação com `.agents`

- `.agents/` permanece como fonte legada do Antigravity.
- Não remova nem mova `.agents/` sem uma decisão explícita.
- Durante a transição, mudanças em regras ou workflows devem ser refletidas tanto na fonte legada quanto no contexto Codex correspondente.

## Manutenção

- Regras gerais devem ficar no `AGENTS.md` da raiz.
- Regras específicas de backend devem ficar em `hora-extra-backend/AGENTS.md`.
- Regras específicas do Unity devem ficar em `hora-extra-client/AGENTS.md`.
- Procedimentos longos e workflows por tipo de tarefa devem ficar na skill `hora-extra-workflows`, não nos arquivos `AGENTS.md`.
