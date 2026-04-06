# Hora-Extra 🏢📁☕

**Hora-Extra** é um jogo multiplayer competitivo de terror psicológico onde a burocracia ganha vida através de pesadelos corporativos e exaustão mental. Desenvolvido como Projeto de Trabalho de Conclusão de Curso (TCC) na PUC Minas.

---

## 👁️ Visão Geral
Em "Hora-Extra", os jogadores assumem o papel de estagiários sobrecarregados tentando sobreviver a um escritório caótico e a um chefe implacável. Inspirado na lentidão e na fadiga de processos burocráticos, o jogo transforma o ambiente corporativo — cubículos, repartições públicas e arquivos empoeirados — em uma arena de sobrevivência.

### 🎮 Gameplay Loop
1. **Lobby:** Jogadores se reúnem e configuram a partida.
2. **Sorteio:** O servidor define aleatoriamente quem será o **Mocinho (Estagiário)** e quem serão os **Vilões (Pesadelos Burocráticos)**.
3. **Exploração:** O estagiário deve localizar e resolver tarefas burocráticas pendentes sob alta pressão.
4. **Perseguição:** Os vilões devem encurralar e capturar o estagiário antes que ele escape.
5. **Conclusão:** Vitória para o estagiário se ele concluir as tarefas e alcançar a saída; vitória para os vilões se capturarem o estagiário.

---

## 📁 Estrutura do Projeto (Monorepo)

Este repositório está organizado da seguinte forma:

- 📂 `arte/`: Modelos 3D, texturas e assets visuais.
- 📂 `documents/`: Documentação oficial (Pitch, GDD, Links úteis).
- 📂 `hora-extra-backend/`: Servidor Node.js (TypeScript) para gerenciamento de partidas e dados.
- 📂 `hora-extra-client/`: Cliente de jogo desenvolvido em **Unity**.

---

## 🛠️ Tecnologias Principais

- **Motor de Jogo:** Unity (C#)
- **Backend:** Node.js, TypeScript, Express.
- **Modelagem 3D:** Estilo Semi-Realista / Low Poly.
- **Metodologia:** SCRUM com sprints semanais.

---

## 🚀 Como Começar

### Backend 🖥️
Para rodar o servidor localmente:
1. Navegue até a pasta `hora-extra-backend`.
2. Instale as dependências: `npm install`.
3. Inicie o servidor em modo de desenvolvimento: `npm run dev`.

### Client 🎮
1. Abra a pasta `hora-extra-client` com o **Unity Hub**.
2. Certifique-se de estar utilizando a versão correta do Unity.
3. Abra a cena principal em `Assets/Scenes`.

---

## 👥 Equipe
- **Rafael Araújo**: Programação Geral
- **Evans Farias**: Interface de Usuário (UI)
- **Alice Matoso**: Game Design e Produção
- **Gerson Jarreta**: Modelagem 3D e Criação de Personagens
- **Emilly Vitória**: Modelagem 3D e Criação de Personagens

---

## 🔗 Links e Documentação
Para mais detalhes, consulte a pasta `documents/`:
- [Project Pitch](documents/pitch.md)
- [Game Design Document (GDD)](documents/gdd.md)
- [Link úteis](documents/links.md)

---
*Este projeto é parte do currículo de Engenharia de Software da PUC Minas.*
