---
name: client-new-mono-behaviour
description: Aplicar quando criar script C# `MonoBehaviour` no Unity. Define `[SerializeField] private _camelCase`, cache de GetComponent em `Awake`, `[Header]/[Tooltip]` no inspector, Observer pattern via `event Action<>`, subscribe em `OnEnable`/unsubscribe em `OnDisable`, marshal de thread para callbacks de rede.
applies_to: client
---

# client-new-mono-behaviour — Criar MonoBehaviour no hora-extra-client

## Quando aplicar

- Novo controller (Player, NPC, UI, AI, sistema de gameplay)
- Script anexado a um GameObject que precisa de lifecycle Unity (`Awake`, `Start`, `Update`, etc.)
- Componente reativo a evento (input, rede, colisão)

## Quando NÃO aplicar

- Classe utility / DTO / model — vai em `Network/Models/`, sem herdar de `MonoBehaviour`
- ScriptableObject — herde `ScriptableObject` (pasta separada do MB)
- Singleton de serviço (SocketManager, ApiClient) — esses **são** MB com `DontDestroyOnLoad`, mas têm padrão próprio (não criar outros singletons sem alinhar)

## Template canônico

`hora-extra-client/Assets/Scripts/Characters/EmoteController.cs`:

```csharp
using System;
using UnityEngine;
using HoraExtra.Network;            // SocketManager, NetworkEvents
using HoraExtra.Network.Models;     // PlayerEmoteRequest

namespace HoraExtra.Characters
{
    /// <summary>
    /// Controla o disparo de emotes locais e a reação a emotes broadcast pelo servidor.
    /// </summary>
    public class EmoteController : MonoBehaviour
    {
        // === Constantes ===
        private const float EMOTE_COOLDOWN_SECONDS = 1.5f;

        // === Inspector ===
        [Header("Animation")]
        [Tooltip("Animator que recebe o trigger 'EmoteWave', 'EmoteDance', etc.")]
        [SerializeField] private Animator _animator;

        [Header("Input")]
        [Tooltip("Tecla que dispara o emote local")]
        [SerializeField] private KeyCode _emoteKey = KeyCode.E;

        // === Estado privado ===
        private float _lastEmoteAt = -999f;

        // === Eventos públicos (Observer pattern) ===
        public static event Action<string, string> OnEmoteReceived;  // (playerId, emoteId)

        // === Lifecycle ===

        private void Awake()
        {
            // Cache de referências — NUNCA em Update.
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
            // Observer: subscribe em rede
            SocketManager.Instance.On(NetworkEvents.PLAYER_EMOTE_BC, OnNetworkEmote);
        }

        private void OnDisable()
        {
            // Sempre unsubscribe pra evitar leak / NRE depois de Destroy
            SocketManager.Instance?.Off(NetworkEvents.PLAYER_EMOTE_BC, OnNetworkEmote);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_emoteKey))
            {
                TryDispatchEmote("wave");
            }
        }

        // === Métodos públicos ===

        public void TryDispatchEmote(string emoteId)
        {
            if (Time.time - _lastEmoteAt < EMOTE_COOLDOWN_SECONDS)
            {
                Debug.Log($"[GAMEPLAY] Emote em cooldown ({EMOTE_COOLDOWN_SECONDS}s)");
                return;
            }

            _lastEmoteAt = Time.time;

            var payload = new PlayerEmoteRequest { Id = emoteId };
            SocketManager.Instance.SendEvent(NetworkEvents.PLAYER_EMOTE, payload);
            PlayLocal(emoteId);

            Debug.Log($"[GAMEPLAY] Emote '{emoteId}' enviado");
        }

        // === Métodos privados ===

        private void OnNetworkEmote(string raw)
        {
            // Callback pode chegar em worker thread — marshal pra main thread!
            // SocketManager já enfileira via _mainThreadQueue; aqui assumimos main thread.
            try
            {
                var payload = JsonUtility.FromJson<PlayerEmoteRequest>(raw);  // ou Newtonsoft, se nesse projeto
                Debug.Log($"[NETWORK] Recebido player_emote: {payload.Id}");
                OnEmoteReceived?.Invoke("from-broadcast", payload.Id);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NETWORK] Falha ao parsear emote: {ex.Message}");
            }
        }

        private void PlayLocal(string emoteId)
        {
            string triggerName = $"Emote{char.ToUpper(emoteId[0]) + emoteId.Substring(1)}";
            _animator?.SetTrigger(triggerName);
        }
    }
}
```

## Convenções obrigatórias (csharp-coding-standards.md)

### Nomenclatura

| Tipo                      | Padrão                  | Exemplo                  |
| :------------------------ | :---------------------- | :----------------------- |
| Classes, métodos          | `PascalCase`            | `EmoteController`, `TryDispatchEmote()` |
| Variáveis públicas / SerializeField | `_camelCase` (com underscore!) | `_animator`, `_emoteKey` |
| Parâmetros, locais        | `camelCase`             | `emoteId`, `triggerName` |
| Constantes                | `SCREAMING_SNAKE_CASE`  | `EMOTE_COOLDOWN_SECONDS` |

> ⚠️ A rule mistura "públicas/serializadas usa PascalCase" com "privadas usa _camelCase". Na prática real do hora-extra: **prefira `[SerializeField] private _camelCase`** em vez de `public PascalCase` — encapsulamento melhor, mesmo Inspector exposure. PascalCase só para algo verdadeiramente público (raro).

### Inspector

- **`[Header("Categoria")]`** agrupa campos no Inspector
- **`[Tooltip("Explicação")]`** vira tooltip ao hover
- **`[SerializeField] private <type> _name;`** > `public <type> Name;`

### Cache de componentes

- `GetComponent<T>()`, `GameObject.Find(...)`, `FindObjectOfType<T>()` → **só em `Awake`/`Start`**
- **Nunca** em `Update`, `FixedUpdate`, `LateUpdate`, ou loops

### Tags / Layers

- `CompareTag("Player")` — não `gameObject.tag == "Player"` (aloca string)

### Single Responsibility

- 1 script, 1 conceito. `PlayerController` não deve fazer UI; `UIController` não deve fazer física.

## Observer pattern (gameplay events)

```csharp
public static event Action<string> OnPlayerJoined;  // (playerId)
public static event Action OnGameStarted;
```

**Subscribe** em `OnEnable`, **unsubscribe** em `OnDisable`:

```csharp
private void OnEnable()
{
    GameEvents.OnPlayerJoined += HandlePlayerJoined;
}

private void OnDisable()
{
    GameEvents.OnPlayerJoined -= HandlePlayerJoined;
}
```

Por que `OnEnable`/`OnDisable` e não `Start`/`OnDestroy`?

- `Start` roda 1×; objetos desabilitados não recebem.
- `OnEnable` roda sempre que o GameObject re-ativa.
- `OnDisable` é simétrico — o par garante que toggle não deixe listener pendurado.

> Use `System.Action<T>` em vez de `UnityEvent`. `UnityEvent` é mais lento e configurado no Inspector (não é onde queremos esses bindings).

## Threading: callbacks de rede

`SocketManager` recebe pacotes UDP em worker thread. **Não toque GameObject/Component a partir dele direto.** Use a fila já existente:

```csharp
// Dentro do SocketManager (já implementado):
private Queue<Action> _mainThreadQueue = new Queue<Action>();
private void OnPacketReceived(string raw) {
    _mainThreadQueue.Enqueue(() => {
        // tudo aqui roda na main thread (drained no Update do SocketManager)
        DispatchEvent(...)
    });
}
```

Seu controller subscreve via `SocketManager.Instance.On(eventName, callback)`. O callback **já é dispatched na main thread** porque o SocketManager drena a fila. Confirme isso ao escrever o controller (lendo `SocketManager.cs` se em dúvida).

## Arquitetura por namespace

Espelha a hierarquia de pastas:

```csharp
namespace HoraExtra.Network         // Assets/Scripts/Network/
namespace HoraExtra.Network.Models  // Assets/Scripts/Network/Models/
namespace HoraExtra.Characters      // Assets/Scripts/Characters/
namespace HoraExtra.AI              // Assets/Scripts/AI/
namespace HoraExtra.UI              // Assets/Scripts/UI/
```

Não obrigatório (Unity tolera classes sem namespace), mas reduz colisão de nomes e organiza Inspector.

## Performance — alocações em Update

```csharp
// ❌ Aloca string a cada frame
private void Update() {
    Debug.Log($"Pos: {transform.position}");
}

// ❌ Vector3.Distance (sqrt) — Vector3.sqrMagnitude é mais rápido
if (Vector3.Distance(a, b) < 1f) { }

// ✅
if ((a - b).sqrMagnitude < 1f) { }   // 1² == 1; ajuste constante

// ❌ GetComponent em Update
var rb = GetComponent<Rigidbody>();

// ✅ cache em Awake
private Rigidbody _rb;
private void Awake() { _rb = GetComponent<Rigidbody>(); }
```

## Checklist

- [ ] Arquivo em `Assets/Scripts/<Dominio>/<Classe>.cs`
- [ ] `namespace HoraExtra.<Dominio>` no topo
- [ ] Classe herda `MonoBehaviour`
- [ ] Campos serializados são `[SerializeField] private _camelCase`
- [ ] `[Header]` + `[Tooltip]` em campos no Inspector
- [ ] Constantes em `SCREAMING_SNAKE_CASE`
- [ ] Cache de `GetComponent` em `Awake` (ou usar `[SerializeField]` se for atribuído no Editor)
- [ ] Listeners de evento em `OnEnable` / unsubscribe em `OnDisable`
- [ ] `CompareTag(...)` em vez de `tag ==`
- [ ] Sem `GetComponent` / `Find` em `Update`
- [ ] Logs prefixados (`[GAMEPLAY]`, `[NETWORK]`, etc.)
- [ ] Sem teste automatizado (rule absoluta)

## Gotchas

1. **Ordem de `Awake` entre GameObjects** não é determinística. Se `EmoteController.Awake` depende de outro componente pronto, mova pra `Start`.
2. **Singleton acessado em `Awake`** de outro objeto pode dar NRE se o singleton inicializa depois. Use `Start` ou null-check.
3. **`OnDisable` em scene unload**: chamado, OK. Mas se o singleton já foi destruído antes (`Application.isPlaying = false`), `SocketManager.Instance` pode ser null. Use `SocketManager.Instance?.Off(...)`.
4. **`UnityEvent` em vez de `Action`**: lentíssimo e Inspector-driven. Não use exceto em assets prontos pra designer (raro).
5. **`Start` em objeto desabilitado**: NÃO roda até habilitar. Use `Awake` (sempre roda 1×) pra cache.
6. **`[SerializeField] private` em propriedade `{ get; set; }`** não funciona; usa campo. Wrap se precisar lógica.
7. **`Instantiate(prefab)` sem parent + sem destruir** = leak de scene. Sempre destruir explicitamente ou setar parent.
8. **`Newtonsoft.Json` está disponível** (em `Assets/Plugins/`); preferível ao `JsonUtility` para shapes complexos. Use `[JsonProperty]` pra chaves compactas.

## Referências no código

- `hora-extra-client/Assets/Scripts/Network/SocketManager.cs` — exemplo canônico de singleton MB
- `hora-extra-client/Assets/Scripts/Characters/` — outros controllers para comparação
- `.agents/rules/csharp-coding-standards.md` — rule humana
- `.agents/rules/client-design-pattern.md` — rule humana (Observer pattern, threading)
