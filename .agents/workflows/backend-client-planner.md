---
description: Workflow para planejamento de tarefas que envolvem o backend e frontend em conjunto no projeto Hora-Extra.
---

Este workflow deve ser utilizado para qualquer funcionalidade que exija comunicação entre o servidor (Node.js) e o cliente (Unity), como novas mecânicas de rede.

> [!CAUTION]
> **ESTE WORKFLOW É APENAS PARA PLANEJAMENTO. O AGENTE NÃO DEVE ESCREVER CÓDIGO NO BACKEND OU MODIFICAR O CLIENTE UNITY ATÉ QUE O PLANO SEJA APROVADO.**

## 1. Análise Exaustiva (Sequential Thinking)
Você **DEVE** utilizar a ferramenta `sequential-thinking` para:
1.  **Compreender o Requisito de Rede**: Analisar se o evento é de alta frequência (movimento) ou baixa frequência (login/item).
2.  **Definir Payload**: Estruturar o JSON que será enviado e recebido.
3.  **Mapear Sincronização**: Identificar quais campos do banco de dados no Backend refletem em scripts no Client.
4.  **Otimização de Banda**: Decidir por chaves curtas para eventos de alta performance.

## 2. Consulta de Referências
- Verifique `hora-extra-client/Docs/Networking/COMMUNICATION.md`.
- Verifique `.agents/rules/client-design-pattern.md` e `.agents/rules/backend-design-pattern.md`.

## 3. Entrega do Plano
O resultado final deste workflow deve ser um artefato `implementation_plan.md` contendo:
- Definição do Contrato de Rede (Evento e Payload).
- Impacto nos modelos do Prisma (Backend) e Models (Client).
- Mecanismo de Interpolação ou Reconciliação se necessário.
- **Testes (TDD)**: Definir testes unitários para a lógica de backend que será integrada.
- **Documentação (OBRIGATÓRIO)**: Planejar onde a funcionalidade será documentada em `e:\PUC\hora-extra\hora-extra-backend\docs` e `e:\PUC\hora-extra\hora-extra-client\Docs`.

Aguarde a aprovação do usuário antes de prosseguir para a execução (`backend-client.md`). **Não execute nenhuma tarefa técnica até lá.**
