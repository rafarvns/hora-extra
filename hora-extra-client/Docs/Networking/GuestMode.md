# Guest Mode — Cliente Unity

## Descrição

O Guest Mode permite que dois (ou mais) jogadores entrem na mesma cena de gameplay
sem precisar de cadastro ou login. Ao clicar em "Jogar como Convidado", o cliente
solicita um JWT temporário ao backend, configura a sessão UDP e carrega a cena de
gameplay. Ambos os guests entram automaticamente na sala compartilhada `"guest-room"`.

Motivação: viabilizar a demonstração de 2 jogadores interagindo no TCC sem depender
do sistema de lobby/login ainda em desenvolvimento.

Referência de protocolo completo: `hora-extra-backend/docs/Networking/COMMUNICATION.md`
e `hora-extra-backend/docs/Mechanics/GuestMode.md` (lógica server-side, lazy reset, mastership).

---

## Implementação

### Arquivos envolvidos

| Arquivo | Responsabilidade |
| :--- | :--- |
| `Assets/Scripts/Network/Models/GuestModels.cs` | DTO `GuestData` — shape do payload REST retornado pelo backend |
| `Assets/Scripts/Network/Rest/Services/GuestService.cs` | Chama `POST /api/auth/guest` via `ApiClient` |
| `Assets/Scripts/Network/GuestSession.cs` | Estado estático: `IsGuestMode`, `GuestRoomId` |
| `Assets/Scripts/Network/SocketManager.cs` | Guarda no `Awake` + método `SetAuthTokenAndReconnect` |
| `Assets/Scripts/Network/NetworkEvents.cs` | Sem novos eventos — comentário documenta o fluxo |

### Fluxo de dados

```
[UI Button "Jogar como Convidado"]
        |
        v
GuestService.JoinAsGuest()
  POST /api/auth/guest
        |
        v (201 Created)
ApiResponse<GuestData>
  .Data.Token   → JWT guest (exp 1h)
  .Data.GuestId → "guest-<uuid-curto>"
  .Data.RoomId  → "guest-room"
        |
        v
GuestSession.IsGuestMode = true
GuestSession.GuestRoomId = "guest-room"
NetworkSettings.AuthToken = token
        |
        v
SocketManager.EnsureExists().SetAuthTokenAndReconnect(token)
  → Cria SocketManager em runtime se Instance == null (tolera cenas sem o GO)
  → Fecha UDP anterior (se houver)
  → UseTestToken = false
  → ConnectToServer()
     → SendHandshake() com JWT guest
        |
        v (servidor)
CONN_SUCCESS { id: "guest-<uuid>" }
room_joined  { roomId: "guest-room", players: [...] }
        |
        v
SceneManager.LoadScene("SampleScene")  ← UI faz isso após chamar SetAuthTokenAndReconnect
```

### Guarda no Awake

O `SocketManager.Awake` agora verifica `GuestSession.IsGuestMode` antes de
autoconectar. Isso impede que o SocketManager envie `CONN` com `TestToken` enquanto
o guest ainda não obteve o JWT real:

```csharp
if (GuestSession.IsGuestMode)
{
    Debug.Log("[NETWORK] Guest mode detectado — aguardando SetAuthTokenAndReconnect().");
    return;
}
```

**Importante:** `GuestSession.IsGuestMode` deve ser `true` antes do `SocketManager`
ser instanciado (ou antes de carregar a cena que o contém). Se o `SocketManager`
já existir via `DontDestroyOnLoad`, a guarda não se aplica — mas nesse caso
`SetAuthTokenAndReconnect` reconecta corretamente mesmo sem a guarda.

### NPC Mastership (servidor)

O primeiro guest a conectar na sala vazia registra os NPCs e torna-se master deles
automaticamente (lógica em `NpcRegisterHandler` no backend). O segundo guest recebe
os broadcasts de movimento. Isso não requer nenhum código adicional no cliente —
o mecanismo de `npc_register` / `npc_registered` existente já lida com isso.

Se o primeiro guest desconectar e a sala ficar vazia, o servidor aplica um lazy reset
no próximo `CONN` guest: limpa masters de NPCs e o novo entrante reassume.

---

## Uso

### Como ligar o botão "Jogar como Convidado" na UI

No `MainMenuController.cs` (ou equivalente), adicione:

```csharp
[SerializeField] private UnityEngine.UI.Button _guestPlayButton;

private void OnEnable()
{
    _guestPlayButton.onClick.AddListener(OnGuestPlayClicked);
}

private void OnDisable()
{
    _guestPlayButton.onClick.RemoveListener(OnGuestPlayClicked);
}

private async void OnGuestPlayClicked()
{
    var resp = await HoraExtra.Network.Rest.Services.GuestService.JoinAsGuest();
    if (resp == null || !resp.Success)
    {
        Debug.LogError($"[UI] Guest join falhou: {resp?.Error?.Message}");
        return;
    }

    HoraExtra.Network.NetworkSettings.AuthToken = resp.Data.Token;
    HoraExtra.Network.GuestSession.IsGuestMode = true;
    HoraExtra.Network.GuestSession.GuestRoomId = resp.Data.RoomId;

    // EnsureExists() cria o SocketManager em runtime se nao existir na cena —
    // evita NullReferenceException em cenas sem o GameObject pre-configurado.
    SocketManager.EnsureExists().SetAuthTokenAndReconnect(resp.Data.Token);

    // Inicializa o spawner de jogadores remotos APOS o SocketManager existir.
    // Ele assina PLAYER_JOINED/PLAYER_MOVE/PLAYER_DISCONNECTED e instancia
    // Capsules coloridas (placeholder) ou o prefab atribuido no Inspector.
    HoraExtra.Network.RemotePlayerSpawner.EnsureExists();

    UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
}
```

No Inspector, ligar o campo `_guestPlayButton` ao botão `BTN_GuestPlay` criado na cena.

### Configuração do SocketManager no Inspector

Para o fluxo guest funcionar corretamente ao iniciar o jogo:

| Campo | Valor recomendado |
| :--- | :--- |
| `Auto Connect` | `false` (para não conectar com TestToken na abertura) |
| `Use Test Token` | `false` |

Esses valores podem ser sobrescritos em runtime pelo `SetAuthTokenAndReconnect`.
Para testes de desenvolvimento sem guest mode, reative `Use Test Token = true`
e `Auto Connect = true` no Inspector.

### Logs esperados no Console Unity

```
[NETWORK] Solicitando acesso como convidado...
[NETWORK] Acesso guest concedido. ID: guest-a1b2c3, Sala: guest-room
[NETWORK] SetAuthTokenAndReconnect: encerrando conexão anterior e reconectando...
[NETWORK] Iniciando Socket UDP para 127.0.0.1:5001
[NETWORK] Handshake de conexão enviado.
[NETWORK] Conexão UDP Estabelecida! ID: guest-a1b2c3
```

E no terminal do backend (Winston):

```
[UDP_SOCKET] Guest guest-a1b2c3 entrou na sala guest-room (host=true)
[UDP_SOCKET] Auto-Join: guest-room
```

### Teste com 2 instâncias

1. `cd hora-extra-backend && npm run dev`
2. Build standalone (File → Build Settings → Build And Run).
3. Play Mode no Editor Unity.
4. Nas duas instâncias: clicar "Jogar como Convidado".
5. Ambas devem logar `[NETWORK] Conexão UDP Estabelecida! ID: guest-...`.
6. Mover um avatar — o outro deve ver o movimento via interpolação.

Ver checklist completo em `hora-extra-backend/docs/Mechanics/GuestMode.md` §"Verificação manual".
