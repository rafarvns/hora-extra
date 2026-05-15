# Guia Passo-a-Passo — Setup 2-Player Guest Mode

Este guia te leva do zero até **dois jogadores se vendo na mesma cena via Guest Mode**.
Tempo estimado: 20–30 minutos (se você nunca abriu o projeto antes).

> Pré-requisito: você já fez `git pull` da branch que contém a feature guest mode (backend + scripts client implementados, mas ainda **sem o botão na UI**). É exatamente isso que este guia faz: liga o botão.

---

## Sumário do que você vai fazer

1. Subir o backend (Node.js).
2. Abrir o projeto Unity e configurar o GameObject `SocketManager` no Inspector.
3. Criar o botão `BTN_GuestPlay` na `MainMenuScene`.
4. Adicionar 3 campos + 1 método no `MainMenuController.cs`.
5. Adicionar a cena de gameplay no Build Settings.
6. Build standalone do client.
7. Rodar o `.exe` + Editor Play Mode em paralelo → ver os 2 guests interagindo.

---

## Passo 1 — Subir o backend

Abra um terminal **e deixe ele aberto durante todos os testes**.

```bash
cd hora-extra-backend
npm install         # se ainda não rodou
npm run dev
```

Aguarde os dois logs aparecerem no console:

```
[UDP_SOCKET] Servidor UDP ouvindo em 0.0.0.0:5001
[SERVER]     Hora Extra Backend rodando em http://0.0.0.0:5000
```

✅ Se ambos apareceram, prossiga. Se algum falhou, verifique:
- `.env` existe? (`USE_SQLITE=true` é o mais simples pro TCC).
- Porta 5000 ou 5001 já está em uso? Mate o processo (`netstat -ano | findstr :5001` no Windows).

---

## Passo 2 — Configurar o SocketManager no Inspector

Abra o Unity Hub e abra o projeto `hora-extra-client`.

1. Abra a cena `Assets/Scenes/Menu_Screens/MainMenuScene.unity` no Editor.
2. No painel **Hierarchy**, procure por um GameObject chamado `SocketManager` (ou similar — provavelmente fica no GameObject `_Managers`, `Bootstrap`, ou solto na raiz).
3. Se NÃO encontrou na MainMenuScene, ele pode estar numa cena de bootstrap separada. Verifique se há uma cena `BootstrapScene` ou `_PersistentScene`. Se não houver, abra `Assets/Scenes/SampleScene.unity` e procure lá. Onde quer que ele esteja, o passo 2.4 abaixo é igual.
4. Com o `SocketManager` selecionado, no **Inspector** vá até o componente `Socket Manager (Script)`:
   - **`Auto Connect`** → DESMARCAR (deixar `false`).
   - **`Use Test Token`** → DESMARCAR (deixar `false`).
5. Salve a cena (`Ctrl+S`).

> **Por que isso?** O `SocketManager` autoconecta com `TestToken` no Awake por padrão. Se não desligarmos, ele entra na `dev-room` antes do botão guest ser clicado — guest mode fica quebrado.

✅ Confirme com `Ctrl+S` e prossiga.

### Caso o GameObject SocketManager não exista em lugar nenhum

Crie um. Em qualquer cena que rode primeiro (MainMenuScene):

1. Hierarchy → botão direito → `Create Empty` → renomeie pra `SocketManager`.
2. No Inspector → `Add Component` → digite `Socket Manager` → adicione.
3. Confirme que os campos `Server Ip = 127.0.0.1`, `Server Port = 5001`, `Auto Connect = false`, `Use Test Token = false`.
4. Salve a cena.

---

## Passo 3 — Criar o botão `BTN_GuestPlay` na MainMenuScene

Ainda com `MainMenuScene` aberta:

1. No Hierarchy, encontre o Canvas onde estão os botões existentes (`btnGoToLogin`, `btnGoToRegister`, `btnGoToLobby`).
2. Selecione um deles (ex.: `btnGoToLogin`), `Ctrl+D` pra duplicar. Renomeie a cópia pra `BTN_GuestPlay`.
3. Posicione visualmente onde quiser na tela (ex.: acima do botão de Login).
4. Expanda `BTN_GuestPlay` → selecione o filho `Text (TMP)` (ou `Text`).
5. No Inspector, troque o texto para: `Jogar como Convidado`.
6. **NÃO** ligue o `OnClick()` no Inspector ainda — a gente vai fazer via código no próximo passo (mais robusto, sobrevive a refatoração).
7. Salve a cena (`Ctrl+S`).

✅ Você deve ver o botão novo aparecendo no Game view com o texto correto.

---

## Passo 4 — Editar `MainMenuController.cs`

Abra `Assets/Scripts/UI/MainMenuController.cs` no seu editor (VS Code, Rider, etc.).

Você vai fazer **3 mudanças**:

### 4.1 — Adicionar `using` no topo

Logo após as linhas `using` existentes (linhas 1-3), adicione:

```csharp
using HoraExtra.Network;
using HoraExtra.Network.Rest.Services;
```

### 4.2 — Adicionar campo `[SerializeField]` na seção `Buttons`

Dentro da classe `MainMenuController`, ache o `[Header("Buttons")]` (linha 13) e adicione um novo botão logo após os 3 existentes:

```csharp
[Header("Buttons")]
[SerializeField] private Button btnGoToRegister;
[SerializeField] private Button btnGoToLogin;
[SerializeField] private Button btnGoToLobby;
[SerializeField] private Button btnGuestPlay;   // ← ADICIONE ESTA LINHA
```

### 4.3 — Registrar listener no `Awake`

Ainda dentro de `Awake()` (linha 26), adicione UMA linha **dentro** do método, junto com os outros listeners:

```csharp
private void Awake()
{
    if (btnGoToRegister != null)
        btnGoToRegister.onClick.AddListener(() => LoadScene(registerSceneName));

    if (btnGoToLogin != null)
        btnGoToLogin.onClick.AddListener(() => LoadScene(loginSceneName));

    if (btnGoToLobby != null)
        btnGoToLobby.onClick.AddListener(() => LoadScene(lobbySceneName));

    // ↓↓↓ ADICIONE ESTAS LINHAS ↓↓↓
    if (btnGuestPlay != null)
        btnGuestPlay.onClick.AddListener(OnGuestPlayClicked);
}
```

### 4.4 — Adicionar o método `OnGuestPlayClicked`

Antes do `QuitGame()` (linha 117), adicione o seguinte método inteiro:

```csharp
/**
 * Handler do botão "Jogar como Convidado".
 * Solicita JWT guest ao backend, configura sessão UDP e carrega a cena de gameplay.
 */
private async void OnGuestPlayClicked()
{
    Debug.Log("[UI] Botão Guest Play clicado — solicitando JWT...");

    var resp = await GuestService.JoinAsGuest();
    if (resp == null || !resp.Success || resp.Data == null)
    {
        Debug.LogError($"[UI] Guest join falhou: {resp?.Error?.Message ?? "resposta nula"}");
        return;
    }

    Debug.Log($"[UI] Guest concedido — id={resp.Data.GuestId}, room={resp.Data.RoomId}");

    // Configura sessão antes do SocketManager conectar
    NetworkSettings.AuthToken = resp.Data.Token;
    GuestSession.IsGuestMode = true;
    GuestSession.GuestRoomId = resp.Data.RoomId;

    // Força reconexão UDP com o token guest
    SocketManager.Instance.SetAuthTokenAndReconnect(resp.Data.Token);

    // Carrega a cena de gameplay
    LoadScene(lobbySceneName);
}
```

> O campo `lobbySceneName` já existe (linha 21) e aponta para `"SampleScene"` por padrão. Se sua cena de gameplay tem outro nome, ajuste no Inspector.

### 4.5 — Salvar e voltar ao Unity

Salve o arquivo. Volte ao Unity Editor e espere a barra `Reloading domain...` terminar.

✅ Console NÃO deve mostrar erros vermelhos de compilação.

Se aparecer:
- `The type or namespace name 'GuestSession' could not be found` → confira o `using HoraExtra.Network;` no topo.
- `The type or namespace name 'GuestService' could not be found` → confira o `using HoraExtra.Network.Rest.Services;` no topo.

---

## Passo 5 — Ligar o botão `BTN_GuestPlay` ao campo `btnGuestPlay` no Inspector

Volte para `MainMenuScene` no Editor:

1. No Hierarchy, selecione o GameObject que tem o `MainMenuController` (geralmente é o `Canvas` ou um GameObject filho chamado `MenuController`).
2. No Inspector, abra o componente `Main Menu Controller`.
3. Você verá um campo novo: `Btn Guest Play`. Ele está vazio.
4. Arraste o `BTN_GuestPlay` do Hierarchy para esse campo.
5. Salve a cena (`Ctrl+S`).

✅ Confira que o campo agora mostra `BTN_GuestPlay (Button)`.

---

## Passo 6 — Adicionar cenas ao Build Settings

`File → Build Settings...` (Ctrl+Shift+B):

1. Confirme que estas cenas estão na lista **e marcadas**:
   - `Assets/Scenes/Menu_Screens/MainMenuScene` (índice 0 — primeira)
   - `Assets/Scenes/SampleScene`
2. Se faltar alguma: abra a cena no Editor e clique `Add Open Scenes`.
3. Arraste `MainMenuScene` pra ser a primeira da lista (índice 0).
4. `Platform` = `Windows, Mac, Linux` (ou sua plataforma de teste). Clique `Switch Platform` se necessário.

---

## Passo 7 — Build standalone

Ainda em Build Settings:

1. Clique `Build And Run`.
2. Escolha uma pasta **vazia** (ex.: `Desktop/hora-extra-build/`).
3. Aguarde o build (~1-3 minutos).
4. O `.exe` abre sozinho. **NÃO clique em "Jogar como Convidado" ainda** — espere abrir o Editor também.

---

## Passo 8 — Abrir o Editor Play Mode em paralelo

1. Volte ao Unity Editor.
2. Abra `MainMenuScene` (`Ctrl + click duplo` na cena no Project view).
3. Aperte `Play` (botão ▶ no topo).

Agora você tem **duas instâncias do jogo rodando**:
- **Cliente A** = Editor (Play Mode).
- **Cliente B** = `.exe` standalone.

---

## Passo 9 — Teste 2-player

### 9.1 — Cliente A entra como guest

No Editor:
1. Clique **"Jogar como Convidado"**.
2. No **Console do Editor**, você deve ver em sequência:
   ```
   [UI]      Botão Guest Play clicado — solicitando JWT...
   [UI]      Guest concedido — id=guest-xxxxxxxx, room=guest-room
   [NETWORK] SetAuthTokenAndReconnect: reconectando...
   [NETWORK] Iniciando Socket UDP para 127.0.0.1:5001
   [NETWORK] Handshake de conexão enviado.
   [NETWORK] Conexão UDP Estabelecida! ID: guest-xxxxxxxx
   ```
3. No **terminal do backend**, você deve ver:
   ```
   [HTTP]        POST /api/auth/guest 201 - ~10ms
   [UDP_SOCKET]  Sessão UDP iniciada: guest-xxxxxxxx em 127.0.0.1:xxxxx (Auto-Join: guest-room)
   ```
4. A cena `SampleScene` deve carregar e o player local deve aparecer.

✅ Cliente A está dentro como **host** (primeiro a entrar).

### 9.2 — Cliente B entra como guest

No `.exe`:
1. Clique **"Jogar como Convidado"**.
2. No **terminal do backend**, você verá outra sessão UDP:
   ```
   [HTTP]        POST /api/auth/guest 201 - ~10ms
   [UDP_SOCKET]  Sessão UDP iniciada: guest-yyyyyyyy em 127.0.0.1:zzzzz (Auto-Join: guest-room)
   ```
3. No **Console do Editor (cliente A)**, deve aparecer:
   ```
   [NETWORK] player_joined (guest-yyyyyyyy)
   ```

✅ Os dois guests estão na mesma sala.

### 9.3 — Mover e verificar sincronização

1. No Editor, mova o player local (WASD ou setas).
2. No `.exe`, você deve **ver o avatar do cliente A se movendo suavemente** (interpolação via `NetworkPlayer.cs`).
3. Faça o mesmo no `.exe` — o Editor deve ver o cliente B mover.

✅ **2-player funcional. Entregue ao professor.**

---

## Passo 10 — Teste do "lazy reset" (opcional, mas valida o algoritmo)

Demonstra que o servidor reseta a sala quando ela esvazia:

1. Com os 2 clientes conectados, **feche o Editor** (Stop Play Mode).
2. **Não toque** no `.exe` (deixe ele conectado).
3. Aguarde ~35 segundos (timeout de session 30s + cleanup tick 10s).
4. No terminal do backend, você verá:
   ```
   [UDP_SOCKET] Sessão expirada: guest-xxxxxxxx (...)
   [NPC_SYNC]   Estado de NPCs limpo para a sala: guest-room (room vazia)
   ```
5. Volte ao Editor, dê Play, clique "Jogar como Convidado" de novo.
6. Esse novo guest agora é o **novo master** dos NPCs (reaproveita a mesma lógica `npcMasters`).

✅ Lazy reset funcionando.

---

## Troubleshooting

| Sintoma | Causa provável | Como resolver |
| :--- | :--- | :--- |
| `Botão guest não responde` | `btnGuestPlay` não ligado no Inspector | Passo 5 — arraste `BTN_GuestPlay` ao campo do `MainMenuController`. |
| `[NETWORK] Tentativa de enviar 'CONN' antes do Socket estar pronto` | `Auto Connect = true` e tentou guest antes do Awake | Passo 2 — desmarcar `Auto Connect` no Inspector. |
| `[UI] Guest join falhou: Cannot connect to destination host` | Backend não está rodando | Passo 1 — `npm run dev` em outro terminal. |
| `CONN_ERROR: Invalid token` | `Use Test Token = true` está sobrescrevendo | Passo 2 — desmarcar `Use Test Token`. |
| `[NETWORK] Conexão UDP Estabelecida! ID: dev-test-player` em vez de `guest-xxx` | Dev bypass ativo | Confirme que `NODE_ENV=development` e `Use Test Token = false` no Inspector. |
| Cena de gameplay não tem players visíveis | Falta um `WorldController` que escute `player_joined` e instancie prefab | Fora do escopo desta feature — verifique com o dev do lobby. |
| Erro de compilação `GuestSession not found` | `using HoraExtra.Network` faltando | Passo 4.1. |
| Console mostra `[NETWORK] Guest mode detectado — aguardando SetAuthTokenAndReconnect()` mas trava | Você setou `IsGuestMode = true` mas não chamou `SetAuthTokenAndReconnect` | Confirme que o método `OnGuestPlayClicked` chama `SocketManager.Instance.SetAuthTokenAndReconnect(...)` (Passo 4.4). |

---

## Resumo visual do fluxo

```
   ┌─────────────────────┐
   │  MainMenuScene      │
   │  [Jogar Convidado]  │
   └──────────┬──────────┘
              │ click
              ▼
   ┌─────────────────────────────────┐
   │ MainMenuController              │
   │  OnGuestPlayClicked()           │
   │   1. GuestService.JoinAsGuest() │ ───→ POST /api/auth/guest (backend)
   │   2. NetworkSettings.AuthToken  │     ←── 201 { token, guestId, roomId }
   │   3. GuestSession.IsGuestMode   │
   │   4. SocketManager.SetAuthToken │ ───→ UDP CONN com JWT guest
   │      AndReconnect()             │     ←── CONN_SUCCESS + room_joined "guest-room"
   │   5. LoadScene("SampleScene")   │
   └─────────────────────────────────┘
              │
              ▼
   ┌─────────────────────────────────┐
   │  SampleScene                    │
   │  - WorldController              │
   │  - NetworkPlayer (remotos)      │  ← interpolação via player_move
   │  - NetworkEntity (NPCs)         │  ← primeiro guest = master
   └─────────────────────────────────┘
```

---

## Referência

- Doc backend: `hora-extra-backend/docs/Networking/COMMUNICATION.md` (protocolo UDP completo).
- Doc mecânica: `hora-extra-backend/docs/Mechanics/GuestMode.md` (lógica server-side, lazy reset).
- Doc client overview: `hora-extra-client/Docs/Networking/GuestMode.md` (este doc é o setup operacional; aquele é a referência técnica).

---

**Última atualização:** 2026-05-14. Implementado para entrega TCC de domingo.
