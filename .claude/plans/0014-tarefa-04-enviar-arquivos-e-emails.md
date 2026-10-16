# Plan 0014 — tarefa-04-enviar-arquivos-e-emails

> Origem: `TAREFAS DOS JOGADORES.pdf` — **Tarefa 4 — Enviar arquivos/e-mails pelo computador**.
> **Depende de [0009](0009-infra-terminal-de-computador.md)**. Mapa das 18 tarefas:
> [README.md](README.md).

## 1. Context

O documento descreve quatro passos:

> interagir com computador → tela pede senha → **jogador precisa descobrir a senha (bilhete,
> post-it)** → após login: clicar no ícone do e-mail e clicar no botão de enviar

É a tarefa mais simples do eixo "computador" — três etapas lineares, uma delas com valor
esperado — e por isso é a **primeira que deve ser implementada depois do plano 0009**: ela
exercita o caminho completo (`expects`, `WRONG_VALUE` retentável, ordem obrigatória, sair e
voltar) sem nenhuma mecânica auxiliar. Se algo estiver errado no shell, aparece aqui, antes dos
planos 0015 e 0017 construírem em cima.

O miolo de gameplay é **descobrir a senha fora da tela**. Isso significa que a tarefa tem duas
metades que não conversam pelo protocolo: a busca pelo post-it é exploração puramente local, e só
a submissão da senha vira pacote. Nada a acrescentar no servidor — mas é a razão pela qual
"fechar a tela não cancela a task" (plano 0009, `TerminalScreen`) é requisito e não conveniência:
o jogador **precisa** poder sair, procurar e voltar sem perder o que já fez.

Diferença para a Tarefa 7 (plano 0017): aqui o jogador **envia um e-mail já pronto** (dois
cliques); lá ele **preenche** destinatário, assunto e conteúdo. As duas compartilham a tela de
e-mail, e por isso este plano entrega o `TerminalMailApp` num formato que o plano 0017 estende em
vez de duplicar.

## 2. Scope & target

**Target:** `both`

**Phase backend** — nenhuma lógica nova. As três etapas são **dados de catálogo** sobre o
`submitStep` do plano 0009. O backend só recebe documentação.

**Phase client** — `TerminalLoginApp.cs` e `TerminalMailApp.cs` (herdam de `TerminalApp`);
`SecretNote.cs` (o post-it); entrada de catálogo; textos em `TaskPresentation`; setup de cena.

### Contratos cross-repo

Nenhum evento novo.

| taskId | type | steps (em ordem) |
| :----- | :--- | :--------------- |
| `task-enviar-arquivo-01` | `send_file_email` | `login` (`expects: "horizon2025"`) · `abrir-email` · `enviar` |

`targetCount` = 3.

## 3. Approach

### Backend

Só documentação: registrar a entrada em `COMMUNICATION.md` como **exemplo canônico de etapa com
`expects`**, e acrescentar `send_file_email` à lista de valores conhecidos de `type`.

Vale repetir no doc o que o plano 0009 garante e o que não garante: o servidor impede login
pulado, login repetido e senha errada; **não** impede um cliente modificado de ler `expects` no
próprio catálogo que ele mesmo registrou.

### Client

#### `UI/Terminal/TerminalLoginApp.cs` (NEW)

Tela de bloqueio. `InputField` de senha (`contentType = Password`) + botão Entrar.

- Confirmar → `Submit("login", campo.text)`.
- `WRONG_VALUE` → mensagem "Senha incorreta", campo limpo, **tela aberta**. Tentativas
  ilimitadas (plano 0009).
- `task_step_ack` de `login` → troca para a área de trabalho.
- `STEP_ALREADY_DONE` no ack de reabertura → o `TerminalScreen` já abre direto na área de
  trabalho; o login não reaparece para quem já entrou. O estado vem do `nextStepId` do último
  ack, nunca de um `bool` local.

#### `UI/Terminal/TerminalMailApp.cs` (NEW)

Área de trabalho com ícones + cliente de e-mail:

- ícone "E-mail" → `TerminalStepButton` com `_stepId = "abrir-email"` (nenhum script novo — a cola
  de Inspector do plano 0009 resolve);
- dentro do e-mail, uma mensagem já redigida, com anexo, e um botão Enviar → `_stepId = "enviar"`.
- `[SerializeField] private bool _composeMode = false` — desligado aqui; é o ponto de extensão
  que o plano 0017 liga para reusar esta mesma tela com campos preenchíveis. Deixar o gancho
  declarado (e sem uso) é mais barato agora do que refatorar a tela depois.

#### `Interactions/SecretNote.cs` (NEW)

O post-it. Trigger + `InteractionPrompt` (plano 0004) exibindo o texto anotado.

- `[SerializeField, TextArea] private string _noteText` — ex.: `"Senha do PC: horizon2025"`;
- `[SerializeField] private string _revealForTaskType` — o bilhete só mostra o conteúdo se a task
  do tipo estiver ativa. Sem esse gate, o jogador lê a senha antes de receber a tarefa e a etapa
  de descoberta some.
- **Nenhuma rede.** É objeto local; o servidor não sabe que o post-it existe.

Componente genérico de propósito: o plano 0013 tem um bilhete equivalente para a senha da gaveta.
Se o 0013 for implementado antes, reusar o `SecretNote` de lá em vez de criar outro.

#### Catálogo e apresentação

`TaskSystemBridge.EnsureSendFileEmailEntry()`, no padrão dos `Ensure*` existentes.

`TaskPresentation` ganha `TYPE_SEND_FILE_EMAIL`:

| Método | Texto |
| :----- | :---- |
| `GetTitle` | "Enviar o arquivo por e-mail" |
| `GetHowTo` | "Use o computador do escritório. A tela pede senha — procure pelo escritório, alguém anotou num post-it. Depois abra o e-mail e clique em enviar." |
| `GetActionPrompt` | "Aperte [E] para usar o computador" |

#### Cena `SCN_FirstFloor.unity`

Um `ComputerTerminal` (`_terminalId = "pc-comercial"`, `_taskType = "send_file_email"`) com
`TerminalLoginApp` + `TerminalMailApp` no `TerminalScreen`; um `SecretNote` **em outra sala**, não
na mesma mesa do PC — a distância é o gameplay. `WorldTaskMarker` no PC; **nenhum marcador no
post-it** (achar é a tarefa).

## 4. Files to change

### Backend

```
hora-extra-backend/docs/Networking/COMMUNICATION.md    MODIFY (entrada de catálogo da Tarefa 4 + type send_file_email)
```

### Client

```
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalLoginApp.cs     NEW
hora-extra-client/Assets/Scripts/UI/Terminal/TerminalMailApp.cs      NEW (com o gancho _composeMode que o plano 0017 liga)
hora-extra-client/Assets/Scripts/Interactions/SecretNote.cs          NEW (genérico; compartilhado com o plano 0013)
hora-extra-client/Assets/Scripts/Characters/TaskSystemBridge.cs      MODIFY (EnsureSendFileEmailEntry)
hora-extra-client/Assets/Scripts/UI/TaskPresentation.cs              MODIFY (TYPE_SEND_FILE_EMAIL)
hora-extra-client/Assets/Prefab/PFB_UI_MailApp.prefab                NEW (asset, Editor)
hora-extra-client/Assets/Scenes/SCN_FirstFloor.unity                 MODIFY (asset, Editor — PC comercial + post-it em outra sala)
hora-extra-client/Docs/Mechanics/TASK-04-ENVIAR-EMAIL.md             NEW
```

## 5. TDD breakdown (phase: backend)

**Nenhum ciclo TDD novo** — catálogo sobre o `submitStep` já coberto pelo plano 0009.

```bash
cd hora-extra-backend && npm test
```

Se aparecer necessidade de lógica nova no servidor, **parar** e abrir ciclo TDD.

## 6. Manual verification steps (phase: client)

Pré-condição: backend rodando, `SCN_FirstFloor.unity` aberta, `UseTestToken = true`,
"Clear on Play".

### 1. Estado inicial

- Ação: entrar em Play Mode com a task sorteada.
- Esperado: how-to da §3 e `0/3`; `WorldTaskMarker` no PC; o post-it **não** tem marcador.

### 2. Abrir o PC cai no login

- Ação: [E] no computador.
- Esperado: a tela abre no `TerminalLoginApp`, não na área de trabalho. Cursor destrava, câmera e
  movimento travam.

### 3. Pular o login não funciona

- Ação: forçar pelo Inspector o envio de `abrir-email`.
- Esperado: `task_rejected — code=STEP_OUT_OF_ORDER`; contador `0/3`.

### 4. Senha errada é retentável

- Ação: digitar `123456`.
- Esperado: `task_rejected — code=WRONG_VALUE`; "Senha incorreta"; campo limpo; **tela continua
  aberta**; contador `0/3`. Repetir 3× e confirmar que nada trava nem bloqueia.

### 5. Senha não vaza

- Ação: procurar `horizon2025` e `123456` no Console e em `hora-extra-backend/logs/`.
- Esperado: nenhuma ocorrência vinda do fluxo de etapa.

### 6. O post-it só revela com a task ativa

- Ação: achar o post-it **sem** a task atribuída (desmarcar `_autoRequestTasksOnConnect`), depois
  com ela.
- Esperado: sem a task, nada é revelado; com a task, o prompt mostra "Senha do PC: horizon2025".

### 7. Login

- Ação: digitar `horizon2025`.
- Esperado: `task_step_ack … stepId=login nextStepId=abrir-email` → `1/3`; a tela troca para a
  área de trabalho **depois** do ack.

### 8. Caixa alta e espaço

- Ação: reiniciar e digitar `  HORIZON2025 `.
- Esperado: aceito (normalização do plano 0009). Se recusar, a normalização não está no servidor.

### 9. Concluir

- Ação: clicar no ícone de e-mail e em Enviar.
- Esperado: `2/3` → `3/3` com `nextStepId=null` e `status=completed`; marcador do PC some.

### 10. Sair e voltar já logado

- Ação: reiniciar, fazer só o login, ESC, andar, voltar ao PC.
- Esperado: a tela abre **direto na área de trabalho** (vindo do `nextStepId`), não no login;
  contador `1/3`.

### 11. Repetir etapa

- Ação: tentar enviar duas vezes.
- Esperado: `task_rejected — code=STEP_ALREADY_DONE`.

### 12. Cleanup

- Ação: sair do Play Mode com a tela aberta.
- Esperado: nenhum `LogError`; cursor normal no Editor.

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

- **Senha gerada pelo servidor** (ver "Nota de autoridade" do plano 0009) e senha por partida.
- Escolher **qual** arquivo anexar — aqui o e-mail já vem pronto; selecionar arquivo é a Tarefa 2
  (plano 0012) e preencher campos é a Tarefa 7 (plano 0017).
- Preenchimento de destinatário/assunto/conteúdo — plano 0017, que liga o `_composeMode`.
- Limite de tentativas, bloqueio de conta ou cooldown após erros.
- Mais de um post-it, pista em várias partes, ou senha que muda de lugar.
- Sistema de e-mail real entre jogadores.
- Animação de boot do PC, protetor de tela, som de teclado.
- Outro jogador ver a tela do colega, ou dois jogadores no mesmo PC.
- Validação de proximidade pelo servidor (limitação geral dos planos 0004 e 0009).
