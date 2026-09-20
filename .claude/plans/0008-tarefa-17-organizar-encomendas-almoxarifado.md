# Plan 0008 — tarefa-17-organizar-encomendas-almoxarifado

> Origem: `Explicação das tarefas para essa entrega.pdf` — **Tarefa 17 — Organizar encomendas no
> almoxarifado**. **Depende do plano [0004](0004-infra-carregar-e-validar-itens.md)**; é a tarefa
> mais dependente da validação por `pairs` introduzida lá.

## 1. Context

A Tarefa 17 é a única da entrega em que **o jogador pode errar**. O documento:

> *"O jogador precisa colocar 6 caixas de encomendas na sua prateleira correta e precisa jogar 6
> caixas de encomendas erradas no lixo. Cada encomenda vai em uma estante. As estantes das
> respectivas encomendas estão com nomes. Tem estante de: Almoxarifado, Recepção, Comercial, TI,
> Financeiro e Recursos Humanos. Cada encomenda tem uma etiqueta com o nome da empresa (Horizon
> Corporate). As outras 6 caixas erradas terão nomes de outras empresas, então o importante aqui é
> o jogador conseguir encontrar a logo na caixa (uma boa solução para isso é o jogador poder
> rotacionar os objetos). As caixas certas tem a TAG CaixaCerta. As caixas erradas tem a TAG
> CaixaErrada."*

Três exigências derivam disso:

1. **Pareamento estrito** — encomenda X só conta na estante X. Isso é exatamente o campo `pairs`
   do plano 0004: nenhuma outra tarefa da entrega justifica tanto o mecanismo, porque aqui o
   cliente **não pode** decidir o acerto localmente sem duplicar a regra do servidor.
2. **Inspeção do objeto** — o jogador precisa girar a caixa na mão para achar a etiqueta/logo.
   Mecânica nova, não coberta pelo plano 0004.
3. **Duas famílias de destino** — 6 estantes nomeadas + 1 lixo, todas modeladas como `slotId`.

As tags `CaixaCerta` e `CaixaErrada` **já existem** em `ProjectSettings/TagManager.asset`.

**Ponto de arquitetura:** as tags são usadas apenas para **autoria e conferência de cena** (e para
o editor helper da §3), **nunca** como fonte de verdade de gameplay. Se o cliente decidisse o
acerto lendo a tag, a tarefa inteira viraria client-authoritative — o oposto da regra
`.agents/rules/backend-design-pattern.md`. Quem decide se a encomenda foi parar no lugar certo é
o servidor, via `pairs`.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova: `pairs` + `WRONG_SLOT` já vêm do plano 0004. Só
documentação, incluindo o registro de que esta é a tarefa em que `task_rejected` é **feedback de
gameplay esperado**, não erro.

**Phase client** — novo `InspectableCarriedItem.cs` (rotacionar o objeto na mão); uma entrada de
catálogo com 12 `pairs`; tipo novo em `TaskPresentation` com feedback de erro; setup de cena das
12 encomendas, 6 estantes e do lixo do almoxarifado.

### Contratos cross-repo

Nenhum evento novo. Uma entrada de catálogo, com `pairs` cobrindo os 12 itens:

| taskId | type | targetCount |
| :----- | :--- | :---------- |
| `task-almox-encomendas-01` | `sort_parcels` | 12 |

```
pairs = [
  { itemId: "encomenda-almoxarifado", slotId: "slot-estante-almoxarifado" },
  { itemId: "encomenda-recepcao",     slotId: "slot-estante-recepcao" },
  { itemId: "encomenda-comercial",    slotId: "slot-estante-comercial" },
  { itemId: "encomenda-ti",           slotId: "slot-estante-ti" },
  { itemId: "encomenda-financeiro",   slotId: "slot-estante-financeiro" },
  { itemId: "encomenda-rh",           slotId: "slot-estante-rh" },
  { itemId: "encomenda-errada-01",    slotId: "slot-lixo-almoxarifado" },
  ... encomenda-errada-02 … -06 → slot-lixo-almoxarifado
]
```

O lixo entra como mais um `slotId` pareado — não precisa de tratamento especial no servidor. É o
mesmo `WRONG_SLOT` que rejeita "encomenda certa no lixo" e "encomenda errada na estante".

## 3. Approach

### Backend

Só documentação em `COMMUNICATION.md`:

- a entrada `task-almox-encomendas-01` como exemplo de `pairs` com múltiplos destinos;
- uma nota na descrição de `task_rejected` deixando claro que `WRONG_SLOT` nesta task é
  **resultado de jogo** (o jogador errou a estante), e o cliente deve tratá-lo como feedback, não
  como falha de rede.

Nenhum handler, service ou factory muda.

### Client

#### `InspectableCarriedItem.cs` (NEW, `Assets/Scripts/Interactions/`)

Permite girar o objeto que está na mão para achar a etiqueta. Componente no mesmo GameObject do
`CarryableItem`.

```csharp
[SerializeField] private float _rotationSpeed = 200f;
[SerializeField] private string _inspectHint = "Segure o botão direito e mova o mouse para girar a caixa";
```

- Só age quando `PlayerCarrier.Carried == this` — em `Update`, sai cedo caso contrário.
- Enquanto o botão direito do mouse estiver pressionado: aplica o delta do mouse como rotação
  local (`transform.Rotate(Vector3.up, -deltaX * _rotationSpeed * Time.deltaTime, Space.World)` e
  o equivalente em `Vector3.right` para `deltaY`).
- Enquanto está girando, **trava a câmera** do jogador para o botão direito não mexer nas duas
  coisas ao mesmo tempo. Conferir como `PlayerController`/a câmera consomem o botão direito hoje;
  se houver conflito, a alternativa é segurar **R** para entrar em modo de inspeção e usar o mouse
  livre. **Decidir no momento da implementação, testando na cena** — a escolha muda só este script.
- Ao soltar, mantém a rotação atual (não faz snap de volta).
- Entrada via `#if ENABLE_INPUT_SYSTEM` / legacy, no mesmo padrão de `InteractionInput` (plano 0004);
  se a leitura de mouse for reaproveitável, movê-la para `InteractionInput` como
  `InspectHeld()` / `MouseDelta()`.
- `_inspectHint` é exibido pelo `InteractionPrompt` enquanto a caixa está na mão, para o jogador
  descobrir o controle.

#### `TaskDepositPoint` — reuso sem alteração de código

As 6 estantes e o lixo são `TaskDepositPoint` com `_acceptedKind = "encomenda"` e
`_taskType = "sort_parcels"`, diferindo **só** no `_slotId`. Como todas as 12 caixas têm o mesmo
`_kind`, o filtro local **não** decide nada: o prompt aparece em qualquer estante, o jogador
entrega, e o servidor responde `task_updated` (acertou) ou `task_rejected`/`WRONG_SLOT` (errou),
com o item voltando para a mão no segundo caso. É o comportamento desejado — a decisão é do
servidor.

Isso confirma que o plano 0004 já resolve esta tarefa sem código de entrega novo; o que falta é
só a inspeção e o texto de feedback.

#### Apresentação (`TaskPresentation.cs`)

```csharp
public const string TYPE_SORT_PARCELS = "sort_parcels";
```

| método | texto |
| :----- | :---- |
| `GetTitle` | "Organizar as encomendas" |
| `GetHowTo` | "Gire cada caixa para achar a etiqueta. As da Horizon Corporate vão na estante do setor escrito nelas; as de outras empresas vão no lixo." |
| `GetActionPrompt` | "Aperte [E] para colocar a encomenda (X/12)" |

Feedback de rejeição: o `TaskDepositPoint` já exibe `payload.Message` do `task_rejected`. Para
esta task, mapear o `code` para um texto de jogo em vez da mensagem crua do servidor:

- `WRONG_SLOT` → "Essa encomenda não é deste lugar. Confira a etiqueta."
- `ALREADY_COUNTED` → "Essa encomenda já foi organizada."

Centralizar esse mapeamento em `TaskPresentation.GetRejectionMessage(string code, string fallback)`,
com `fallback` = mensagem do servidor para códigos não mapeados.

#### Cena `SCN_Main.unity`

12 encomendas recebem `CarryableItem` + `InspectableCarriedItem`, todas com
`_kind = "encomenda"`, `_isTool = false`,
`_pickupMessage = "Aperte [E] para pegar a encomenda"`:

| GameObject | `_itemId` | tag | destino correto |
| :--------- | :-------- | :-- | :-------------- |
| encomenda Almoxarifado | `encomenda-almoxarifado` | `CaixaCerta` | estante Almoxarifado |
| encomenda Recepção | `encomenda-recepcao` | `CaixaCerta` | estante Recepção |
| encomenda Comercial | `encomenda-comercial` | `CaixaCerta` | estante Comercial |
| encomenda TI | `encomenda-ti` | `CaixaCerta` | estante TI |
| encomenda Financeiro | `encomenda-financeiro` | `CaixaCerta` | estante Financeiro |
| encomenda RH | `encomenda-rh` | `CaixaCerta` | estante Recursos Humanos |
| encomendas erradas ×6 | `encomenda-errada-01` … `-06` | `CaixaErrada` | lixo |

7 destinos recebem `TaskDepositPoint` (+ `WorldTaskMarker` com `_taskType = "sort_parcels"`):

| Destino | `_slotId` |
| :------ | :-------- |
| estante Almoxarifado | `slot-estante-almoxarifado` |
| estante Recepção | `slot-estante-recepcao` |
| estante Comercial | `slot-estante-comercial` |
| estante TI | `slot-estante-ti` |
| estante Financeiro | `slot-estante-financeiro` |
| estante Recursos Humanos | `slot-estante-rh` |
| lixo do almoxarifado | `slot-lixo-almoxarifado` |

**Conferência obrigatória antes de testar:** as 12 etiquetas visuais (empresa + setor) precisam
bater com os `pairs` do catálogo. Uma caixa cuja etiqueta diz "TI" mas cujo `_itemId` está pareado
com a estante Financeiro deixa a task injusta e indepurável. Vale um `EditorWindow`/script de
validação que percorra a cena e compare tag `CaixaCerta`/`CaixaErrada` com o destino declarado no
catálogo — as tags existem exatamente para permitir essa conferência automatizada.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada sort_parcels com 12 pairs + nota sobre WRONG_SLOT como feedback de jogo)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/InspectableCarriedItem.cs   NEW
hora-extra-client/Assets/Scripts/Interactions/InteractionInput.cs         MODIFY (InspectHeld/MouseDelta, se a leitura de mouse for centralizada)
hora-extra-client/Assets/Scripts/Interactions/TaskDepositPoint.cs         MODIFY (usar TaskPresentation.GetRejectionMessage no feedback)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs           MODIFY (EnsureParcelEntry — 1 entrada com 12 pairs)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs                   MODIFY (TYPE_SORT_PARCELS + ramos + GetRejectionMessage)
hora-extra-client/Assets/Scenes/SCN_Main.unity                            MODIFY (asset, Editor — 12 encomendas + 7 destinos)
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo** — sem lógica de servidor. O caminho crítico desta tarefa
(`pairs` → `WRONG_SLOT`) é coberto pelos ciclos 6–8 do plano 0004, e `ALREADY_COUNTED` pelo
ciclo 5.

Vale rodar a suíte com atenção ao ciclo 7 (`WRONG_SLOT` não incrementa `currentProgress`): é a
garantia de que errar a estante nesta tarefa não conta ponto.

Regressão obrigatória: `cd hora-extra-backend && npm test`.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_Main.unity` aberta, `UseTestToken = true`, "Clear on Play".
Para garantir o sorteio desta task, esvaziar temporariamente o `_initialCatalog` deixando só
`task-almox-encomendas-01`.

### 0. Conferência de etiquetas (fazer ANTES de testar)

- Ação: para cada uma das 12 caixas, comparar a etiqueta visual com o `_itemId` e com o `slotId`
  pareado no catálogo.
- Esperado: as 6 `CaixaCerta` são "Horizon Corporate" e apontam para a estante do setor escrito
  nelas; as 6 `CaixaErrada` são de outras empresas e apontam para `slot-lixo-almoxarifado`.
  Divergência aqui invalida todos os testes seguintes.

### 1. Catálogo com 12 pairs

- Ação: entrar em Play Mode.
- Esperado: `[NETWORK] task_catalog_register enviado`; backend **não** loga warn de shape de
  `pairs`; `MissionListHud` mostra "Organizar as encomendas" com o how-to da §3 e progresso `0/12`.

### 2. Pegar uma encomenda

- Ação: chegar numa caixa e pressionar [E].
- Esperado: `"Aperte [E] para pegar a encomenda"`; a caixa vai para a mão;
  o hint de inspeção aparece.

### 3. Rotacionar para achar a etiqueta

- Ação: segurar o botão direito (ou o controle escolhido na §3) e mover o mouse.
- Esperado: a caixa gira nos dois eixos de forma suave; dá para achar a face com a logo; **a
  câmera do jogador não gira junto**. Ao soltar, a caixa mantém a rotação.

### 4. Encomenda certa na estante certa

- Ação: levar a encomenda de TI até a estante TI e pressionar [E].
- Esperado, nesta ordem:
  - `"Aperte [E] para colocar a encomenda (0/12)"`;
  - `[NETWORK] task_progress enviado — taskId=task-almox-encomendas-01 itemId=encomenda-ti slotId=slot-estante-ti`;
  - `[GAMEPLAY] task_updated recebido — … progress=1`;
  - **só então** a caixa some da mão.

### 5. Encomenda certa na estante ERRADA — o caso central

- Ação: levar a encomenda de Recepção até a estante Financeiro e pressionar [E].
- Esperado:
  - o prompt **aparece** normalmente (o cliente não sabe que está errado — isso é esperado);
  - `[NETWORK] task_rejected — code=WRONG_SLOT taskId=task-almox-encomendas-01`;
  - mensagem "Essa encomenda não é deste lugar. Confira a etiqueta.";
  - a caixa **volta para a mão**;
  - o contador **continua** no valor anterior. Se incrementar, a validação do plano 0004 está
    furada — é falha grave.

### 6. Encomenda errada no lixo

- Ação: levar uma caixa de outra empresa (`CaixaErrada`) ao lixo do almoxarifado e pressionar [E].
- Esperado: `task_updated` normal, progresso +1, caixa some da mão.

### 7. Encomenda errada numa estante

- Ação: levar uma `CaixaErrada` até qualquer estante.
- Esperado: `task_rejected — code=WRONG_SLOT`; caixa volta para a mão; contador inalterado.

### 8. Encomenda certa no lixo

- Ação: levar uma `CaixaCerta` até o lixo.
- Esperado: `task_rejected — code=WRONG_SLOT`; caixa volta para a mão. (O jogador não pode
  "resolver" a tarefa jogando tudo fora.)

### 9. Repetição

- Ação: reativar pelo Inspector uma encomenda já organizada, pegar e entregar de novo no mesmo
  destino.
- Esperado: `task_rejected — code=ALREADY_COUNTED`; mensagem "Essa encomenda já foi organizada.";
  contador inalterado.

### 10. Conclusão

- Ação: organizar as 12 caixas corretamente (6 estantes + 6 no lixo).
- Esperado: na décima segunda, `status=completed progress=12`; mensagem "Tarefa concluída com
  sucesso!"; `WorldTaskMarker` das 7 estantes/lixo some; prompts deixam de aparecer.

### 11. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError` residual; nenhuma caixa presa no `_handAnchor`; nenhuma caixa com
  rotação acumulada absurda quebrando o collider.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test
npx tsc --noEmit
```

### Client

Seguir os passos 0–11 da §6 em Play Mode.

## 8. Out of scope

- Penalidade por errar (perder ponto, tempo ou falhar a task). O documento não pede: errar apenas
  não conta progresso e devolve a caixa para a mão. `WRONG_SLOT` é terminal-zero, não destrutivo.
- Snap visual da caixa na prateleira (o objeto é desativado ao ser entregue).
- Zoom/foco de câmera no modo de inspeção — só rotação.
- Usar as tags `CaixaCerta`/`CaixaErrada` como lógica de acerto em runtime (ver "Ponto de
  arquitetura" na §1); elas ficam para autoria e para o script de conferência de cena.
- O script/EditorWindow de validação cena↔catálogo sugerido na §3 é recomendado mas opcional; se
  for implementado, é ferramenta de editor e **não** entra no build.
- Carregar mais de uma encomenda por vez.
- Estantes com capacidade limitada ou ordem interna.
- Recepção, limpeza e materiais do almoxarifado — planos 0005, 0006 e 0007.
