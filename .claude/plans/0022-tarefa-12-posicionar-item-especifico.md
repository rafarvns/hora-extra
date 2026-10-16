# Plan 0022 — tarefa-12-posicionar-item-especifico

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 12 — Posicionar item específico**.
> **Depende de [0004](0004-infra-carregar-e-validar-itens.md)** (carregar + `pairs`) e de
> [0011](0011-infra-slot-de-posicionamento.md) (`TaskPlacementSlot`).
> Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> o jogador recebe uma **solicitação** para colocar um item específico em determinado local →
> deve **localizar o objeto correto** no cenário → após coletá-lo, deve transportá-lo até a área
> indicada → a tarefa é concluída ao posicionar corretamente o item

Mecanicamente é a menor tarefa das 18: **um** item, **um** destino. `pairs` com um par só,
`targetCount = 1`, `TaskPlacementSlot` do plano 0011. Zero código de gameplay novo.

O que dá substância à tarefa é o que **não** é mecânico: a solicitação e a busca. Se a cena
marcar o item com um holofote, a tarefa vira "ande até lá e aperte [E] duas vezes" e não sobra
nada. Por isso este plano trata o desenho de *quais objetos parecidos existem no cenário* como a
entrega principal, e não como decoração.

### Decisão: nenhum marcador no item, marcador só no destino

O `WorldTaskMarker` (plano 0003) fica **só** no destino. O item não tem marcador, não tem
destaque e não tem ghost até estar na mão. A pista é textual: a solicitação diz o que procurar
(*"a pasta azul do arquivo"*), e o cenário tem pastas de outras cores. Achar é a tarefa.

É a mesma lógica do post-it do plano 0014 (*"o bilhete não tem marcador"*) e da área de trabalho
do plano 0015 (*"os ícones ficam visualmente iguais em peso"*). Três tarefas diferentes chegando
à mesma regra: **quando encontrar é o gameplay, não sinalizar**.

### Decisão: o item errado nem entra no catálogo

Os objetos-isca (as pastas das outras cores) são `CarryableItem` normais com `_kind` igual ao do
item certo, mas com `itemId` **fora** do `items`/`pairs` da task. Consequência: o jogador
consegue pegar e levar até o destino, o prompt aparece — e o servidor responde `UNKNOWN_ITEM`, o
item volta para a mão, e a mensagem explica.

É melhor do que bloquear o pickup da isca: a tentativa frustrada ensina, e o erro é recuperável e
barato. E exercita um código de rejeição do plano 0004 que nenhuma outra tarefa exercita de forma
natural.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. Catálogo sobre o `pairs` do plano 0004; o backend só
recebe documentação.

**Phase client** — `TaskRequestPanel.cs` (a solicitação); entrada de catálogo; textos; setup de
cena com o item certo, 3 iscas e o destino.

### Contratos cross-repo

Nenhum evento novo.

| taskId | type | targetCount | pairs |
| :----- | :--- | :---------- | :---- |
| `task-posicionar-item-01` | `place_item` | 1 | `pasta-azul-01 → slot-arquivo-pasta` |

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md` como **menor exemplo possível de
`pairs`** e acrescentar `place_item` aos valores conhecidos de `type`.

Vale anotar no doc que `UNKNOWN_ITEM` é, nesta tarefa, um **caminho de gameplay esperado** e não
um sintoma de bug — quem for ler os logs depois precisa saber disso.

### Client

#### `UI/TaskRequestPanel.cs` (NEW)

A "solicitação" do documento. Painel que aparece quando a task é atribuída, com o pedido redigido
em prosa:

> *"Preciso que você leve **a pasta azul** do arquivo para a prateleira da recepção. Tem várias
> pastas por aí, mas é a azul."*

- Aparece no `task_assigned`, fecha sozinho depois de alguns segundos ou com um clique, e pode ser
  reaberto pelo `MissionListHud` — perder a solicitação não pode travar a tarefa.
- Texto num `[SerializeField, TextArea]`, **não** no catálogo: o catálogo carrega o `pairs`, e a
  solicitação é conteúdo de UI. Mesma decisão do `MailBriefingPanel` no plano 0017 — se esse plano
  já estiver implementado, considerar unificar os dois num painel de briefing só.

#### Catálogo e apresentação

`TaskSystemBridge.EnsurePlaceItemEntry()`.

`TaskPresentation` ganha `TYPE_PLACE_ITEM`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Levar a pasta azul para a recepção" |
| `GetHowTo` | "Procure a pasta azul pelo escritório e coloque-a na prateleira da recepção. Cuidado: há pastas de outras cores." |
| `GetActionPrompt` | "Aperte [E] para posicionar a pasta" |

#### Cena `SCN_FirstFloor.unity`

| GameObject | Componente | Configuração |
| :--------- | :--------- | :----------- |
| Pasta azul | `CarryableItem` | `pasta-azul-01`, `_kind = "pasta"` |
| Pasta vermelha / verde / cinza | `CarryableItem` | `pasta-vermelha-01`, `pasta-verde-01`, `pasta-cinza-01`, `_kind = "pasta"` — **fora do catálogo** |
| Prateleira da recepção | `TaskPlacementSlot` + `WorldTaskMarker` | `slot-arquivo-pasta`, `_expectedItemId = "pasta-azul-01"` |

Base de asset: `SM_Env_FileFolderOffice.blend`, com 4 materiais `MAT_` de cores diferentes
(`.agents/rules/unity-asset-management.md`).

As 4 pastas ficam em **salas diferentes**, não lado a lado. Enfileiradas na mesma estante, a
escolha é trivial e a busca some — que é justamente o que a §1 quer evitar.

Sobre o `_expectedItemId` do slot (plano 0011): ele faz o prompt só aparecer com a pasta certa na
mão, o que **contradiz** a decisão da §1 de deixar o jogador tentar e tomar `UNKNOWN_ITEM`. Para
esta tarefa, deixar `_expectedItemId` **vazio** — assim o slot aceita qualquer `pasta`, o pacote
sai, e o servidor rejeita com a mensagem explicativa. O campo continua existindo para as tarefas
em que o gate local é desejável (plano 0016).

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 12 + type place_item + nota sobre UNKNOWN_ITEM esperado)
```

### Client

```
hora-extra-client/Assets/Scripts/UI/TaskRequestPanel.cs           NEW
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs   MODIFY (EnsurePlaceItemEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs           MODIFY (TYPE_PLACE_ITEM)
hora-extra-client/Assets/Materials/MAT_Folder_Blue.mat            NEW (asset, Editor — + Red, Green, Gray)
hora-extra-client/Assets/Prefab/PFB_UI_TaskRequest.prefab         NEW (asset, Editor)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity              MODIFY (asset, Editor — 4 pastas em salas diferentes + prateleira)
hora-extra-client/Docs/Mechanics/TASK-12-POSICIONAR.md            NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo** — catálogo sobre o `pairs` do plano 0004.

```bash
cd hora-extra-backend && npm test
```

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. Solicitação aparece

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: o `TaskRequestPanel` aparece com o pedido em prosa; `MissionListHud` mostra o how-to e
  `0/1`.

### 2. Solicitação é recuperável

- Ação: fechar o painel e reabrir pelo `MissionListHud`.
- Esperado: o mesmo texto volta. Perder a solicitação não pode travar a tarefa.

### 3. Nenhuma pista no item

- Ação: procurar a pasta azul pelo cenário.
- Esperado: **nenhum** marcador, destaque ou ghost em nenhuma pasta. Marcador só na prateleira.

### 4. Pasta errada é recusada com explicação

- Ação: pegar a pasta vermelha e levar até a prateleira; pressionar [E].
- Esperado: o prompt **aparece** (o slot aceita qualquer `pasta`, §3); o pacote sai;
  `task_rejected — code=UNKNOWN_ITEM`; a pasta **volta para a mão**; a mensagem do servidor é
  exibida; contador `0/1`.

### 5. Trocar de item

- Ação: largar a vermelha e ir buscar a azul.
- Esperado: `DropCurrent` (plano 0004) devolve a vermelha ao mundo; dá para pegar a azul.

### 6. Posicionar

- Ação: encaixar a pasta azul na prateleira.
- Esperado, nesta ordem: `task_progress … itemId=pasta-azul-01 slotId=slot-arquivo-pasta` →
  `task_updated … status=completed progress=1` → **só então** a pasta sai da mão e aparece
  encaixada na pose do `_anchor`.

### 7. Pose e estabilidade

- Ação: olhar a pasta encaixada e esbarrar nela.
- Esperado: posição e rotação do `_anchor`; não cai nem se move (plano 0011).

### 8. Task concluída fecha tudo

- Ação: observar após a conclusão.
- Esperado: marcador some; o prompt da prateleira deixa de aparecer; levar outra pasta não faz
  nada.

### 9. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; nenhuma pasta presa no `_handAnchor`.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo nesta tarefa
npx tsc --noEmit
```

### Client

Seguir os 9 passos da §6 em Play Mode.

## 8. Out of scope

- Solicitação **sorteada** entre vários pares item/destino, ou gerada pelo servidor. O par é fixo
  no catálogo (§3).
- Mais de um item por solicitação — para isso existe a Tarefa 6 (plano 0016).
- Dica progressiva ("está quente/frio") ou marcador após N minutos procurando.
- Inspecionar a pasta na mão para ler uma etiqueta — o modo de rotação do plano 0008 pode ser
  ligado aqui depois, se a cor não bastar.
- Desfazer o posicionamento (limitação do plano 0011).
- Sincronizar entre jogadores o estado da prateleira.
- Validação server-side de coordenadas (o servidor valida o par, não a posição).
- Unificar `TaskRequestPanel` e `MailBriefingPanel` (plano 0017) num componente só — vale a pena,
  mas é refatoração que atravessa dois planos; fazer quando o segundo dos dois for implementado.
