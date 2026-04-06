# Base Pitch - TCC PUC Minas: Hora-Extra

## Equipe
- **Rafael Araújo** (DEV-GERAL)
- **Evans Farias** (DEV-UI)
- **Alice Matoso** (Game Design/Storytelling)
- **Gerson Jarreta** (Modelador 3D)

## Visão Geral do Jogo
"Hora-Extra" é um jogo multiplayer competitivo onde a burocracia ganha vida através de pesadelos corporativos e exaustão mental.

### Mecânicas Principais
- **Assimetria de Movimentação**: O **Mocinho** (Estagiário) é mais ágil e possui a habilidade de correr, enquanto os **Vilões** (Pesadelos Burocráticos) são mais lentos, porém jogam em maior número e possuem habilidades de controle.
- **Objetivo do Mocinho**: Escapar do ambiente resolvendo tarefas burocráticas pendentes sob alta pressão.
- **Objetivo dos Vilões**: Encurralar e capturar o estagiário. O toque de um vilão resulta em derrota imediata para o mocinho.
- **Estratégia de Campo**: Vilões podem posicionar armadilhas (ex: pilhas de papel intransponíveis, carimbos que paralisam) para reduzir a mobilidade do estagiário.

### Temática e Ambientação
O jogo é inspirado na lentidão e na fadiga de processos burocráticos. A ambientação utiliza espaços comuns do dia a dia corporativo — escritórios labirinticos, repartições públicas claustrofóbicas e arquivos empoeirados — transformando-os em arenas de sobrevivência.

---

## Ficha Técnica
- **Gêneros**: 3D, Labirinto, Perseguição, Estratégia, Competição, Plataforma.
- **Estilo Visual**: Semi-Realista 3D.
- **Plataforma**: PC.
- **Infraestrutura**: Multiplayer Competitivo Online (Arquitetura Cliente/Servidor).

---

## Conceito e História
### Premissa
Os processos são excessivamente burocráticos e nosso protagonista, um estagiário sobrecarregado, ainda não conhece os "macetes" do mundo corporativo. Com o trabalho acumulado e abusando de estimulantes para não adormecer, sua mente exausta começa a projetar seus maiores medos na realidade da "Hora-Extra".

**Os Pesadelos Corporativos:**
- **A Guardiã do Protocolo**: Uma figura austera que solicita o RG, mas após horas de espera, exige um comprovante de residência autenticado que o jogador não possui.
- **O Office Boy Fantasma**: Perde pastas cruciais em corredores que mudam de lugar, obrigando o jogador a refazer caminhos perigosos.
- **O Gerente de Prazos**: Um vulto onipresente que aumenta a tensão com cobranças impossíveis de entregas que já deveriam ter sido feitas ontem.
- **A Impressora Amaldiçoada**: Bloqueia rotas de fuga com atolamentos de papel infinitos e fumaça tóxica de toner.

---

## Gameplay Loop
1. **Lobby**: Os jogadores se reúnem e configuram a partida.
2. **Sorteio**: O servidor sorteia aleatoriamente os papéis (Estagiário vs. Pesadelos).
3. **Início**: O estagiário começa a localizar e resolver as tarefas obrigatórias para desbloquear a saída.
4. **Perseguição**: Os vilões spawnam em locais estratégicos e iniciam o cerco.
5. **Conclusão**:
   - **Vitória do Estagiário**: Conclui as tarefas e alcança a saída a tempo.
   - **Vitória dos Vilões**: Capturam o estagiário antes que ele consiga escapar.
6. **Ciclo**: O servidor calcula a pontuação e inicia o lobby para uma nova rodada.

---

## Audiovisual
- **Arte**: Estilo semi-realista 3D com iluminação dramática que enfatiza o cansaço (paleta de cores sóbrias, sombras longas e luzes de neon de escritório).
- **Som**: Trilha sonora de alta tensão, utilizando sons de escritório distorcidos (digitação frenética, telefones tocando ao longe, zumbido de ar-condicionado) para criar uma atmosfera opressora.

---

## Escopo e Cronograma
- **Versão Alfa (MVP)**: Mecânicas básicas, 1 mapa (Escritório), sistema de lobby funcional e 3 tipos de tarefas.
- **O que NÃO entra agora**: Customização estética de personagens, múltiplos mapas e chat de voz nativo.
- **Tempo Estimado**: 5 meses.