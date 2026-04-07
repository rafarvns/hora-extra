---
description: Workflow para execução de tarefas que envolvem o backend e frontend em conjunto no projeto Hora-Extra.
---

Este workflow assume que o contrato de rede e o plano de implementação (`implementation_plan.md`) já foram aprovados.

## 1. Contrato (Documentação)
1. Atualize obrigatoriamente o arquivo `hora-extra-client/Docs/Networking/COMMUNICATION.md` com o novo evento.
2. Certifique-se de que as chaves curtas para payloads de alta frequência estão documentadas.

## 2. Implementação Backend (Server-Authoritative)
1. **Handler**: Crie o handler em `src/sockets/handlers/`.
2. **Interface**: Defina o payload em `src/types/`.
3. **Lógica**: Implemente a validação antes do broadcast.
// turbo
4. **Build**: Execute `npm run build` no backend.

## 3. Implementação Frontend (Unity Client)
1. **Events**: Adicione em `Assets/Scripts/Network/NetworkEvents.cs`.
2. **Models**: Crie o DTO em `Assets/Scripts/Network/Models/`.
3. **Escuta/Emissão**: Implemente as chamadas no `SocketManager.cs` respeitando a thread principal do Unity.

## 4. Teste de Integração (Manual)
1. **Logs Cruzados**: Mantenha logs claros (`[SOCKET]` no Server e `[NETWORK]` no Unity) para depuração.
2. **Validação**: Verifique se o servidor ignora dados malformados ou ilegais.
3. **Checklist**:
   - Conexão ok?
   - Payload chegou nos dois lados?
   - Estado foi persistido?

## 5. Relatório
Crie o artefato `walkthrough.md` com o resumo da integração e resultados dos testes manuais.
