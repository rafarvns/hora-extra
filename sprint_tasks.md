# Sprint: Etapa 2 - Protótipo de Mecânicas

Status: Em Planejamento

---

### [Sub-épico 2.1] Unity: Movimentação Estagiário
**Descrição**: Implementar a lógica de movimentação 3D básica para o personagem "Estagiário".
**Requisitos**:
- Movimentação direcional (WASD).
- Sistema de velocidade superior (agilidade).
- Stamina de corrida (Shift): Barra de stamina, consumo e regeneração.
- Implementação via `CharacterController`.

---

### [Sub-épico 2.1] Unity: Movimentação Burocracia
**Descrição**: Implementar a lógica de movimentação 3D básica para o personagem "Burocracia".
**Requisitos**:
- Movimentação direcional (WASD).
- Velocidade fixa (passo constante e lento/médio).
- Implementação via `CharacterController`.

---

### [Sub-épico 2.2] Backend: Implementação Socket.io
**Descrição**: Configurar o servidor para comunicação bi-direcional em tempo real.
**Requisitos**:
- Inicialização do Socket.io no Node.js.
- Endpoint de monitoramento de conexões.
- Gerenciamento de eventos de conexão/desconexão.

---

### [Sub-épico 2.2] Unity: Cliente Socket
**Descrição**: Configurar o cliente Unity para se conectar ao backend Node.js.
**Requisitos**:
- Integração de biblioteca Socket.io C#.
- Gerenciamento de conexão (Host/URL dinâmica).
- Log de status de conexão no console do Unity.

---

### [Sub-épico 2.3] Sincronização: Replicação de Movimento
**Descrição**: Garantir que o movimento de um jogador seja refletido para todos os outros clientes.
**Requisitos**:
- Envio de pacotes de posição/rotação (Rate limited).
- Recebimento e atualização de clones remotos.
- Interpolação (Lerp) para suavizar o movimento na rede.

---

### [Sub-épico 2.3] Ambiente de Testes: Mapa Vazio
**Descrição**: Criar uma cena de teste para validar a integração multiplayer.
**Requisitos**:
- Cena com plano de chão e iluminação básica.
- Sistema de Spawn de jogadores no início da conexão.
- Diferenciação visual entre tipos de personagens (Estagiário vs Burocracia).
