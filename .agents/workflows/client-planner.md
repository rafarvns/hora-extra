---
description: Workflow para planejamento de ações no frontend (Unity/C#) do projeto Hora-Extra.
---

Este workflow deve ser utilizado antes de qualquer implementação técnica no cliente Unity para garantir que a arquitetura e os padrões de rede foram respeitados.

## 1. Análise Exaustiva (Sequential Thinking)
Você **DEVE** utilizar a ferramenta `sequential-thinking` para:
1.  **Compreender o Requisito**: Analisar o impacto visual e lógico no gameplay.
2.  **Mapear Scripts**: Identificar quais scripts em `Assets/Scripts/` serão alterados ou criados.
3.  **Identificar o Padrão**: Definir se a funcionalidade segue o `client-design-pattern.md` (especialmente para rede).
4.  **Definição de UI/Assets**: Verificar se novos prefabs, sprites ou animações são necessários.

## 2. Consulta de Referências
- Verifique `.agents/rules/client-design-pattern.md` para padrões de rede (SocketManager/NetworkEvents).
- Verifique `.agents/rules/csharp-coding-standards.md` para padrões de código.
- Verifique `.agents/rules/no-unit-test-on-unity.md` para diretrizes de verificação.

## 3. Entrega do Plano
O resultado final deste workflow deve ser um artefato `implementation_plan.md` contendo:
- Lista de scripts C# a serem criados/modificados.
- Definição de eventos de rede (se houver comunicação).
- Impacto nos componentes do Unity Inspector (`[SerializeField]`, `[Header]`).

Aguarde a aprovação do usuário antes de prosseguir para a execução (`client.md`).
