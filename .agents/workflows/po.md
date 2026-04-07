---
description: Workflow para auxiliar o Product Owner (PO) na gestão do projeto, criação de sprints, épicos e tarefas.
---

Este workflow define as etapas para que agentes auxiliem o PO na organização do backlog, planejamento de sprints e manutenção do Trello do projeto Hora-Extra.

## 1. Alinhamento com a Visão do Produto
Antes de sugerir novas tarefas ou épicos, o agente deve:
1. Ler o **Pitch** e o **GDD** em `documents/` para garantir que as sugestões estejam alinhadas com o core loop e o tema do jogo.
2. Identificar a fase atual do projeto (ex: Protótipo de Mecânicas, Integração de Rede).

## 2. Definição de Épicos e Histórias de Usuário
Ao criar novos Épicos:
1. **Épico**: Deve representar uma grande entrega de valor (ex: "Sistema de Inventário", "Sincronização de Movimento Multiplayer").
2. **User Stories**: Devem seguir o formato: *"Como [Papel], eu quero [Ação] para que [Valor]"*.
3. **Critérios de Aceite**: Devem ser claros e testáveis.

## 3. Planejamento de Sprint
Ao auxiliar na criação de uma nova Sprint:
1. Analisar o backlog atual em busca de dependências técnicas.
2. Sugerir tarefas que caibam no período da sprint (ex: 1 semana).
3. Documentar o plano da sprint em um arquivo `sprint_<n>.md` temporário ou formatado para o Trello.

## 4. Gestão do Trello
O projeto utiliza o Trello para acompanhamento visual. As tarefas devem ser formatadas para fácil importação ou cópia:
- **Título**: Curto e acionável.
- **Descrição**: Incluir Contexto, Requisitos e Definição de Pronto (DoD).
- **Etiquetas**: Sugerir etiquetas como `Backend`, `Client`, `Arte`, `Bug`, `Documentação`.

## 5. Manutenção do Backlog
Regularmente, o agente deve:
1. Identificar tarefas obsoletas ou duplicadas.
2. Refinar tarefas vagas adicionando detalhes técnicos baseados no estado atual do código.
3. Sugerir prioridades com base no impacto no gameplay (MVP) e riscos técnicos.

## 6. Template de Tarefa (DoD)
Toda tarefa sugerida deve conter:
- **[ ] Descrição Clara**: O que precisa ser feito.
- **[ ] Critérios de Aceite**: Como saberemos que está pronto.
- **[ ] Referência Técnica**: Arquivos ou sistemas impactados.
- **[ ] Teste**: Como o PO ou QA pode validar a entrega.

---
*Link do Trello: [Trello Hora-Extra](https://trello.com/invite/b/699504957b787149b01c3b8d/ATTI3822b46621093641c29b7920fc0c59c2C682D9D8/pitch)*
