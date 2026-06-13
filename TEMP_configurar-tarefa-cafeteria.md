# TEMP — Como configurar uma nova tarefa de cafeteira

> **Documento temporário.** Guia prático para criar/configurar uma tarefa do tipo
> cafeteira (QTE de café) no jogo. Apague quando não precisar mais.
> Baseado nos planos `0001`/`0002`/`0003` (sistema de tasks + tarefa do café).

---

## 1. Como uma tarefa de cafeteira funciona (visão geral)

```
[Cliente define catálogo]  →  task_catalog_register  →  [Servidor guarda por sala]
[Cliente pede tasks]       →  task_assign_request {} →  [Servidor sorteia N=3 aleatórias]
                                                          ↓ broadcast
                           ←  task_assigned { playerId, tasks[] }
[Jogador chega na cafeteira + tem a task]  →  prompt "Pressione E"
[Pressiona E]              →  task_start_interaction { taskId }  →  status: pending → in_progress
[Minigame QTE: N rounds de timing]
   acertou todos          →  task_complete_attempt { taskId, success:true }  → status: completed
   errou um               →  task_complete_attempt { taskId, success:false } → status: failed (terminal)
                           ←  task_updated { playerId, taskId, currentProgress, status }
```

O **servidor é a fonte de verdade do status**. O cliente só roda o timing local e
reporta o resultado.

Peças envolvidas:

| Peça | Arquivo |
|---|---|
| Catálogo + envio/recepção de eventos | `Assets/Scripts/Characters/TaskSystemBridge.cs` |
| Interação na cafeteira (trigger + prompt + gate) | `Assets/Scripts/Characters/CoffeeMakerInteraction.cs` |
| Minigame de timing | `Assets/Scripts/Characters/CoffeeQTE.cs` |
| Prefab da cafeteira | `Assets/Prefab/PFB_Interactable_CoffeeMaker.prefab` |
| DTOs/eventos | `Assets/Scripts/Network/Models/TaskModels.cs`, `NetworkEvents.cs` |

---

## 2. Passo a passo: adicionar uma nova tarefa de cafeteira

### Passo 1 — Definir a entrada no catálogo

A tarefa precisa existir no catálogo que o cliente envia ao servidor. Há duas formas:

**Opção A — pelo Inspector (recomendado):**

1. Selecione o GameObject que tem o componente **`TaskSystemBridge`** na cena.
2. No Inspector, encontre a lista **`Task Catalog — entradas iniciais da cena`**
   (campo `_initialCatalog`).
3. Adicione um item e preencha:

   | Campo | Valor | Observação |
   |---|---|---|
   | `id` | `task-coffee-maker-02` | **único** — não repetir com outras tasks |
   | `description` | `Faça o café da tarde` | texto livre exibido ao jogador |
   | `type` | `coffee_maker` | **obrigatório ser exatamente `coffee_maker`** (é o filtro do gate) |
   | `targetCount` | `3` | nº de rounds do QTE (dificuldade) |

**Opção B — por código:** veja `TaskSystemBridge.EnsureCoffeeMakerEntry()` — ele
adiciona a entrada padrão `task-coffee-maker-01` programaticamente se não existir.
Você pode duplicar essa lógica para garantir outra entrada.

> ⚠️ O campo `type` **tem que ser `coffee_maker`** (string exata). É a constante
> `TASK_TYPE` em `CoffeeMakerInteraction.cs` (linha 27) usada no gate
> `FindMyTask("coffee_maker", "pending", "in_progress")`. Qualquer outro valor não
> dispara a interação da cafeteira.

### Passo 2 — Colocar a cafeteira no mundo

1. Arraste o prefab **`Assets/Prefab/PFB_Interactable_CoffeeMaker`** para a cena
   (`SCN_Main.unity`) na posição desejada.
2. Confirme no prefab/instância:
   - **Trigger Collider** com `Is Trigger = true` (raio de interação).
   - Referências do `CoffeeMakerInteraction`:
     - `_promptUI` → canvas filho com o texto "Pressione E"
     - `_coffeeQte` → componente `CoffeeQTE` do mesmo prefab
     - `_interactKey` → `E` (padrão)
   - Referências do `CoffeeQTE` (todas opcionais — sem elas o minigame roda, mas sem feedback visual):
     - `_qteCanvas` → canvas do minigame
     - `_progressBar` → `Image` com `Image Type = Filled`
     - `_roundText` → `Text` "Round N/Total"
3. Garanta que o **GameObject do jogador tem a tag `Player`** — o trigger usa
   `CompareTag("Player")`.

### Passo 3 — Ajustar a dificuldade (opcional)

No componente **`CoffeeQTE`** do prefab:

| Campo | Padrão | Efeito |
|---|---|---|
| `_windowDuration` | `2` s | duração de cada round (menor = mais difícil) |
| `_hitZoneStart` | `0.55` | início da janela verde de acerto (0–1) |
| `_hitZoneEnd` | `0.85` | fim da janela verde de acerto (0–1) |
| `_qteKey` | `E` | tecla de acerto durante o QTE |

O **número de rounds** vem do `targetCount` da task (Passo 1), não daqui.

---

## 3. Como testar

1. Suba o backend em localhost:
   ```bash
   cd hora-extra-backend
   npm run dev
   ```
   (`.env` já está em `USE_SQLITE=true`. Cliente aponta pra `127.0.0.1` via
   `BackendConfig.Host`.)
2. Entre em **Play Mode** no Unity (`SocketManager.UseTestToken = true`).
3. Confirme no Console: `[NETWORK] task_catalog_register enviado — N tarefa(s)`
   (N deve incluir sua entrada `coffee_maker`).
4. Dispare a atribuição: chame `TaskSystemBridge.Instance.RequestMyTasks()`
   (botão de debug ou via `Start`). O sorteio é **aleatório (N=3)** — pode ser
   necessário pedir algumas vezes / resetar até cair a sua task.
   - Esperado: `[GAMEPLAY] tasks recebidas ...` com uma task `type=coffee_maker`, `status=pending`.
5. Aproxime o jogador da cafeteira → prompt **"Pressione E"** aparece.
6. Pressione **E** → QTE inicia (`status: in_progress`).
7. Acerte os 3 rounds → `status: completed`. Erre um → `status: failed`.

---

## 4. Limitações importantes (ler antes de criar várias)

- **Sorteio aleatório:** a task só é atribuída se o servidor sorteá-la entre as
  N=3. Para garantir, use catálogos pequenos ao testar ou chame `RequestMyTasks()`
  repetidamente após reset.

- **Gate por `type`, não por `id` (⚠️ limitação real):** o
  `CoffeeMakerInteraction` usa `FindMyTask("coffee_maker", ...)`, que retorna a
  **primeira** task `coffee_maker` do jogador. Ou seja: **se o jogador tiver duas
  tasks `coffee_maker` ao mesmo tempo, TODAS as cafeteiras da cena apontam para a
  mesma (a primeira)**. Hoje o sistema suporta bem **uma** task de café por
  jogador. Para múltiplas cafeteiras distintas seria preciso evoluir o gate para
  casar por `id` (ex.: um `[SerializeField] string _taskId` no
  `CoffeeMakerInteraction` filtrando a task específica). Isso ficou explicitamente
  fora de escopo no plano `0003`.

- **Falha é terminal:** `status=failed` não pode ser refeito (decisão de design).
  Ao falhar, o prompt não reaparece para aquela task.

- **Sem persistência:** todo o estado é in-memory no servidor. Reiniciar/resetar a
  sala (`resetRoom` via dev token) limpa catálogo e atribuições.

---

## 5. Resumo rápido (TL;DR)

1. `TaskSystemBridge._initialCatalog` → adicionar `{ id único, description, type:"coffee_maker", targetCount:N }`.
2. Arrastar `PFB_Interactable_CoffeeMaker` para a cena; conferir refs + tag `Player`.
3. (Opcional) ajustar `CoffeeQTE` para dificuldade.
4. Testar: backend up → Play Mode → `RequestMyTasks()` → aproximar → `E` → QTE.
5. ⚠️ Hoje funciona bem com **uma** task `coffee_maker` por jogador.
