# Plan 0017 — tarefa-07-escrever-e-enviar-email

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 7 — Escrever e enviar e-mail**.
> **Depende de [0009](0009-infra-terminal-de-computador.md)**; reusa o `TerminalMailApp` do plano
> [0014](0014-tarefa-04-enviar-arquivos-e-emails.md) ligando o `_composeMode`.
> Mapa das 18 tarefas: [README.md](README.md).

## 1. Context

O documento:

> o jogador deve acessar um computador → recebe uma **solicitação contendo informações
> específicas** → precisa preencher os **campos corretos** do e-mail (destinatário, assunto e
> conteúdo) → após a revisão, o e-mail deve ser enviado

É a Tarefa 4 com o miolo invertido. Lá o e-mail vinha pronto e o desafio era *descobrir a senha*;
aqui não há senha e o desafio é **transcrever com atenção**: ler a solicitação e preencher três
campos que têm resposta certa.

Três campos com resposta certa = três etapas com `expects` (plano 0009), mais a etapa de envio.
Nada de protocolo novo.

O que este plano precisa decidir bem são duas coisas de desenho:

**Onde mora a solicitação.** O texto do pedido ("mande para `financeiro@horizon.com` com assunto
`Reembolso` …") é **conteúdo de UI**, não protocolo: o catálogo carrega só os `expects`. Se a
solicitação viesse do servidor, seria duplicar a mesma informação em dois formatos — e um deles
ficaria desatualizado na primeira mudança de texto.

**Quando validar.** O documento diz *"após a revisão, o e-mail deve ser enviado"*. Duas leituras:
validar campo a campo (feedback imediato) ou tudo no envio (revisão de verdade). Este plano adota
**campo a campo**, por ser o que o modelo de `steps` já faz: cada campo é uma etapa, confirmar
um campo errado dá `WRONG_VALUE` retentável, e o botão Enviar só passa quando os três estiverem
cumpridos (`STEP_OUT_OF_ORDER` caso contrário). A "revisão" vira a conferência visual do e-mail
montado antes de clicar em Enviar. A alternativa — mandar os três valores juntos no envio —
exigiria um payload com múltiplos valores, que `task_step_submit` não tem e não vale criar.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. As quatro etapas são **dados de catálogo** sobre o
`submitStep` do plano 0009.

**Phase client** — `TerminalMailApp` ganha o modo de composição (`_composeMode = true`);
`MailBriefingPanel.cs` (a solicitação); entrada de catálogo; textos; setup de cena.

### Contratos cross-repo

Nenhum evento novo.

| taskId | type | steps (em ordem) |
| :----- | :--- | :--------------- |
| `task-escrever-email-01` | `compose_email` | `destinatario` (`expects: "financeiro@horizon.com"`) · `assunto` (`expects: "reembolso de viagem"`) · `conteudo` (`expects: "segue o comprovante em anexo"`) · `enviar` |

`targetCount` = 4. A ordem do array é a ordem exigida — preencher o assunto antes do destinatário
dá `STEP_OUT_OF_ORDER`. Ver a nota de usabilidade na §3.

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md` e acrescentar `compose_email` aos
valores conhecidos de `type`.

Vale destacar no doc a dependência desta tarefa da **normalização** do plano 0009
(`trim().toLowerCase()`): sem ela, `Reembolso de Viagem` seria recusado e a tarefa viraria um
exercício de digitação exata. Se alguém mexer na normalização depois, esta tarefa quebra — por
isso a regra é contrato documentado, não detalhe de implementação.

### Client

#### `UI/Terminal/TerminalMailApp.cs` (MODIFY — liga o `_composeMode`)

O gancho foi declarado no plano 0014 justamente para isto. Com `_composeMode = true`:

- os três campos viram `InputField` editáveis, cada um com um botão "Confirmar" ao lado que chama
  `Submit(stepId, campo.text)`;
- campo confirmado com sucesso fica **travado e marcado com ✓** (o servidor já não aceitaria de
  novo — `STEP_ALREADY_DONE` — e deixar editável convida ao erro);
- `WRONG_VALUE` → marca o campo em vermelho com a mensagem do servidor, **mantém o texto digitado**
  (diferente da senha do plano 0014, que limpa: aqui o jogador quase sempre errou por um detalhe,
  e apagar tudo é punitivo);
- o botão Enviar fica sempre visível; clicar cedo dá `STEP_OUT_OF_ORDER` com a mensagem do
  servidor — mesmo critério do plano 0012.

**Nota de usabilidade sobre a ordem.** `steps` é uma lista ordenada e o servidor exige a ordem.
Para o jogador não tomar `STEP_OUT_OF_ORDER` por navegar livremente entre os campos, a tela
**destaca o campo atual** (vindo do `nextStepId` do último ack) e desabilita visualmente os
seguintes. O gate é de UI; a autoridade continua no servidor.

#### `UI/Terminal/MailBriefingPanel.cs` (NEW)

A "solicitação". Painel lateral na tela do e-mail, sempre visível durante a composição, com o
texto do pedido — deliberadamente redigido em **prosa**, não em formulário:

> *"Oi! Preciso que você mande um e-mail pro pessoal do financeiro (financeiro@horizon.com) com o
> assunto Reembolso de viagem. No corpo escreve só: Segue o comprovante em anexo. Valeu!"*

Escrever em prosa é o que transforma a tarefa em leitura atenta em vez de copiar-colar de campos
rotulados. O texto fica num `[SerializeField, TextArea]`, não no catálogo — ver §1.

#### Catálogo e apresentação

`TaskSystemBridge.EnsureComposeEmailEntry()`.

`TaskPresentation` ganha `TYPE_COMPOSE_EMAIL`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Escrever e enviar um e-mail" |
| `GetHowTo` | "Vá até um computador, leia a solicitação e preencha o e-mail com o destinatário, o assunto e o conteúdo pedidos. Revise antes de enviar." |
| `GetActionPrompt` | "Aperte [E] para usar o computador" |

#### Cena `SCN_FirstFloor.unity`

Um `ComputerTerminal` (`_terminalId = "pc-financeiro"`, `_taskType = "compose_email"`) com o
`TerminalMailApp` em `_composeMode` + `MailBriefingPanel`; `WorldTaskMarker` no PC. **Sem** tela
de login — esta tarefa não tem senha.

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 7 + type compose_email + destaque da dependência de normalize)
```

### Client

```
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalMailApp.cs       MODIFY (implementa o _composeMode declarado no plano 0014)
hora-extra-client/Assets/Scripts/UI/Terminal/MailBriefingPanel.cs     NEW
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs       MODIFY (EnsureComposeEmailEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs               MODIFY (TYPE_COMPOSE_EMAIL)
hora-extra-client/Assets/Prefab/PFB_UI_MailApp.prefab                 MODIFY (asset, Editor — variante de composição; criado no plano 0014)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                  MODIFY (asset, Editor — PC do financeiro)
hora-extra-client/Docs/Mechanics/TASK-07-COMPOR-EMAIL.md              NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo** — catálogo sobre o `submitStep` do plano 0009.

```bash
cd hora-extra-backend && npm test
```

Regressão relevante: o ciclo de normalização (`Cycle 9` do plano 0009) é o que sustenta esta
tarefa. Se ele estiver falhando, **parar** — não adianta testar em Play Mode.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. Estado inicial

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: how-to da §3 e `0/4`; `WorldTaskMarker` no PC.

### 2. A solicitação está legível

- Ação: [E] no PC.
- Esperado: a tela abre direto na composição (sem login); o `MailBriefingPanel` mostra o pedido em
  prosa; o campo **destinatário** está destacado e os outros dois, esmaecidos.

### 3. Fora de ordem

- Ação: tentar confirmar o **assunto** primeiro.
- Esperado: o gate de UI não deixa. Forçando o envio pelo Inspector:
  `task_rejected — code=STEP_OUT_OF_ORDER`; contador `0/4`.

### 4. Valor errado mantém o texto

- Ação: no destinatário, digitar `financeiro@horizonte.com` e confirmar.
- Esperado: `task_rejected — code=WRONG_VALUE`; campo em vermelho com a mensagem do servidor; **o
  texto digitado continua lá** para ser corrigido; contador `0/4`.

### 5. Destinatário correto

- Ação: corrigir para `financeiro@horizon.com`.
- Esperado: ack → `1/4`; campo travado com ✓; o destaque passa para o assunto.

### 6. Normalização

- Ação: no assunto, digitar `  Reembolso De Viagem  `.
- Esperado: **aceito** → `2/4`. Se recusar, a normalização do servidor não está ativa — é bloqueio
  para esta tarefa inteira.

### 7. Conteúdo

- Ação: preencher o conteúdo conforme a solicitação.
- Esperado: ack → `3/4`; botão Enviar deixa de ser o único não cumprido.

### 8. Campo travado não reenvia

- Ação: tentar editar e reconfirmar o destinatário.
- Esperado: o campo está travado. Forçando o envio: `task_rejected — code=STEP_ALREADY_DONE`.

### 9. Enviar

- Ação: revisar o e-mail montado e clicar em Enviar.
- Esperado: `stepId=enviar` → ack `nextStepId=null` → `status=completed progress=4`; confirmação
  na tela **depois** do ack; marcador do PC some.

### 10. Sair e voltar

- Ação: reiniciar, preencher só o destinatário, ESC, andar, voltar.
- Esperado: a tela reabre com o destinatário já ✓ e o destaque no assunto (estado vindo do
  `nextStepId`, não de campo local); contador `1/4`.

### 11. Cleanup

- Ação: sair do Play Mode com a tela aberta e um campo pela metade.
- Esperado: nenhum `LogError`; cursor normal no Editor.

## 7. Verification commands

### Backend

```bash
cd hora-extra-backend
npm test          # regressão — nenhum teste novo nesta tarefa
npx tsc --noEmit
```

### Client

Seguir os 11 passos da §6 em Play Mode.

## 8. Out of scope

- **Validação só no envio** (revisão como gate único) — ver a decisão na §1.
- Aceitar mais de uma redação correta para o conteúdo. `expects` é um valor só (limitação do plano
  0009); por isso o conteúdo pedido é uma frase curta e literal, e não um texto livre.
- Solicitação gerada dinamicamente, sorteada entre variantes, ou vinda do servidor.
- Anexar arquivo nesta tarefa (é a Tarefa 2/4).
- Corretor ortográfico, formatação, negrito, ou múltiplos destinatários.
- Caixa de entrada com outros e-mails, responder/encaminhar.
- E-mail chegando de verdade para outro jogador.
- Penalidade ou limite de tentativas por errar um campo.
- Ordem livre entre os três campos — o modelo de `steps` é linear; o destaque de campo atual é a
  mitigação de UX, não uma mudança de regra.
