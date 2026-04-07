---
description: Workflow para tarefas que envolvem o backend e frontend em conjunto no projeto Hora-Extra.
---

Este workflow deve ser seguido para qualquer funcionalidade que exija comunicação entre o servidor (Node.js) e o cliente (Unity), como novas mecânicas co-op ou sincronização de estado.

## 1. Definição do Contrato (COMMUNICATION.md)
Antes de qualquer código, defina como os dois lados vão conversar:
1. **Documentação**: Atualize o arquivo `hora-extra-client/Docs/Networking/COMMUNICATION.md`.
2. **Eventos**: Defina o nome do evento e a estrutura exata do JSON (Payload).
3. **Otimização**: Se for um evento de alta frequência (ex: movimento), use chaves curtas (ex: `p` para `position`).
4. **Sincronia**: Garanta que os tipos de dados batem entre TypeScript e C#.

## 2. Implementação no Backend (Server-Authoritative)
O servidor é a fonte da verdade. Comece por ele:
1. **Handler**: Crie um novo handler em `src/sockets/handlers/`.
2. **Mensagem**: Defina a interface do payload em `src/types/`.
3. **Lógica**: Implemente a lógica de validação. O servidor deve autorizar a ação antes de processar.
4. **Broadcast**: Se necessário, emita o estado atualizado para os outros jogadores na sala (ex: `socket.to(room).emit(...)`).
// turbo
5. **Build**: Execute `npm run build` para validar a tipagem.

## 3. Implementação no Frontend (Unity Client)
Consuma a funcionalidade no cliente:
1. **NetworkEvents**: Adicione a nova constante de string em `Assets/Scripts/Network/NetworkEvents.cs`.
2. **DTO (Model)**: Crie a classe C# correspondente ao payload JSON em `Assets/Scripts/Network/Models/`.
3. **Escuta/Emissão**:
   - Para enviar: `SocketManager.Instance.Emit(NetworkEvents.EVENT_NAME, payload)`.
   - Para receber: Registre o listener no `SocketManager.cs` vinculando a um evento `System.Action` do C#.
4. **Main Thread**: Lembre-se de usar `_socket.OnUnityThread` para qualquer interação com GameObjects.

## 4. Fluxo de Teste Integrado
Teste a comunicação de ponta a ponta:
1. **Servidor**: Inicie o backend (`npm start`). Monitore os logs com o prefixo `[SOCKET]`.
2. **Cliente**: Rode o Unity em **Play Mode**.
3. **Checklist**:
   - O cliente se conectou com sucesso (Token JWT válido)?
   - O evento foi enviado e recebido pelo servidor?
   - O servidor processou e devolveu a resposta correta?
   - Os logs em ambos os lados (`[NETWORK]` no Unity e `[SOCKET]` no Node) confirmam a troca de dados?
4. **Latência**: Verifique se a interpolação no Unity está lidando bem com os dados recebidos.

## 5. Práticas Obrigatórias
- **Fail Fast**: Se o servidor receber dados inválidos, ele deve ignorar ou desconectar o cliente malicioso.
- **Tipagem Forte**: Não use `any` no TypeScript nem `object` genérico no C# para payloads. Use interfaces e classes.
- **Sem Testes Automatizados no Unity**: Siga a regra `.agents/rules/no-unit-test-on-unity.md`. Valide visualmente.
- **Logs Cruzados**: Mantenha logs claros de entrada e saída em ambos os lados para facilitar o debugging.
