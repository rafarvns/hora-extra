# Plan 0011 — infra-slot-de-posicionamento

> **Plano-base do eixo "posição-alvo".** Origem: `TAREFAS DOS JOGADORES.pdf` — a Tarefa **6**
> (*"cada item possui uma posição-alvo definida"*) e a Tarefa **12** (*"a tarefa é concluída ao
> posicionar corretamente o item"*) pedem que o objeto **fique visível no lugar certo**, não que
> desapareça.
>
> Depende dos planos [0004](0004-infra-carregar-e-validar-itens.md) (carregar/entregar, `pairs`)
> e [0007](0007-tarefa-15-organizar-materiais-almoxarifado.md) (componente de destaque de slot
> vazio, reusado aqui). Consumido por: [0016](0016-tarefa-06-organizar-mesas-do-escritorio.md),
> [0022](0022-tarefa-12-posicionar-item-especifico.md). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O `TaskDepositPoint` do plano 0004 modela **recipiente**: o item entra na caixa, no
porta-canetas, no lixo, e some. `Consume()` desativa o GameObject porque, naqueles casos, ninguém
precisa vê-lo depois — é literalmente o que o documento de entrega pede para a Tarefa 16
(*"o papel some da 'mão' do jogador"*).

Duas tarefas da lista-mestra pedem o oposto:

- **Tarefa 6 — Organizar as mesas.** *"O jogador deve posicionar corretamente itens como
  documentos, canetas, grampeadores e pastas. Cada item possui uma posição-alvo definida. A
  tarefa é concluída quando todos os objetos estiverem nos locais corretos."* Uma mesa em que os
  objetos sumiram ao serem "organizados" não é uma mesa organizada — é uma mesa vazia. O
  resultado da tarefa **é** o estado visual.
- **Tarefa 12 — Posicionar item específico.** Mesma coisa com um item só.

A diferença entre as duas famílias é **puramente visual e local**. O protocolo já cobre a
validação: `pairs` (plano 0004) diz que `item-X` pertence ao `slot-Y`, e o servidor rejeita
`WRONG_SLOT`. Não há nada a acrescentar no servidor — e é importante **não** acrescentar, para
não criar um segundo caminho de validação fazendo o mesmo que `pairs`.

Este plano entrega, então, um componente de cliente: `TaskPlacementSlot`, irmão do
`TaskDepositPoint`, que em vez de desativar o item o **encaixa** na posição-alvo.

### Por que irmão e não flag

A alternativa seria um `bool _snapInsteadOfConsume` no `TaskDepositPoint`. Foi descartada: os dois
têm ciclos de vida diferentes (o recipiente aceita N itens de uma categoria; o slot aceita **um**
item específico e depois está ocupado), estados diferentes (`ocupado` não existe no recipiente) e
prompts diferentes. Uma flag faria metade dos campos do Inspector serem ignorados em cada modo —
a fonte clássica de bug de configuração de cena. O código comum (gate de prompt, envio, espera da
confirmação, devolução em rejeição) sai para uma base abstrata compartilhada.

## 2. Scope & target

**Target:** `client`

**Phase backend — nenhuma mudança, nem de código nem de documentação.** Não há evento novo, campo
novo nem código de rejeição novo: `pairs`, `task_progress` com `itemId`/`slotId` e `WRONG_SLOT`
já existem desde o plano 0004 e cobrem 100% da validação. Consequência prática pela
`.agents/rules/`: `COMMUNICATION.md` **não** é tocado (nada a sincronizar) e o fluxo
`/feature` roda `manual-verifier`, não `test-runner`.

**Phase client** — refatorar o `TaskDepositPoint` do plano 0004 extraindo uma base abstrata
`TaskDeliveryTarget`; novo `TaskPlacementSlot.cs`; `TaskPlacementGhost.cs` para a pré-visualização;
reuso do componente de destaque do plano 0007; `TaskPresentation` ganha o fallback de prompt de
posicionamento.

## 3. Approach

### `Interactions/TaskDeliveryTarget.cs` (NEW, abstrata)

Sobe para cá o que o `TaskDepositPoint` do plano 0004 já faz e que os dois modos compartilham:

- `[SerializeField] protected string _slotId`, `_acceptedKind`, `_taskType`, `_promptMessage`;
- gate de exibição do prompt (jogador no trigger + carregando item do `_acceptedKind` + task do
  tipo em `pending`/`in_progress`);
- ao pressionar [E]: guardar `_pendingItem`, `SendProgress(taskId, itemId, _slotId)` e **esperar**;
- `OnTaskUpdated` com progresso maior → `OnDeliveryConfirmed(item)` (abstrato);
- `OnTaskRejected` para esta task → devolve o item para a mão e exibe `payload.Message`.

`TaskDepositPoint` passa a herdar e implementar `OnDeliveryConfirmed` chamando `carrier.Consume()`
— **comportamento idêntico ao do plano 0004**. Esta é uma refatoração sem mudança de
comportamento; os planos 0005–0008 não precisam de nenhum ajuste.

### `Interactions/TaskPlacementSlot.cs` (NEW)

Herda de `TaskDeliveryTarget`. Campos próprios:

- `[SerializeField] private Transform _anchor` — posição **e rotação** alvo. Se vazio, usa o
  próprio transform.
- `[SerializeField] private string _expectedItemId` — qual item pertence a este slot. Espelha o
  `pairs` do catálogo; existe para o cliente poder mostrar o ghost certo e para o prompt não
  aparecer com o item errado na mão. **Não é a validação** — quem valida é o servidor.
- `[SerializeField] private bool _lockAfterPlacement = true`.
- `public bool IsOccupied { get; private set; }`.

`OnDeliveryConfirmed(item)`:

1. `carrier.Release(item)` — solta sem desativar (método novo no `PlayerCarrier`, irmão de
   `Consume()`/`DropCurrent()`);
2. reparenta em `_anchor`, zera `localPosition`/`localRotation` — o encaixe é exato, não físico;
3. desliga o `Collider` do item e, se houver `Rigidbody`, marca `isKinematic` (senão o objeto
   "organizado" escorrega da mesa no primeiro esbarrão);
4. `IsOccupied = true`; esconde ghost e destaque; deixa de exibir prompt.

Ordem importa: reparentar **antes** de zerar o local transform, e zerar com o item já desligado da
física. Um item com `Rigidbody` não-kinemático reparented mantém velocidade e sai andando.

### `Interactions/TaskPlacementGhost.cs` (NEW)

Pré-visualização translúcida do item esperado enquanto o slot está vazio:

- `[SerializeField] private GameObject _ghostPrefab` e `_ghostMaterial` (`MAT_` por
  `.agents/rules/unity-asset-management.md`);
- instanciado desativado no `Awake`, sem collider, na layer `Ignore Raycast`;
- visível **só** quando: slot vazio **e** task do tipo ativa **e** o jogador está carregando o
  item esperado. A última condição é o que impede a mesa virar um mapa de fantasmas antes do
  jogador entender a tarefa — o ghost é dica de *encaixe*, não de *busca*.
- Quem indica "onde tem lugar vazio" é o destaque do plano 0007
  (`SlotHighlight`), reusado aqui sem alteração e com gate mais frouxo (slot vazio + task ativa).

### `UI/TaskPresentation.cs` (MODIFY)

Fallback genérico de posicionamento (`"Pressione E para posicionar"`), no mesmo formato do
fallback de entrega do plano 0004. Os textos por tarefa entram nos planos 0016 e 0022.

### `Characters/PlayerCarrier.cs` (MODIFY)

`public void Release(CarryableItem item)` — tira da mão **sem** desativar e sem devolver para a
posição original (que é o que `DropCurrent()` faz). Dispara o mesmo `OnCarryChanged` dos outros
dois caminhos, para o HUD não ficar dessincronizado.

## 4. Files to change

### Backend

Nenhum. Ver §2 — este plano não altera o contrato de rede.

### Client

```
hora-extra-client/Assets/Scripts/Interactions/TaskDeliveryTarget.cs   NEW (base abstrata extraída de TaskDepositPoint)
hora-extra-client/Assets/Scripts/Interactions/TaskDepositPoint.cs     MODIFY (passa a herdar de TaskDeliveryTarget — sem mudança de comportamento)
hora-extra-client/Assets/Scripts/Interactions/TaskPlacementSlot.cs    NEW
hora-extra-client/Assets/Scripts/Interactions/TaskPlacementGhost.cs   NEW
hora-extra-client/Assets/Scripts/Interactions/PlayerCarrier.cs        MODIFY (Release)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs               MODIFY (fallback de prompt de posicionamento)
hora-extra-client/Assets/Materials/MAT_PlacementGhost.mat             NEW (asset, Editor — translúcido)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                  MODIFY (asset, Editor — 1 slot de sanidade)
hora-extra-client/Docs/Mechanics/PLACEMENT-SLOTS.md                   NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD — não há código de backend neste plano.**

Rodar a suíte completa mesmo assim, como regressão de que nada no servidor foi tocado por engano:

```bash
cd hora-extra-backend && npm test
```

Se durante a implementação aparecer necessidade de lógica nova no servidor, **parar**: é sinal de
que o desenho escorregou para um segundo caminho de validação paralelo ao `pairs`. Reavaliar antes
de escrever, e se for mesmo necessário, abrir ciclo TDD — `.agents/rules/backend-unit-tests.md`
não abre exceção.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando (`npm run dev`), `SCN_FirstFloor.unity` aberta,
`SocketManager.UseTestToken = true`, Console com "Clear on Play".

**Cena de sanidade:** dois `CarryableItem` (`item-sanity-a`, `item-sanity-b`, ambos
`_kind = "sanity"`) e dois `TaskPlacementSlot` (`slot-sanity-a` esperando `item-sanity-a`;
`slot-sanity-b` esperando `item-sanity-b`), com a entrada de catálogo `task-sanity-placement`
declarando `pairs = [{item-sanity-a → slot-sanity-a}, {item-sanity-b → slot-sanity-b}]` e
`targetCount = 2`. Descartar depois.

### 1. Regressão do recipiente (plano 0004/0005)

- Ação: executar uma entrega em um `TaskDepositPoint` já existente.
- Esperado: comportamento **idêntico** ao de antes da refatoração — item some, progresso soma.
  Este passo vem primeiro de propósito: a extração da base abstrata é a única parte deste plano
  que pode quebrar algo que já funcionava.

### 2. Destaque de slot vazio

- Ação: receber a task e olhar os dois slots de mãos vazias.
- Esperado: `SlotHighlight` (plano 0007) ativo nos dois; **nenhum ghost** visível ainda.

### 3. Ghost aparece com o item certo na mão

- Ação: pegar `item-sanity-a` e andar pela cena.
- Esperado: o ghost aparece **só** em `slot-sanity-a`; `slot-sanity-b` continua só com destaque.

### 4. Slot errado não aceita

- Ação: carregando `item-sanity-a`, chegar em `slot-sanity-b` e pressionar [E].
- Esperado: prompt não aparece (`_expectedItemId` não bate). Se aparecer e for enviado, o servidor
  responde `task_rejected — code=WRONG_SLOT` e o item **volta para a mão** — também aceitável,
  mas o gate local deveria ter evitado o pacote.

### 5. Encaixe autoritativo

- Ação: levar `item-sanity-a` até `slot-sanity-a` e pressionar [E].
- Esperado, nesta ordem: `[NETWORK] task_progress enviado — itemId=item-sanity-a slotId=slot-sanity-a`
  → `[GAMEPLAY] task_updated … progress=1` → **só então** o item sai da mão e aparece encaixado no
  `_anchor`. Posição e rotação exatas do anchor, **não** aproximadas.

### 6. Item fica parado

- Ação: correr e esbarrar no item encaixado; dar um Play/Pause e olhar o Inspector.
- Esperado: não se move, não cai, não gira. `Collider` desligado e `Rigidbody.isKinematic = true`
  (ou sem `Rigidbody`).

### 7. Slot ocupado não aceita de novo

- Ação: pegar `item-sanity-b` e ir até `slot-sanity-a`.
- Esperado: prompt não aparece; ghost e destaque de `slot-sanity-a` continuam desligados.

### 8. Concluir

- Ação: encaixar `item-sanity-b` em `slot-sanity-b`.
- Esperado: `task_updated … status=completed progress=2`; os dois itens visíveis nos lugares;
  todos os destaques e ghosts desligados.

### 9. Rejeição devolve o item

- Ação: reativar `item-sanity-a` pelo Inspector e tentar encaixar de novo.
- Esperado: `task_rejected — code=ALREADY_COUNTED`; o item volta para a **mão** (não para o chão,
  não para o slot); nada se move na mesa.

### 10. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; nenhum ghost órfão na hierarquia; nenhum item preso no `_handAnchor`.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo; nada de servidor muda neste plano
npx tsc --noEmit
```

### Client

Seguir os 10 passos da §6 em Play Mode.

## 8. Out of scope

- As entradas de catálogo e o setup de cena das Tarefas 6 e 12 — planos 0016 e 0022.
- **Validação de posição pelo servidor.** O servidor valida o par `itemId → slotId` (plano 0004),
  não coordenadas. Conferir se o objeto está mesmo *em cima da mesa* seria física autoritativa —
  fora do escopo de toda esta entrega.
- Encaixe livre (o jogador escolhe onde soltar, com snap por proximidade). O alvo é sempre um
  `_anchor` autorado.
- Pré-visualização animada/pulsante do ghost — é material translúcido estático.
- Desfazer um encaixe (pegar de volta o item já posicionado). `_lockAfterPlacement` existe como
  campo para o dia em que isso for pedido, mas o caminho de "despposicionar" exigiria um evento
  de decremento no servidor, que **não** existe e não é criado aqui.
- Sincronizar entre jogadores o item já encaixado — segue a mesma limitação do plano 0004: cada
  cliente vê a própria cena.
- Slot que aceita mais de um item ou ordem interna.
- Física de empilhamento.
