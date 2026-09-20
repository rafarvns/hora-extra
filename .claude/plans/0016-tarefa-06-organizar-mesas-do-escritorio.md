# Plan 0016 — tarefa-06-organizar-mesas-do-escritorio

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 6 — Organizar as mesas do escritório**.
> **Depende de [0004](0004-infra-carregar-e-validar-itens.md)** (carregar/entregar + `pairs`) e de
> [0011](0011-infra-slot-de-posicionamento.md) (`TaskPlacementSlot` — o item **fica** no lugar em
> vez de sumir). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento é explícito sobre o critério de conclusão:

> algumas mesas estão desorganizadas → o jogador deve posicionar corretamente itens como
> documentos, canetas, grampeadores e pastas → **cada item possui uma posição-alvo definida** →
> a tarefa é concluída quando todos os objetos estiverem **nos locais corretos**

É a tarefa que motivou o plano 0011. A diferença para a Tarefa 16 (plano 0005) é só visual, mas é
o ponto inteiro: lá o papel entra na caixa e some; aqui o grampeador tem que **ficar visível** no
canto da mesa. Uma mesa "organizada" com os objetos desativados é uma mesa vazia.

No protocolo, nada é novo: `pairs` (plano 0004) já modela `item → posição-alvo`, e o servidor já
rejeita `WRONG_SLOT` e `ALREADY_COUNTED`. Toda a entrega é catálogo + cena sobre o
`TaskPlacementSlot` do plano 0011.

Esta é também a primeira tarefa em que o **mesmo tipo de objeto aparece em mais de uma mesa**
(duas canetas, duas pastas). Como `pairs` é um par exato `itemId → slotId`, cada caneta tem a sua
mesa: trocar as duas de lugar é erro, e o servidor pega. Isso é mais estrito do que o documento
exige — ver a decisão abaixo.

### Decisão: par exato, não par por categoria

A leitura frouxa seria "qualquer caneta serve em qualquer porta-canetas". A leitura estrita é
"a caneta A vai na mesa 1". Este plano adota a **estrita**, por dois motivos: é o que `pairs` já
faz sem código novo, e é o que a frase "cada item possui uma **posição-alvo definida**" diz.

O custo é de autoria: o `_expectedItemId` do slot e o `pairs` do catálogo têm que bater
exatamente, item por item. Um par trocado só aparece em Play Mode, como um `WRONG_SLOT` que o
jogador não entende. Por isso a §6 tem um passo dedicado a conferir a tabela inteira.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. Catálogo sobre o `pairs` do plano 0004; o backend só
recebe documentação.

**Phase client** — entrada de catálogo no `TaskSystemBridge`; textos em `TaskPresentation`; setup
de cena com 8 itens (`CarryableItem`) e 8 `TaskPlacementSlot` distribuídos em 3 mesas.

### Contratos cross-repo

Nenhum evento novo. A entrada usa `pairs`, já documentado pelo plano 0004:

| taskId | type | targetCount | pairs |
| :----- | :--- | :---------- | :---- |
| `task-organizar-mesas-01` | `organize_desks` | 8 | 8 pares `itemId → slotId` (tabela na §3) |

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md` como **exemplo de `pairs` com
posição-alvo** (contraposto ao exemplo de recipiente da Tarefa 16), e acrescentar `organize_desks`
aos valores conhecidos de `type`.

### Client

#### Tabela item → slot (fonte da verdade da autoria)

| # | `_itemId` | `_kind` | Mesa | `_slotId` |
| :- | :-------- | :------ | :--- | :-------- |
| 1 | `mesa1-documento` | `documento` | 1 | `slot-mesa1-documento` |
| 2 | `mesa1-caneta` | `caneta` | 1 | `slot-mesa1-caneta` |
| 3 | `mesa1-grampeador` | `grampeador` | 1 | `slot-mesa1-grampeador` |
| 4 | `mesa2-pasta` | `pasta` | 2 | `slot-mesa2-pasta` |
| 5 | `mesa2-caneta` | `caneta` | 2 | `slot-mesa2-caneta` |
| 6 | `mesa2-documento` | `documento` | 2 | `slot-mesa2-documento` |
| 7 | `mesa3-pasta` | `pasta` | 3 | `slot-mesa3-pasta` |
| 8 | `mesa3-grampeador` | `grampeador` | 3 | `slot-mesa3-grampeador` |

O prefixo `mesaN-` nos ids não é estética: com 8 pares, é o que deixa um erro de pareamento
visível a olho na tabela do catálogo. Os itens **não** ficam nas suas mesas no começo — ficam
espalhados/trocados, senão não há o que organizar.

Assets existentes reutilizáveis: `SM_Env_PaperOffice.blend` (documento),
`SM_Env_FileFolderOffice.blend` (pasta), e as canetas já instanciadas na cena
(`Caneta`, `Caneta2`, `Caneta3`). Grampeador não existe em `arte/3D_Models/` — abrir pedido de
asset ou substituir por um dos existentes; ver §8.

#### Catálogo e apresentação

`TaskSystemBridge.EnsureOrganizeDesksEntry()`, no padrão dos `Ensure*` existentes.

`TaskPresentation` ganha `TYPE_ORGANIZE_DESKS`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Organizar as mesas do escritório" |
| `GetHowTo` | "As mesas estão bagunçadas. Pegue cada objeto e coloque-o no lugar certo da mesa a que ele pertence." |
| `GetActionPrompt` | "Aperte [E] para posicionar (X/8)" |

#### Cena `SCN_FirstFloor.unity`

- 8 `CarryableItem` com `_isTool = false`, espalhados nas mesas erradas e pelo chão.
- 8 `TaskPlacementSlot` (plano 0011), cada um com `_anchor` posicionado **e rotacionado** na pose
  final do objeto. A rotação importa tanto quanto a posição: uma caneta encaixada de pé na mesa
  parece bug, não organização.
- `SlotHighlight` (plano 0007) em todos os slots, para o jogador identificar "os objetos que estão
  fora do lugar" — que é o primeiro passo pedido pelo documento.
- `TaskPlacementGhost` (plano 0011) em todos, com o prefab do item esperado.
- `WorldTaskMarker` **uma vez por mesa**, não por slot — 8 marcadores na mesma sala viram ruído.

#### Nota de autoria

`_expectedItemId` (cena) e `pairs` (catálogo) são a **mesma informação em dois lugares**, e é o
risco central deste plano. Conferir o par inteiro antes do Play Mode. Se o plano 0008 tiver
implementado o script de conferência cena↔catálogo sugerido na §3 dele, rodá-lo aqui — este é
exatamente o caso de uso que o justifica.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 6 + type organize_desks)
```

### Client

```
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs     MODIFY (EnsureOrganizeDesksEntry com os 8 pairs)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs             MODIFY (TYPE_ORGANIZE_DESKS)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                MODIFY (asset, Editor — 8 CarryableItem + 8 TaskPlacementSlot + anchors + 3 markers)
hora-extra-client/Docs/Mechanics/TASK-06-MESAS.md                   NEW (inclui a tabela item→slot como referência de autoria)
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo.** Esta tarefa não adiciona lógica de servidor — é catálogo sobre a
validação de `pairs` já coberta pelos ciclos do plano 0004.

```bash
cd hora-extra-backend && npm test
```

Se aparecer necessidade de lógica nova no servidor, **parar** e abrir ciclo TDD.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. Conferência da tabela (antes do Play Mode)

- Ação: comparar, item por item, o `_itemId`/`_slotId` da cena com o `pairs` do catálogo.
- Esperado: os 8 pares batem exatamente. **Este passo vem primeiro de propósito** — é o erro mais
  provável deste plano e o mais confuso de diagnosticar depois.

### 2. Estado inicial

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: how-to da §3 e `0/8`; os 8 slots com `SlotHighlight` ligado; nenhum ghost visível;
  marcadores nas 3 mesas.

### 3. Ghost aparece com o item na mão

- Ação: pegar `mesa1-caneta`.
- Esperado: ghost só em `slot-mesa1-caneta`; `slot-mesa2-caneta` continua só com destaque.

### 4. Mesa errada não aceita

- Ação: levar `mesa1-caneta` até `slot-mesa2-caneta`.
- Esperado: prompt não aparece. Forçando o envio: `task_rejected — code=WRONG_SLOT`; o item volta
  para a mão; contador não muda. Este é o teste da decisão "par exato" da §1.

### 5. Posicionar

- Ação: encaixar `mesa1-caneta` no slot certo.
- Esperado, nesta ordem: `task_progress … itemId=mesa1-caneta slotId=slot-mesa1-caneta` →
  `task_updated … 1/8` → **só então** o item sai da mão e aparece encaixado.

### 6. Pose correta

- Ação: olhar o item encaixado de perto e de vários ângulos.
- Esperado: posição **e rotação** do `_anchor`; o objeto parece apoiado na mesa, não flutuando
  nem atravessando o tampo. Repetir para cada tipo (caneta deitada, pasta em pé, documento plano).

### 7. Item fica parado

- Ação: correr e esbarrar nos itens já posicionados.
- Esperado: nada se move nem cai (`Collider` off, `Rigidbody.isKinematic`, plano 0011).

### 8. Slot ocupado

- Ação: levar outro item do mesmo `_kind` até um slot já preenchido.
- Esperado: prompt não aparece; destaque e ghost do slot continuam desligados.

### 9. Concluir

- Ação: posicionar os 8.
- Esperado: `status=completed progress=8`; **as 3 mesas visivelmente organizadas**; todos os
  destaques e ghosts desligados; marcadores somem.

### 10. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; nenhum ghost órfão; nenhum item preso no `_handAnchor`.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo nesta tarefa
npx tsc --noEmit
```

### Client

Seguir os 10 passos da §6 em Play Mode.

## 8. Out of scope

- **Asset de grampeador** — não existe em `arte/3D_Models/`. Até existir, usar um placeholder ou
  substituir o item 3/8 por outro já modelado. Isso muda a tabela da §3 e o `targetCount` se for
  removido em vez de substituído.
- Pareamento **por categoria** em vez de par exato (ver a decisão na §1).
- Bagunçar as mesas dinamicamente, ou posição inicial aleatória dos itens.
- Desfazer um posicionamento (limitação do plano 0011 — não há decremento no servidor).
- Física de apoio real: o encaixe é por `_anchor` autorado, não por colisão com o tampo.
- Sincronizar entre jogadores o estado visual das mesas (limitação geral do plano 0004).
- Validação server-side de coordenadas: o servidor valida o par, não a posição.
- Animação e som de posicionar.
- Agrupar as 3 mesas como sub-tarefas independentes — é **uma** entrada de catálogo com
  `targetCount = 8`, para não inflar demais o sorteio de 3 de N.
