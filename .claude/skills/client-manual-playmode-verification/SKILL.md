---
name: client-manual-playmode-verification
description: Aplicar SEMPRE em feature do cliente Unity. Substitui completamente a noção de teste automatizado (rule absoluta `no-unit-test-on-unity.md`). Define checklist Play Mode pra humano + convenção de logs `[NETWORK]/[GAMEPLAY]/[UI]/[AI]`. Consultada pelo agente `manual-verifier`.
applies_to: client
---

# client-manual-playmode-verification — Validação manual no Unity Play Mode

> 🚫 **Não criar `Tests/`, `*.Tests.cs` ou qualquer suite Unity Test Framework no `hora-extra-client/`.** A rule `.agents/rules/no-unit-test-on-unity.md` é absoluta. Reviewer flagga como **grave**.

## Por que essa skill existe

Hora-Extra é TCC com iteração rápida. Mocks de GameObject/Component custam mais do que produzem. **Toda validação client-side é manual via Play Mode** + logs estruturados via `Debug.Log`.

Quando o agente `manual-verifier` é spawned, ele lê esta skill + a seção "Manual verification steps" do plano e enumera um checklist pro usuário rodar.

## Quando aplicar

- Toda feature de cliente Unity precisa de plano de validação manual
- Antes de fechar uma feature — passar pelo checklist no Editor
- Quando algo "parece" funcionar mas não tem certeza — dispare log de debug

## Quando NÃO aplicar

- Backend: lá tem Vitest (ver `backend-vitest-spec`)
- Lógica pura C# fora de Unity (ex: classe utilitária sem GameObject) — raro no projeto

## Estrutura de um plano de validação manual

A seção do plano "Manual verification steps" deve listar passos numerados, cada um com:

```markdown
## Manual verification steps

### 1. Setup
- Cena: `Assets/Scenes/SCN_Main.unity` (abra antes de entrar em Play Mode)
- Pré-condição: backend rodando em `127.0.0.1:5000` (HTTP) e `:5001` (UDP)
- Player Prefab `PFB_Player` na scene

### 2. Conexão UDP
- Ação: entre em Play Mode
- Espera-se no Console: `[NETWORK] Conectado a 127.0.0.1:5001 com token DEV`
- Pode estar com `UseTestToken=true` no SocketManager (default em dev)

### 3. Disparar emote
- Ação: pressione `E` durante o Play Mode
- Espera-se:
  - Console Unity: `[GAMEPLAY] Emote 'wave' enviado`
  - Console Unity (segundos depois): `[NETWORK] Recebido player_emote de <id>` (se houver outro player)
  - Visual: animação de wave executando no Player

### 4. Edge case — emote inválido
- Ação: modifique temporariamente o EmoteController pra mandar `id="cheat-emote"` e teste
- Espera-se: console Unity NÃO recebe `player_emote` de volta (servidor dropa)
- Reverter código depois

### 5. Cleanup
- Saia do Play Mode
- Confirme que nenhum erro vermelho ficou no Console
```

## Convenção de logs

**Prefixos obrigatórios** entre colchetes pro humano filtrar Console por área:

| Prefixo       | Quando                                           |
| :------------ | :----------------------------------------------- |
| `[NETWORK]`   | SocketManager, ApiClient, eventos UDP/REST recv/send |
| `[GAMEPLAY]`  | Player controller, NPC, mecânicas locais         |
| `[UI]`        | Canvas, modals, input UI                         |
| `[AI]`        | Behavior tree, decision making                   |
| `[INPUT]`     | Input system (opcional, se virar verboso)        |
| `[STATE]`     | State machines, save/load                        |

### Níveis

```csharp
Debug.Log($"[NETWORK] Conectado a {endpoint}");        // info
Debug.LogWarning($"[GAMEPLAY] Player com HP <= 0");    // warn
Debug.LogError($"[NETWORK] Falha de parse: {raw}");    // error (vermelho no Console)
```

`Debug.LogError` para o usuário **VER** o problema (vermelho). `Debug.LogWarning` pra anomalia que não bloqueia. `Debug.Log` pra rastreio normal.

### O que logar

- **Eventos de rede**: envio (`SendEvent("player_emote", ...)`) e recebimento (`On("player_emote_bc", ...)`) com IDs/payloads curtos
- **Transições de estado**: "entrou em sala X", "spawnou NPC Y", "morreu"
- **Erros**: parse, NRE em referência cacheada, timeout

### O que NÃO logar

- Posições em todo `Update` (vai estourar Console)
- Senhas, tokens completos
- `Debug.Log(this)` em loop (cria garbage)

## Setup recomendado do Console Unity

Para o usuário fazer Play Mode produtiva:

1. **Clear on Play**: ATIVADO (Console limpa ao entrar em Play) — vê só os logs da rodada atual
2. **Error Pause**: ATIVADO (pausa em `LogError`) — facilita debug
3. **Collapse**: DESATIVADO (vê cada chamada separada)
4. **Filter** (search bar): use `[NETWORK]` ou `[GAMEPLAY]` pra focar

## Smoke test cross-language (backend + cliente)

Quando feature é cross-repo (`target=both`), o teste manual envolve **observar logs dos dois lados ao mesmo tempo**:

| Onde                                              | O que esperar                                     |
| :------------------------------------------------ | :------------------------------------------------ |
| Backend terminal (`npm run dev`)                  | `[UDP_SOCKET]` logs do Winston com IDs            |
| Unity Console                                     | `[NETWORK]` logs com mesmas IDs/strings de evento |
| `COMMUNICATION.md` aberto pro side-by-side check  | Tabela bate com payloads observados               |

Se backend loga mas cliente não recebe ou vice-versa, é alinhamento (ver `cross-repo-communication-sync`).

## Quando reportar fail

O manual-verifier vai perguntar:

- ✅ "Tudo verde — todos os passos passaram"
- ❌ "Falhou no passo N" + descrição curta

Reporte fail se:

- Um log esperado não aparece
- Aparece erro vermelho não esperado no Console
- Comportamento visual diverge do plano
- Crash do Editor / freeze >5s

Não reporte fail por:

- Frame drops esporádicos (perf é outra investigação)
- Warning não-bloqueante (sublinhado amarelo, não vermelho)
- Comportamento sutil não documentado no plano (esse vai pra "improvements" ou nova feature)

## Editor crashou no Play Mode? Diagnóstico rápido

1. **Stack trace**: `Logs/` na pasta do projeto + console output anterior ao crash
2. **NRE em `Update`/`FixedUpdate`** é a #1 causa — provavelmente referência cacheada em `Awake` ficou null porque ordem de Awake é não-determinística
3. **Infinite loop** em Coroutine — `IEnumerator` sem `yield return`
4. **Thread issue** — callback de SocketManager tocando GameObject sem marshal pra main thread

## Checklist (manual-verifier vai enumerar isso)

- [ ] Backend está rodando (`npm run dev` em `hora-extra-backend/`)
- [ ] Cena correta aberta no Editor antes do Play
- [ ] Console Unity está em "Clear on Play"
- [ ] Cada passo do plano §"Manual verification steps" foi executado em ordem
- [ ] Logs esperados apareceram com prefixos `[NETWORK]/[GAMEPLAY]/[UI]/[AI]`
- [ ] Nenhum LogError vermelho não esperado
- [ ] Comportamento visual bate com o descrito
- [ ] Saiu do Play Mode sem freeze/crash

## Anti-patterns

- ❌ **`Tests/` ou `*.Tests.cs`** em qualquer pasta do `hora-extra-client/`
- ❌ Plano sem seção "Manual verification steps"
- ❌ Logs sem prefixo (`Debug.Log("conectou")` — qual sistema?)
- ❌ Validar só por "compila no Unity" — compilar não é executar
- ❌ Confiar em comportamento de uma única run; flaky → re-rodar

## Gotchas

1. **Domain Reload** em entrada de Play Mode é lento (~5s). Não é bug — é Unity reimportando assemblies. Em settings (Edit > Project Settings > Editor > Enter Play Mode Options), pode desativar "Reload Domain" pra acelerar (a um custo: state estático persiste entre Plays).
2. **`[SerializeField]` que sumiu do Inspector** = você editou o C# com Editor aberto e algum stale ref ficou. Reimport ou rebuild.
3. **Logs com `$"..."` (string interp)** alocam strings — OK fora de `Update`. Em `Update`, considere `Debug.unityLogger.Log(LogType.Log, "[GAMEPLAY]", ...)` pra menos garbage.
4. **Console preencheu até 1000+ msgs** = filter ou Collapse. Mas se está spammando, é candidato a `Debug.Log` removível.
5. **NPE em referência que era cached em `Awake`**: ordem de `Awake` entre GameObjects não é determinística. Use `Start` se precisa que outros componentes já estejam prontos.
6. **Marshal pra main thread**: callbacks de `SocketManager` rodam em worker thread; tocar GameObject de lá = exception silenciosa ou crash. Use a fila `_mainThreadQueue` já existente.

## Referências

- `.agents/rules/no-unit-test-on-unity.md` — rule absoluta
- `hora-extra-client/Assets/Scripts/Network/SocketManager.cs` — main-thread queue pattern
- `hora-extra-client/Docs/Networking/Network_Debug_UI.md` — UI de debug existente, se aplicar
- `hora-extra-client/Assets/Scenes/SCN_Main.unity` — cena padrão de Play Mode
