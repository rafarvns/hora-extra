# Implementação de Rede UDP (Root) - Hora-Extra

Este documento detalha a implementação do sistema de rede customizado baseado em UDP Datagrams, que substituiu o Socket.IO para prover uma sincronização de jogo mais robusta e performática.

## 🚀 Motivação
O Socket.IO, embora robusto para aplicações web tradicionais, apresentava instabilidades no ambiente Unity 6 e overhead excessivo para sincronização de alta frequência (movimento). A migração para UDP "raiz" elimina dependências de terceiros e reduz a latência.

## 🏗️ Lado do Servidor (Node.js)

### **UdpSocketManager.ts**
O servidor utiliza o módulo nativo `dgram` do Node.js.
- **Porta**: 3001 (UDP).
- **Gerenciamento de Sessão**: Como o UDP é connectionless, o servidor mantém um `Map` interno associando `IP+Porta` a um `PlayerId`.
- **Autenticação**: O primeiro pacote deve ser o evento `CONN` com o token JWT.
- **Heartbeat**: Sessões são removidas automaticamente após 30 segundos de inatividade.

### **Handlers Agnósticos**
Os handlers foram refatorados para receber `RemoteInfo` (detalhes do remetente) em vez de um objeto `Socket`. Isso permite que a lógica de negócio (como entrar em salas ou mover) seja independente do protocolo de transporte.

---

## 🏗️ Lado do Cliente (Unity/C#)

### **SocketManager.cs (UDP)**
Implementação nativa usando `System.Net.Sockets.UdpClient`.
- **Thread Safety**: O recebimento de pacotes ocorre em uma thread separada. As ações que interagem com o Unity (Transform, Debug.Log) são enfileiradas e processadas no `Update()` (Main Thread).
- **Sem Dependências**: Removido o plugin SocketIO. A única dependência mantida foi o `Newtonsoft.Json` (via Package Manager oficial da Unity) para serialização robusta.

### **Uso Básico**
```csharp
// Enviar movimento
SocketManager.Instance.Emit("player_move", new { p = position, r = rotation });

// Escutar eventos
SocketManager.Instance.On("player_joined", (data) => {
    Debug.Log("Jogador entrou: " + data["name"]);
});
```

---

## 🔐 Segurança e Desenvolvimento
O bypass de desenvolvimento (`DEV_TEST_TOKEN`) foi mantido no handshake `CONN`, permitindo testes rápidos sem passar pelo fluxo completo de login.

---

## 🔧 Manutenção
- Para adicionar novos eventos, basta criar o handler no backend e registrar na `SocketHandlerFactory.ts`. No Unity, use `SocketManager.Instance.On`.
- Caso pacotes fora de ordem se tornem um problema, deve-se adicionar um campo `sequence` (int) em `COMMUNICATION.md` e validar no receptor.
