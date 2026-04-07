---
description: Workflow para ações no frontend (Unity/C#) do projeto Hora-Extra.
---

Este workflow deve ser seguido para qualquer modificação ou adição de novas funcionalidades no cliente Unity (C#).

## 1. Planejamento e Arquitetura
Antes de codar, identifique a natureza da funcionalidade:
- **UI/Interface**: Novos menus ou componentes visuais.
- **Gameplay**: Lógica de jogo local (movimentação, combate).
- **Networking**: Sincronização em tempo real (Socket.IO).
- Consulte `.agents/rules/client-design-pattern.md` para padrões de rede.
- Consulte `.agents/rules/csharp-coding-standards.md` para padrões de código.

## 2. Implementação de Scripts (C#)
1. **Localização**: Crie scripts em `Assets/Scripts/` seguindo a subcategoria apropriada (ex: `UI/`, `Gameplay/`, `Network/`).
2. **Nomenclatura**: Use `PascalCase` para classes e métodos. Use `_camelCase` para campos privados.
3. **Inspector**: Sempre utilize `[SerializeField]` com `private` para variáveis que precisam ser expostas no Inspector. Use `[Header]` e `[Tooltip]` para organização.
4. **Cache**: Cacheie componentes (`GetComponent`) no `Awake` ou `Start`. **Nunca** no `Update`.

## 3. Implementação de Rede (Socket.IO)
Se a funcionalidade requer comunicação com o servidor:
1. **Eventos**: Registre o nome do evento em `Assets/Scripts/Network/NetworkEvents.cs` como uma constante estática.
2. **DTOs**: Crie uma classe simples (POCO) em `Assets/Scripts/Network/Models/` para representar o payload JSON.
3. **Escuta (C<-S)**:
   - Registre a escuta no `SocketManager.cs` utilizando o método `_socket.OnUnityThread`.
   - Use o padrão **Observer**: o `SocketManager` dispara um evento `System.Action` que os scripts de gameplay assinam.
4. **Emissão (C->S)**:
   - Use `SocketManager.Instance.Emit("nome_evento", payload)` para enviar dados ao servidor.

## 4. Gestão de Assets e Prefabs
1. **Prefabs**: Sempre trabalhe com **Prefab Variants** se estiver estendendo um objeto base.
2. **Organização**: Novos modelos, sprites ou sons devem ser colocados em suas respectivas pastas sob `Assets/Art/` ou `Assets/Audio/`.
3. **Naming**: Siga o padrão do projeto para assets (ex: `PF_Player`, `S_Wall`).

## 5. Práticas Obrigatórias
- **Thread Safety**: Callbacks de rede chegam em threads secundárias. **Sempre** utilize `OnUnityThread` do wrapper Socket.IO para interagir com o Unity.
- **Newtonsoft.Json**: Utilize o atributo `[JsonProperty("name")]` se o nome da propriedade no servidor divergir do padrão C#.
- **Sem Testes Unitários**: **NUNCA** crie testes unitários ou pastas de `Tests/` no Unity. Siga a regra `.agents/rules/no-unit-test-on-unity.md`.
- **Desempenho**: Use `Vector3.sqrMagnitude` para checar distâncias. Evite `GameObject.Find`.

## 6. Verificação e Testes (Manuais)
1. **Compilação**: Certifique-se de que não há erros de compilação no console do Unity.
2. **Play Mode**: Realize testes manuais no editor para validar comportamentos físicos e visuais.
3. **Cenário Multi-instância**: Se for uma funcionalidade de rede, teste com duas instâncias do Unity ou uma build local conectada ao servidor local.
4. **Sem Testes Automatizados**: Ignore qualquer sugestão ou tentativa de criar suítes de testes automatizados para o Unity.
5. **Documentação**: Atualize ou crie arquivos `.md` em `hora-extra-client/Docs/` se houver mudanças estruturais significativas.
