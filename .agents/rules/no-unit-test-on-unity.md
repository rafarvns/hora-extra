# Proibiçao de Testes Unitários no Cliente (Unity) - Hora-Extra

Este documento estabelece uma regra absoluta e mandatória para o desenvolvimento do cliente no projeto "Hora-Extra".

## 1. Regra de Ouro
**É ESTRITAMENTE PROIBIDO criar, sugerir ou implementar testes unitários (Unit Tests) para o projeto Unity (`hora-extra-client`).**

## 2. Racional e Justificativa
- **Agilidade**: O projeto é um protótipo acadêmico/TCC focado em iteração rápida de mecânicas visuais e gameplay.
- **Complexidade Unity**: Testes unitários no Unity frequentemente exigem mocks pesados de GameObjects e componentes, consumindo tempo que deve ser focado na experiência do usuário.
- **Validação Manual**: A validação das funcionalidades do cliente deve ser feita visualmente no editor do Unity ou através de logs detalhados durante o Play Mode.

## 3. Substitutos para Testes
Em vez de testes unitários, utilize as seguintes práticas para garantir a qualidade:
- **Logging Robusto**: Use `Debug.Log`, `Debug.LogWarning` e `Debug.LogError` com os prefixos adequados (ex: `[NETWORK]`, `[GAMEPLAY]`).
- **Play Mode Testing**: Realize testes manuais no editor para validar comportamentos físicos e visuais.
- **Logs de Rede**: Verifique a consistência dos dados recebidos do servidor via console do Unity.

## 4. Exceção (Backend)
- Esta regra aplica-se **apenas** ao cliente Unity.
- Testes no backend Node.js (`hora-extra-backend`) podem ser implementados se necessário, mas não no cliente.

---
*Este documento é uma Regra de Agente (Rule) e deve ser seguido sem exceções. Não tente criar pastas `Tests/` ou arquivos com sufixo `Tests.cs`.*
