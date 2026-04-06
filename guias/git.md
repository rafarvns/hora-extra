# 🎮 Guia de Git: Hora-Extra

Bem-vindo ao time! Este guia foi feito para que todos consigam usar o Git sem medo, mesmo que seja a sua primeira vez mexendo em código ou ativos de jogo.

---

## 🔥 O que é o Git?
Imagine o Git como um **"Save Game"** gigante. Se você fizer algo errado e o projeto parar de funcionar, você pode simplesmente "voltar no tempo" para o último save que estava funcionando.

---

## 🗺️ O Plano: Como organizamos as salas (Branches)

No nosso projeto, temos três tipos principais de "salas":

1.  **`main` (A Sala do Diretor)**: Onde fica a versão final do jogo. Só mexemos aqui quando terminamos uma grande etapa!
2.  **`develop` (A Oficina)**: Onde tudo acontece. É aqui que juntamos as peças do quebra-cabeça.
3.  **`feature/*` ou `bugfix/*` (Sua Mesa Particular)**: Onde você faz o seu trabalho sem atrapalhar ninguém.

---

## 🛠️ O Fluxo de Trabalho (Passo a Passo)

### 1. Começando uma nova tarefa
Sempre comece saindo da `develop`.

*   **Linha de Comando:**
    ```bash
    git checkout develop
    git pull origin develop
    git checkout -b feature/nome-da-sua-tarefa
    ```
*   **GitHub Desktop:**
    1. Clique em **Current Branch** e selecione `develop`.
    2. Clique em **Fetch origin** e depois em **Pull origin**.
    3. Clique em **Current Branch** > **New Branch**.
    4. Nomeie como: `feature/sua-tarefa` (ou `bugfix/sua-tarefa`).

---

### 2. Salvando seu progresso (Commit & Push)
Enquanto trabalha, vá salvando seu progresso.

*   **Linha de Comando:**
    ```bash
    git add .
    git commit -m "Explique o que você fez de forma simples"
    git push origin feature/nome-da-sua-tarefa
    ```
*   **GitHub Desktop:**
    1. No canto esquerdo inferior, escreva um **Summary** (ex: "Criei o modelo da mesa").
    2. Clique em **Commit to feature/...**.
    3. Clique no botão azul **Push origin** no topo.

---

### 3. Entregando a tarefa (Pull Request)
Tarefa pronta? Hora de levar para a `develop` e avisar o time!

1.  Vá no site do GitHub do projeto.
2.  Clique no botão verde **Compare & pull request**.
3.  Escolha a base como `develop` e compare com a sua `feature/...`.
4.  Crie o Pull Request.

---

### 4. O Grande Final: Release (Final de Etapa)
Quando terminamos as tarefas da semana ou sprint:
1.  Fazemos o merge da `develop` para a `main`.
2.  Geramos uma **Release** (Versão 1.0, 1.1...).

---

## 💡 Dicas de Sobrevivência

-   **Regra de Ouro**: Nunca, mas **NUNCA**, trabalhe direto na `main` ou na `develop`. Crie sempre uma branch nova!
-   **Prefixos Obrigatórios**:
    -   `feature/` : Para coisas novas (ex: `feature/modelo-mesa`).
    -   `bugfix/` : Para consertar erros (ex: `bugfix/colisao-parede`).
-   **Mensagens Curtas**: No commit, diga o que mudou (ex: "Ajustei cor da luz" em vez de "Atualizei o arquivo").

---
*Em caso de dúvidas ou se o Git "der erro", chame o @Rafael ou o líder do time!*
