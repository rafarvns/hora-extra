# Documentação: Implementação do Socket.IO (Unity)

Esta pasta documenta a implementação customizada do cliente de rede para o projeto **Hora-Extra**.

## 🚀 Visão Geral
Devido a instabilidades e bugs no repositório original via Git URL (Package Manager), decidimos migrar a biblioteca para uma solução nativa baseada em `ClientWebSocket` do .NET, localizada em `Assets/Plugins/SocketIO`.

Esta implementação suporta o protocolo **Socket.IO v4 (Engine.IO v4)** e resolve os seguintes problemas:
1.  **Erros de Construtor**: Corrigido o erro de sintaxe no `NewtonsoftJsonSerializer` que ocorria no Unity 6.
2.  **Threading**: Uso de `UnityThread` para garantir que os eventos de rede cheguem com segurança à Thread Principal do Unity.
3.  **Independência**: Não depende mais de pacotes externos do GitHub, eliminando erros de clonagem (`git clone failed`).

---

## 🏗️ Estrutura do Cliente

Os scripts principais estão em `Assets/Plugins/SocketIO/`:

1.  **SocketIOUnity.cs**: O wrapper principal. Implementa a conexão WebSocket e o sistema de eventos.
2.  **SocketIOClientCore.cs**: Define os tipos necessários para que o `SocketManager` e os dados JSON funcionem.
3.  **UnityThread.cs**: Singleton responsável por despachar ações da thread de rede para a thread do jogo.

---

## 🛠️ Como Usar (SocketManager)

O **SocketManager** é o "Boss" da rede no Unity. Veja as funções básicas:

### 1. Conexão
A conexão é iniciada no `Start()` se `AutoConnect` estiver ativado:
```csharp
SocketManager.Instance.ConnectToServer();
```

### 🔐 Autenticação (JWT)
O servidor agora exige um token JWT válido para permitir a conexão. O token deve ser passado no objeto `Auth` da configuração:

```csharp
// Exemplo de como passar o token no SocketManager.cs
var options = new SocketIOOptions
{
    Auth = new Dictionary<string, string>
    {
        { "token", PlayerData.Instance.AuthToken }
    }
};

_socket = new SocketIOUnity(serverUrl, options);
_socket.Connect();
```

> [!IMPORTANT]
> Se o token for inválido, o servidor emitirá um erro de conexão (`connect_error`) e o cliente não conseguirá emitir ou receber eventos.

### 2. Escutar Eventos (Receber dados)
Use `OnUnityThread` para que o seu código de UI ou Game Objects funcione sem erros:
```csharp
_socket.OnUnityThread("player_joined", (data) => {
    string id = data.GetValue<string>("id");
    Debug.Log("Novo jogador entrou: " + id);
});
```

### 3. Emitir Eventos (Enviar dados)
Use `Emit` passando o nome do evento e um objeto/classe anônima para converter em JSON:
```csharp
SocketManager.Instance.Emit("player_input", new { 
    direction = new { x = 1, y = 0 }, 
    actions = new string[] { "interact" } 
});
```

---

## 🔧 Configurações do Protocolo
O cliente utiliza o protocolo `EIO=4` (Socket.IO v4). Caso o servidor seja atualizado para v5 ou sofra mudanças drásticas, as modificações devem ser feitas em `SocketIOUnity.cs`, especificamente no método `ParseMessage` e `Emit`.

## ⚠️ Observações para o Unity 6
- **Newtonsoft.Json**: A implementação depende do pacote oficial `com.unity.nuget.newtonsoft-json` (v3.2.1 ou superior) instalado via manifest.json.
- **IL2CPP**: Como usamos `ClientWebSocket` nativo, a implementação é altamente compatível com builds para Android, iOS e Windows sem necessidade de configurações extras.
