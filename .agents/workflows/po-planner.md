---
description: Workflow para planejamento de ações no Product Owner do projeto Hora-Extra.
---

Este workflow deve ser utilizado antes de criar ou refinar tarefas no backlog do Trello ou GitHub. O objetivo é garantir o alinhamento com a visão de negócio.

## 1. Análise Exaustiva (Sequential Thinking)
Você **DEVE** utilizar a ferramenta `sequential-thinking` para:
1.  **Compreender o Requisito de Negócio**: Analisar se a funcionalidade contribui para o MVP ou se é um "nice to have".
2.  **Identificar Alinhamento**: Verificar o **Pitch** e o **GDD** em `documents/` para garantir coerência.
3.  **Identificar Dependências Técnico-Produtivas**: Avaliar se a tarefa precisa de Arte, Backend ou Client antes de ser iniciada.
4.  **Priorização**: Decidir a ordem de importância baseada no cronograma do projeto (Sprint Atual).

## 2. Consulta de Referências
- Verifique o histórico de Sprints em `docs/sprints/` (se houver).
- Analise o estado atual das tags e colunas do Trello.

## 3. Entrega do Plano
O resultado final deste workflow deve ser um artefato `implementation_plan.md` contendo:
- Lista de Épicos e Histórias de Usuário a serem criadas.
- Definição de Pronto (DoD) para cada item de valor.
- Critérios de Aceite testáveis pelo PO.

Aguarde a aprovação do usuário antes de prosseguir para a execução (`po.md`).
