---
description: Workflow para execução de tarefas no frontend (Unity/C#) do projeto Hora-Extra.
---

Este workflow assume que um plano de implementação (`implementation_plan.md`) já foi aprovado. O foco aqui é a implementação técnica no Unity Editor e C#.

## 1. Implementação de Scripts (C#)
1. **Localização**: Siga as subpastas em `Assets/Scripts/` (ex: `UI/`, `Gameplay/`, `Network/`).
2. **Nomenclatura**: `PascalCase` para classes e métodos. `_camelCase` para privados.
3. **Inspector**: Use `[SerializeField]` com `private` para variáveis que o designer precise ver no Editor.
4. **Cache**: Cacheie componentes no `Awake` ou `Start`. Nunca no `Update`.

## 2. Implementação de Rede (Socket.IO)
1. **Eventos**: Registre em `Assets/Scripts/Network/NetworkEvents.cs`.
2. **DTOs**: Crie classes POCO em `Assets/Scripts/Network/Models/`.
3. **Escuta (C<-S)**: Use `OnUnityThread` no wrapper para registrar listeners. Use o padrão **Observer**.
4. **Emissão (C->S)**: Use `SocketManager.Instance.Emit("evento", payload)`.

## 3. Gestão de Assets no Unity
1. **Prefabs**: Use Prefab Variants ao estender objetos.
2. **Organização**: Novos arquivos de arte em `Assets/Art/`, sons em `Assets/Audio/`.
3. **Naming**: Siga o padrão: `PF_` para Prefabs, `S_` para Sprites/Scripts.

## 4. Verificação Manual
1. **Compilação**: Certifique-se de que não há erros no Console do Unity.
2. **Play Mode**: Valide visualmente e fisicamente no editor.
3. **Rede**: Teste com instâncias conectadas ao servidor local se houver networking.

## 5. Práticas Proibidas
- **NUNCA** crie suítes de testes unitários ou pastas `Tests/` no Unity.
- **NUNCA** use `GameObject.Find` ou interações diretas em threads secundárias.

## 6. Documentação (OBRIGATÓRIO)
1. **Localização**: Adicione ou atualize a documentação em `e:\PUC\hora-extra\hora-extra-client\Docs`.
2. **Padrão**: Utilize Markdown e inclua diagramas Mermaid se houver lógica complexa de networking ou estado.

## 7. Relatório
Crie o artefato `walkthrough.md` com prints ou vídeos de demonstração se possível.
