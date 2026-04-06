# Game Design Document: Hora-Extra

**PROJETO TCC - 2026/1**  
*"Sobreviva ao pior estágio da sua vida"*

---

## 1. Equipe
- **Rafael Araújo**: Programação Geral
- **Evans Farias**: Interface de Usuário (UI)
- **Alice Matoso**: Game Design e Produção
- **Gerson Jarreta**: Modelagem 3D e Criação de Personagens
- **Emilly Vitória**: Modelagem 3D e Criação de Personagens

---

## 2. Visão Geral do Jogo
**Hora-Extra** é um jogo multiplayer cooperativo de terror psicológico onde estagiários tentam sobreviver a um escritório caótico e a um chefe implacável. No jogo, a burocracia do mundo real é transformada em elementos de tensão e perseguição.

---

## 3. Loop de Gameplay
O ciclo de jogo segue uma estrutura de pressão crescente:
1. **Início do Jogo**: Jogadores se reúnem no lobby.
2. **Início da Partida**: A exploração do escritório começa para localizar as tarefas pendentes.
3. **Execução de Tarefas**: Os jogadores devem completar objetivos burocráticos sob pressão constante.
4. **Fuga do Chefe**: Um chefe implacável começa a perseguir os jogadores pelos corredores.
5. **Desfecho da Rodada**: Vitória ao escapar com sucesso ou derrota ao ser capturado, reiniciando o ciclo.

---

## 4. Objetivos e Condições

### Objetivo Principal
Completar todas as tarefas designadas no tempo/limite de recursos e escapar do escritório antes de ser pego.

### Condição de Vitória
Todos os jogadores sobreviventes devem completar a lista de tarefas e alcançar a saída do local.

### Condição de Derrota
O chefe captura todos os estagiários antes que as tarefas sejam finalizadas ou antes da fuga.

---

## 5. Inspirações de Gameplay e Mecânicas

### Jogos e Mídias
- **Dead by Daylight**: Pela mecânica de perseguição e elementos de sobrevivência cooperativa assimétrica.
- **Among Us**: Pela interação sob pressão e foco em completar tarefas enquanto um perigo espreita.
- **Pac-Man**: Pelo loop clássico de perseguição em ambientes labirínticos.
- **R.E.P.O**: Como referência para a mecânica de lobby e preparação.
- **The Stanley Parable**: Pela estética de ambiente corporativo surreal e opressor.
- **Office Space & The Office**: Pela ambientação, temática e humor ácido sobre a vida em escritórios.

---

## 6. Direção de Arte e Estilo Visual

- **Estilo**: 3D Low Poly mesclado com elementos semi-realistas.
- **Ambiente**: Representação de um ambiente corporativo tradicional (cubículos, luzes fluorescentes, arquivos metálicos) com um toque de surrealismo e desordem.
- **Referências Visuais**: Ambientes limpos de escritório contrastando com áreas caóticas de "blocagem" e sombras longas que intensificam o terror.

---

## 7. Escopo do MVP (Para o Semestre)
Para o desenvolvimento inicial, o foco será:
- **Mapa Principal**: Um mapa contendo sala do chefe, estações de trabalho, sala de reuniões e copa (fase de blocagem).
- **Inimigo Principal**: Um chefe funcional com IA de perseguição básica.
- **Sistema de Tarefas**: Implementação das interações básicas para progresso na partida.
- **Sistema de Stamina**: Gerenciamento de recursos de movimentação (corrida/fuga).
- **Personagens**: Modelos básicos (blocagem) para os estagiários e o chefe.

---

## 8. Metodologia de Trabalho
- **Framework**: Metodologia **SCRUM**.
- ** sprints**: Ciclos de desenvolvimento semanais.
- **Ferramentas**: Utilização de quadros como Trello para gestão do backlog, sprints e acompanhamento de bugs/ajustes.
