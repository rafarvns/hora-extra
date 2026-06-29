---
name: client-new-network-event
description: Aplicar quando o cliente Unity precisa enviar OU reagir a evento UDP novo/modificado. Constante em `NetworkEvents.cs` (nunca string hardcoded), DTO em `Network/Models/` com `[JsonProperty]` pra chaves compactas, listener via `SocketManager.On(...)` em `OnEnable`. Depende de `cross-repo-communication-sync`.
applies_to: client
---

# client-new-network-event — Wire-up de evento UDP no cliente Unity

## Quando aplicar

- Cliente vai enviar um evento novo pro servidor (`player_emote`, `chat`, `interact`)
- Cliente vai reagir a um broadcast novo (`state_update`, `npc_spawn`)
- Você está mudando o nome de evento existente ou seu payload

## Quando NÃO aplicar

- Evento puramente local que não tocou rede (use o sistema de Action/eventos do `client-new-mono-behaviour` Observer)
- REST endpoint (HTTP, não UDP) — use `ApiClient` em `Network/Rest/`

## Pré-requisito

> **Leia `cross-repo-communication-sync` ANTES.** Esta skill é a parte cliente do contrato; o `COMMUNICATION.md` e o handler backend são os outros lados. Não implemente isolado.

## Ordem de criação

```
1. Atualizar NetworkEvents.cs              ← constante string canônica
2. Criar DTO em Network/Models/             ← shape do payload com [JsonProperty]
3. Subscribe / Send no controller de gameplay  ← consumo
```

(Skill `cross-repo-communication-sync` cobre passos paralelos no backend + `COMMUNICATION.md`.)

## Passo 1 — NetworkEvents.cs

`hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs`:

```csharp
namespace HoraExtra.Network
{
    /// <summary>
    /// Catálogo centralizado de event names. JAMAIS usar string hardcoded fora daqui.
    /// </summary>
    public static class NetworkEvents
    {
        // === C → S (Cliente → Servidor) ===
        public const string CONN          = "CONN";
        public const string JOIN_ROOM     = "join_room";
        public const string PLAYER_MOVE   = "player_move";
        public const string PLAYER_SPRINT = "player_sprint";
        public const string PING          = "ping";
        public const string PLAYER_EMOTE  = "player_emote";    // ← NOVO

        // === S → C (Servidor → Cliente) ===
        public const string CONN_SUCCESS    = "CONN_SUCCESS";
        public const string CONN_ERROR      = "CONN_ERROR";
        public const string ROOM_JOINED     = "room_joined";
        public const string STATE_UPDATE    = "state_update";
        public const string PLAYER_EMOTE_BC = "player_emote";  // mesmo string, name diferente pra intent
        public const string ERROR           = "ERROR";
        public const string PONG            = "pong";
    }
}
```

**Convenções:**

- Strings em `snake_case`. Eventos sistema/handshake em `UPPER_SNAKE` (`CONN`, `ERROR`, `CONN_SUCCESS`).
- Quando o **mesmo nome literal** é usado em ambas as direções (cliente envia `player_emote`; servidor faz broadcast `player_emote` pros outros), crie 2 constantes C# (`PLAYER_EMOTE`, `PLAYER_EMOTE_BC`) com o **mesmo valor** pra deixar a intent clara em `Send(...)` vs `On(...)`.
- Comentários `// === C → S ===` e `// === S → C ===` organizam a seção.

> ⚠️ **NUNCA hardcode strings de evento no gameplay.** `SocketManager.Instance.SendEvent("player_emote", ...)` = bug em escala. Use `NetworkEvents.PLAYER_EMOTE`. Reviewer flagga.

## Passo 2 — DTO em Network/Models/

`hora-extra-client/Assets/Scripts/Network/Models/PlayerEmote.cs`:

```csharp
using Newtonsoft.Json;

namespace HoraExtra.Network.Models
{
    /// <summary>
    /// Payload enviado pelo cliente: { id: string, d?: number }
    /// </summary>
    public class PlayerEmoteRequest
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("d")]  public int? Duration;   // null = servidor usa default 2000ms
    }

    /// <summary>
    /// Broadcast do servidor: { playerId: string, id: string, d: number }
    /// </summary>
    public class PlayerEmoteBroadcast
    {
        [JsonProperty("playerId")] public string PlayerId;
        [JsonProperty("id")]       public string Id;
        [JsonProperty("d")]        public int Duration;
    }
}
```

**Regras:**

- 1 arquivo por evento (request + broadcast podem coexistir no mesmo `.cs`).
- `namespace HoraExtra.Network.Models`.
- **`[JsonProperty("chave_do_json")]` em CADA campo** que tem nome diferente em C#/JSON, e em todos os campos com chave compacta (`p`, `r`, `s`, `d`).
- Campos `public` no DTO — não usar property `{ get; set; }` exceto quando precisar de lógica.
- Tipo nullable (`int?`, `float?`, `string`) pra campos opcionais.
- `string`, `int`, `float`, `bool`, `string[]`, `float[]` cobrem 90% dos casos.

> Newtonsoft.Json está em `Assets/Plugins/` — disponível por padrão. **Não** use `JsonUtility` (do Unity) para esses DTOs porque não suporta nullable, dictionary, e mapping de chave compacta direito.

### Payloads com array (high-frequency move)

```csharp
public class PlayerMoveRequest
{
    [JsonProperty("p")] public float[] Position;   // [x, y, z]
    [JsonProperty("r")] public float   Rotation;   // yaw em graus
}
```

## Passo 3 — Consumir no controller

### Enviar (cliente → servidor)

```csharp
using HoraExtra.Network;
using HoraExtra.Network.Models;

private void OnEmoteInput(string emoteId)
{
    var payload = new PlayerEmoteRequest { Id = emoteId, Duration = 2000 };
    SocketManager.Instance.SendEvent(NetworkEvents.PLAYER_EMOTE, payload);
    Debug.Log($"[GAMEPLAY] Emote '{emoteId}' enviado");
}
```

> O `SendEvent` interno do `SocketManager` serializa via Newtonsoft, monta o pacote `{e, d}` e envia via UDP. Não inventar wrap próprio.

### Receber (servidor → cliente) — subscribe em OnEnable

```csharp
private void OnEnable()
{
    SocketManager.Instance.On(NetworkEvents.PLAYER_EMOTE_BC, OnEmoteBroadcast);
}

private void OnDisable()
{
    SocketManager.Instance?.Off(NetworkEvents.PLAYER_EMOTE_BC, OnEmoteBroadcast);
}

private void OnEmoteBroadcast(string rawJson)
{
    try
    {
        var payload = JsonConvert.DeserializeObject<PlayerEmoteBroadcast>(rawJson);
        Debug.Log($"[NETWORK] Emote '{payload.Id}' de {payload.PlayerId}");

        // Acionar animação do player remoto, UI, etc.
        var remotePlayer = PlayerRegistry.FindById(payload.PlayerId);
        remotePlayer?.PlayEmote(payload.Id, payload.Duration);
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[NETWORK] Falha ao parsear player_emote: {ex.Message} | raw: {rawJson}");
    }
}
```

> A assinatura real do `On(event, callback)` pode variar (string raw vs already-deserialized). Confira `SocketManager.cs` no projeto. O padrão `string raw → JsonConvert.DeserializeObject<T>` cobre o caso conservador.

### Threading

`SocketManager` recebe em worker thread e (na implementação atual do projeto) drena uma `_mainThreadQueue` no `Update`. **O callback `OnEmoteBroadcast` deve estar rodando na main thread** — toque `transform`, `Animator`, UI sem medo.

Se algum dia o pattern mudar e callbacks vierem em worker, marshal manualmente:

```csharp
private void OnEmoteBroadcast(string rawJson)
{
    SocketManager.Instance.EnqueueMainThread(() => {
        // toque GameObjects aqui
    });
}
```

## Checklist

- [ ] Constante adicionada em `NetworkEvents.cs` na seção correta (C→S ou S→C)
- [ ] DTO em `Network/Models/<Evento>.cs` com `[JsonProperty]` em cada campo (especialmente chaves compactas)
- [ ] Send via `SocketManager.Instance.SendEvent(NetworkEvents.X, payload)` — nunca string literal
- [ ] Receive via `SocketManager.Instance.On(NetworkEvents.X, callback)` em `OnEnable`
- [ ] `OnDisable` faz `Off(NetworkEvents.X, callback)` (com null-check no Instance)
- [ ] Try/catch ao parsear JSON; log com `[NETWORK]` prefix
- [ ] `COMMUNICATION.md` atualizado no backend (skill `cross-repo-communication-sync`)
- [ ] Smoke test cross-log: backend loga recebimento + cliente loga broadcast voltando

## Anti-patterns

- ❌ `SocketManager.Instance.SendEvent("player_emote", ...)` — string hardcoded
- ❌ DTO sem `[JsonProperty]` em chave compacta → campo fica zero/null em runtime
- ❌ Subscribe em `Start` (não simétrico com unsubscribe)
- ❌ Subscribe sem unsubscribe → leak; `OnDisable` é obrigatório
- ❌ Tocar `transform` em callback sem garantir main thread
- ❌ `JsonUtility` para payload com chave compacta — não mapeia 1-char direito
- ❌ Criar evento novo só do lado cliente, sem handler backend → silencioso fail
- ❌ Renomear evento sem atualizar `COMMUNICATION.md`

## Gotchas

1. **`SocketManager.Instance` pode ser null** quando uma cena recarrega: use `?.` consistentemente no `OnDisable`.
2. **Chave compacta esquecida no `[JsonProperty]`** = parse silencioso falha; campo fica zero/null. Sem erro visível. Diagnostique com `Debug.Log` do raw JSON.
3. **`int` vs `int?`** importa: `int` default 0 quando chave ausente; `int?` é null. Use nullable pra opcionais.
4. **Newtonsoft `JsonConvert.DeserializeObject<T>(raw)`** pode throw em JSON malformado — sempre try/catch.
5. **Mesmo nome literal `player_emote` C→S e S→C** = intencional (cliente envia, servidor broadcast com mesmo nome pros outros). Duas constantes C# (`PLAYER_EMOTE`, `PLAYER_EMOTE_BC`) com mesmo valor cosmético.
6. **DTO de high-frequency** (`player_move` 20Hz): considere pool de objetos pra reduzir GC. Não otimizar prematuramente — só se profile mostrar.
7. **`UseTestToken=true` (default em dev)** = SocketManager usa `DEV_TEST_TOKEN`. Auto-join "dev-room". Lembre disso quando testar manualmente.

## Referências no código

- `hora-extra-client/Assets/Scripts/Network/NetworkEvents.cs` — catálogo de constantes
- `hora-extra-client/Assets/Scripts/Network/Models/` — DTOs existentes (referência)
- `hora-extra-client/Assets/Scripts/Network/SocketManager.cs` — `SendEvent`, `On`, `Off`, `_mainThreadQueue`
- `hora-extra-backend/docs/Networking/COMMUNICATION.md` — contrato MD
- `.agents/rules/client-design-pattern.md` §2 (NetworkEvents) e §3 (threading)
- `.agents/rules/communication-sync-rule.md`
- Skill `cross-repo-communication-sync` — workflow cross-language completo
