---
description: Workflow para planejamento de ações no backend do projeto Hora-Extra.
---

Este workflow deve ser utilizado antes de qualquer implementação técnica no backend para garantir que a arquitetura e os impactos foram devidamente analisados.

## 1. Análise Exaustiva (Sequential Thinking)
Você **DEVE** utilizar a ferramenta `sequential-thinking` para:
1.  **Compreender o Requisito**: Analisar o que o usuário solicitou em relação ao estado atual do servidor.
2.  **Identificar o Modelo**: Definir se a funcionalidade é **REST (Stateless)** ou **Real-time (Stateful/Socket)**.
3.  **Mapear Dependências**: Verificar quais serviços, controllers ou modelos do Prisma serão afetados.
4.  **Definição de Design**: Decidir se novos handlers, serviços ou factories são necessários seguindo o `backend-design-pattern.md`.

## 2. Consulta de Referências
- Verifique `hora-extra-backend/docs/REST_API_GUIDE.md` para padrões de rota.
- Verifique `.agents/rules/backend-design-pattern.md` para padrões de Game Server.
- Verifique o schema do Prisma em `hora-extra-backend/prisma/schema.prisma`.

## 3. Entrega do Plano
O resultado final deste workflow deve ser um artefato `implementation_plan.md` contendo:
- Mudanças propostas no banco de dados.
- Novos endpoints ou eventos de socket.
- Lógica de validação (Server-Authoritative).

Aguarde a aprovação do usuário antes de prosseguir para a execução (`backend.md`).
