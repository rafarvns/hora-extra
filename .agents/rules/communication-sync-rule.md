---
description: Regra de Sincronia de Comunicação - Hora-Extra
---

# Regra de Sincronia de Comunicação - Hora-Extra

A consistência entre o que o Servidor envia e o que o Cliente recebe é crítica para o sucesso do co-op. Este documento institui a obrigatoriedade de manter a documentação atualizada.

## 1. A Regra de Ouro da Documentação
**Qualquer** modificação técnica em eventos de rede deve ser refletida no arquivo `COMMUNICATION.md` na raiz do projeto antes da conclusão da tarefa.

## 2. O que deve ser sincronizado?
Sempre que você:
- Adicionar um novo evento constante em `NetworkEvents.cs` (Unity).
- Adicionar ou modificar um listener de socket no `SocketManager.ts` (Backend).
- Mudar a estrutura de um JSON (Payload) enviado via rede.

**Você deve atualizar a tabela correspondente no `COMMUNICATION.md`.**

## 3. Validação Cruzada (Cross-Check)
- Antes de subir código de rede, verifique se os tipos de dados no `COMMUNICATION.md` batem com as classes/interfaces no C# (Models) e TypeScript (Interfaces).
- Verifique se os nomes das chaves (Ex: `p` em vez de `position`) foram corretamente atualizados em ambos os lados e na documentação.

---
*Este documento é uma Regra de Agente (Rule). O COMMUNICATION.md é o contrato de confiança entre o cliente e o servidor.*
