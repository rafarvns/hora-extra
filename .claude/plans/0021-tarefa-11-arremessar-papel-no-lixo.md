# Plan 0021 — tarefa-11-arremessar-papel-no-lixo

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 11 — Arremessar papel no lixo**.
> **Depende de [0004](0004-infra-carregar-e-validar-itens.md)** (carregar + `items` + dedup por
> `itemId`). Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> o jogador pode coletar papéis descartáveis → ao mirar em uma lixeira, pode arremessá-los → **o
> sistema calcula distância e precisão**

É a única tarefa com **física de projétil**. Tudo o mais já existe: pegar a bolinha é
`CarryableItem` (plano 0004), e contar cada bolinha uma vez é `items` + `ALREADY_COUNTED`.

### Relação com a Tarefa 18

O plano [0006](0006-tarefa-18-limpar-recepcao-almoxarifado.md) já tem bolinhas de papel que o
jogador leva até a lixeira e guarda com [E] — o documento de entrega descreve exatamente isso
("Aperte [E] para jogar fora o papel"). Esta tarefa é a **versão à distância** da mesma ação, e as
duas convivem: são entradas de catálogo distintas, com `itemId` distintos, e a mesma lixeira pode
atender as duas (um `TaskDepositPoint` para a 18, um `ThrowTarget` para a 11).

Reusar os mesmos `itemId` nas duas tasks seria um erro: `collectedItems` é por
`playerId:taskId`, então não haveria conflito no servidor, mas o jogador veria a mesma bolinha
servindo a duas tarefas — confuso e não pedido.

### Decisão: acerto é reportado pelo cliente; "precisão" é cosmética

A física roda no cliente. O servidor não simula projétil e não vai simular — seria um motor de
física autoritativo, coisa que este projeto não tem e que nenhuma outra tarefa exige.

Então o desenho é honesto sobre o que cada lado garante:

- **Servidor garante**: que a bolinha `X` conta **uma vez só** (`ALREADY_COUNTED`), que ela
  pertence àquela task (`UNKNOWN_ITEM`), e que a task existe e está aberta. É o mesmo grau de
  confiança do QTE da cafeteira (plano 0003) e do minigame do plano 0015 — o cliente reporta o
  resultado, o servidor controla a contabilidade.
- **Cliente calcula**: trajetória, acerto, distância do arremesso e "precisão" (quão perto do
  centro da lixeira). Esses números viram **feedback visual** — nada disso vira progresso.

O documento pede que "o sistema calcule distância e precisão"; ele calcula e mostra. Torná-los
autoritativos exigiria a posição da lixeira no catálogo e a distância medida no servidor a partir
da posição autoritativa do arremessador — possível com o `zone` do plano 0010, e registrado na §8
como extensão, mas fora do escopo aqui para não arrastar uma dependência inteira por um número
decorativo.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. Catálogo sobre o `items` do plano 0004; o backend só
recebe documentação.

**Phase client** — `ThrowableItem.cs`, `PlayerThrower.cs`, `ThrowAimIndicator.cs`,
`ThrowTarget.cs`, `ThrowScorePopup.cs`; entrada de catálogo; textos; setup de cena.

### Contratos cross-repo

Nenhum evento novo.

| taskId | type | targetCount | items |
| :----- | :--- | :---------- | :---- |
| `task-arremessar-papel-01` | `throw_paper` | 4 | `bolinha-arremesso-01` … `bolinha-arremesso-04` |

O `slotId` enviado no `task_progress` é `slot-lixeira-arremesso` — vai para log e telemetria; o
servidor não o valida nesta tarefa (não há `pairs`), exatamente como na Tarefa 16.

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md`, acrescentar `throw_paper` aos valores
conhecidos de `type`, e **documentar o modelo de confiança da §1** junto com o do plano 0015 —
resultado de física/minigame é reportado pelo cliente; contabilidade é do servidor.

### Client

#### `Interactions/ThrowableItem.cs` (NEW)

Componente **ao lado** do `CarryableItem` (plano 0004), não no lugar dele: pegar continua sendo
pegar. Marca o item como arremessável e guarda o `_taskType` alvo.

#### `Interactions/PlayerThrower.cs` (NEW)

No prefab `Player`, ao lado do `PlayerCarrier`.

- Ativo só quando `PlayerCarrier.Carried` tem `ThrowableItem`.
- **Segurar** o botão de arremesso (botão direito do mouse) entra em modo de mira: mostra o
  `ThrowAimIndicator` e carrega a força (clamp entre mín. e máx., ~1,2 s para o máximo).
- **Soltar** arremessa: `carrier.Release(item)` (método do plano 0011 — se 0011 ainda não estiver
  implementado, adicioná-lo aqui, é uma linha), liga `Rigidbody` não-kinemático e aplica
  `AddForce` na direção da câmera com a força carregada.
- Cancelar a mira com [ESC] sem arremessar devolve o item à mão.
- Enquanto mira, reduzir a velocidade de caminhada — sem isso, dá para correr mirando e o
  arremesso fica trivial.

#### `Interactions/ThrowAimIndicator.cs` (NEW)

Trajetória prevista com `LineRenderer`: simula ~30 passos de `p += v*dt; v += g*dt` com a mesma
força e gravidade que o arremesso vai usar. Se a simulação e o arremesso divergirem, a mira
mente e a tarefa fica frustrante — usar **as mesmas constantes**, lidas de um único lugar.

#### `Interactions/ThrowTarget.cs` (NEW)

Na lixeira. Trigger de entrada (a boca da lixeira, não o corpo inteiro).

- `OnTriggerEnter` de um `ThrowableItem` em voo → acerto:
  - calcula `distancia` (do ponto de arremesso até a lixeira) e `precisao`
    (`1 - distânciaHorizontalAoCentro / raio`, clampeado em `[0,1]`);
  - `TaskSystemBridge.Instance.SendProgress(taskId, itemId, "slot-lixeira-arremesso")`;
  - mostra o `ThrowScorePopup` **depois** do `task_updated` — o mundo só muda com a confirmação
    do servidor, como em todo o resto do projeto;
  - em `OnTaskRejected`, o popup não aparece e a bolinha fica no chão (ver erro, abaixo).
- Erro: a bolinha bate e cai no chão. Ela continua sendo um `CarryableItem` **válido** — o jogador
  vai lá, pega de novo e tenta outra vez. Tentativas ilimitadas.

Que errar seja recuperável é o que torna a tarefa jogável: com 4 bolinhas e nenhuma segunda
chance, um arremesso ruim tornaria a task impossível.

#### `UI/ThrowScorePopup.cs` (NEW)

Texto flutuante na lixeira: *"Acertou! 6,2 m · precisão 87%"*. Puramente local (§1).

#### Catálogo e apresentação

`TaskSystemBridge.EnsureThrowPaperEntry()`.

`TaskPresentation` ganha `TYPE_THROW_PAPER`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Acertar os papéis na lixeira" |
| `GetHowTo` | "Pegue as 4 bolinhas de papel e acerte-as na lixeira. Segure o botão direito para mirar e solte para arremessar." |
| `GetActionPrompt` | "Aperte [E] para pegar a bolinha" |

#### Cena `SCN_FirstFloor.unity`

4 bolinhas com `CarryableItem` + `ThrowableItem` + `Rigidbody` (kinemático no começo),
`_kind = "bolinha-arremesso"`, espalhadas **a alguma distância** da lixeira — perto demais e não
há arremesso. A lixeira ganha `ThrowTarget` + `WorldTaskMarker`.

Uma linha no chão (decal ou material) marcando um ponto de arremesso sugerido ajuda a comunicar a
mecânica sem tutorial. Opcional.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 11 + type throw_paper + nota do modelo de confiança)
```

### Client

```
hora-extra-client/Assets/Scripts/Interactions/ThrowableItem.cs        NEW
hora-extra-client/Assets/Scripts/Interactions/PlayerThrower.cs        NEW
hora-extra-client/Assets/Scripts/Interactions/ThrowAimIndicator.cs    NEW
hora-extra-client/Assets/Scripts/Interactions/ThrowTarget.cs          NEW
hora-extra-client/Assets/Scripts/UI/ThrowScorePopup.cs                NEW
hora-extra-client/Assets/Scripts/Interactions/PlayerCarrier.cs        MODIFY (Release — se o plano 0011 não tiver sido implementado ainda)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs       MODIFY (EnsureThrowPaperEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs               MODIFY (TYPE_THROW_PAPER)
hora-extra-client/Assets/Prefab/Player.prefab                         MODIFY (asset, Editor — PlayerThrower + LineRenderer de mira)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                  MODIFY (asset, Editor — 4 bolinhas + lixeira com ThrowTarget)
hora-extra-client/Docs/Mechanics/TASK-11-ARREMESSO.md                 NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo.** Esta tarefa não adiciona lógica de servidor — é catálogo sobre o
`items` do plano 0004.

```bash
cd hora-extra-backend && npm test
```

Se aparecer necessidade de lógica nova no servidor (ex.: validar distância), **parar**: isso é a
extensão listada na §8 e exige o `zone` do plano 0010 — não improvisar um campo novo.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. Estado inicial

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: how-to da §3 e `0/4`; `WorldTaskMarker` na lixeira; as 4 bolinhas no chão, paradas
  (kinemáticas).

### 2. Pegar

- Ação: [E] numa bolinha.
- Esperado: vai para a mão; nenhum `Rigidbody` acordado; nenhum pacote enviado (pegar não conta).

### 3. Mirar

- Ação: segurar o botão direito.
- Esperado: a trajetória aparece e **cresce** enquanto segura; a velocidade de caminhada cai;
  soltar o botão arremessa.

### 4. A mira não mente

- Ação: mirar apontando para uma marca no chão e arremessar.
- Esperado: a bolinha cai **onde a linha terminava**, com pequena margem. Se divergir muito, a
  simulação e o `AddForce` usam constantes diferentes (§3) — corrigir antes de seguir.

### 5. Cancelar a mira

- Ação: mirar e apertar [ESC].
- Esperado: a bolinha volta para a mão; nada é arremessado; velocidade normaliza.

### 6. Errar é recuperável

- Ação: arremessar de propósito para o lado.
- Esperado: a bolinha cai no chão com física; **nenhum** pacote enviado; nenhum popup; dá para ir
  lá, pegar de novo com [E] e arremessar outra vez.

### 7. Acertar

- Ação: acertar a lixeira.
- Esperado, nesta ordem: `task_progress … itemId=bolinha-arremesso-01 slotId=slot-lixeira-arremesso`
  → `task_updated … 1/4` → **só então** o popup com distância e precisão.

### 8. Precisão varia

- Ação: acertar de perto e de longe.
- Esperado: a distância no popup corresponde ao arremesso; a precisão é maior no acerto mais
  central. São números locais (§1) — conferir que fazem sentido, não que o servidor os conhece.

### 9. Mesma bolinha não conta duas vezes

- Ação: tirar a bolinha da lixeira pelo Inspector, pegar e acertar de novo.
- Esperado: `task_rejected — code=ALREADY_COUNTED`; **nenhum** popup; contador inalterado.

### 10. Concluir

- Ação: acertar as 4.
- Esperado: `status=completed progress=4`; marcador some; arremessos seguintes não contam.

### 11. Não conflita com a Tarefa 18

- Ação: se o plano 0006 já estiver implementado, conferir que as bolinhas daquela tarefa
  continuam funcionando com [E] na lixeira.
- Esperado: os dois fluxos convivem; `itemId` distintos; nenhum prompt duplicado na lixeira.

### 12. Cleanup

- Ação: sair do Play Mode.
- Esperado: nenhum `LogError`; nenhuma bolinha presa no `_handAnchor`; nenhum `LineRenderer`
  visível.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo nesta tarefa
npx tsc --noEmit
```

### Client

Seguir os 12 passos da §6 em Play Mode.

## 8. Out of scope

- **Distância e precisão autoritativas.** Extensão possível: colocar a posição da lixeira no
  catálogo (`zone`, plano 0010) e medir no servidor a partir da posição autoritativa do
  arremessador. Fora daqui para não arrastar a dependência do plano 0010 por um número
  decorativo (§1).
- Anti-cheat do acerto (o cliente reporta — mesmo modelo dos planos 0003 e 0015).
- Pontuação acumulada, ranking, combo, "cesta de 3 pontos".
- Ricochete em parede contando como acerto especial.
- Outro jogador ver a bolinha voando (não há sincronização de projétil).
- Acertar outro jogador, ou dano/incômodo por arremesso.
- Bolinhas infinitas ou respawn — são 4 e ficam no chão ao errar.
- Arremessar qualquer outro objeto carregável (só quem tem `ThrowableItem`).
- Animação de arremesso e som.
- Mira com controle/gamepad.
