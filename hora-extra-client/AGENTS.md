# Contexto Codex - Client Unity Hora-Extra

Estas instruções valem para `hora-extra-client`.

## Regra de Testes

- Não crie, sugira ou implemente testes unitários no projeto Unity.
- Não crie pastas `Tests/` nem arquivos com sufixo `Tests.cs`.
- Valide o cliente por inspeção no Unity Editor, Play Mode, logs e testes manuais.
- Esta proibição vale apenas para o client Unity; o backend deve continuar usando testes automatizados.

## Padrões C# e Unity

- Use `PascalCase` para classes, métodos e variáveis públicas/serializadas.
- Use `_camelCase` para campos privados/protegidos.
- Use `camelCase` para parâmetros e variáveis locais.
- Use `SCREAMING_SNAKE_CASE` para constantes.
- Prefira `[SerializeField] private` a campos públicos editáveis no Inspector.
- Use `[Header]` e `[Tooltip]` para organizar campos expostos ao designer.
- Cacheie componentes em `Awake` ou `Start`; nunca faça `GetComponent` recorrente em `Update` ou `FixedUpdate`.
- Use `CompareTag` em vez de comparar `tag` diretamente.
- Evite alocações frequentes em loops de frame.

## Organização Unity

- Scripts devem ficar em `Assets/Scripts/`, separados por domínio como `Network`, `Gameplay`, `UI` e `Characters`.
- Prefabs devem ficar na área de prefabs do projeto e usar variantes/nested prefabs quando houver extensão de comportamento visual.
- Assets devem seguir organização por tipo: arte, materiais, sprites, áudio, cenas e plugins.
- Use prefixos consistentes para facilitar busca no editor, como `PFB_`, `SPR_`, `MAT_`, `SO_` e `SCN_`.
- Não faça mudanças permanentes em instâncias de prefab diretamente na cena; edite o asset ou aplique a alteração intencionalmente.

## Rede no Cliente

- A conexão de rede deve ser gerenciada pelo `SocketManager`.
- Centralize nomes de eventos em `Assets/Scripts/Network/NetworkEvents.cs`; não use strings literais espalhadas.
- DTOs e modelos de rede devem ficar em `Assets/Scripts/Network/Models/`.
- Use `Newtonsoft.Json` para serialização/parsing quando necessário.
- Sempre use `OnUnityThread` do SocketIOUnity antes de manipular GameObjects ou componentes Unity a partir de callbacks de rede.
- Componentes de gameplay não devem se inscrever diretamente no `SocketManager`; exponha eventos via `System.Action`, `UnityEvent` ou dispatcher equivalente.
- Inscreva listeners no `OnEnable` e remova no `OnDisable`.
- Trate `state_update` como snapshot e interpole movimento quando aplicável.

## Logs e Validação Manual

- Use prefixos de log como `[NETWORK]` e `[GAMEPLAY]`.
- Diferencie `Debug.Log`, `Debug.LogWarning` e `Debug.LogError`.
- Para funcionalidades de rede, valide com backend local quando possível e compare logs do servidor e do Unity.

## Documentação

- Documente novas mecânicas, UI, rede, assets importantes e mudanças de arquitetura em `hora-extra-client/Docs/`.
- Para rede, atualize também `Docs/Networking/COMMUNICATION.md` ou documento equivalente do contrato client-backend.
