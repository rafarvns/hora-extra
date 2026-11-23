# Uso do Cliente REST (Unity) - Hora-Extra

Este documento explica como se comunicar com o servidor backend via REST usando o cliente Unity.

## 1. Localização e Scripts
- **Localização**: `Assets/Scripts/Network/Rest/`
- **Principais Scripts**:
    - `ApiClient.cs`: O coração da conexão REST. Estático e genérico.
    - `ApiResponse.cs`: O invólucro para todas as respostas JSON, contendo dados e metadados.
    - `Services/`: Serviços de domínio por recurso (ex: `HealthService`).

---

## 2. Como Fazer uma Chamada REST
Todo novo recurso ou integração deve ser feito através de uma classe `Service` dedicada.

### Passo 1: Definir os Modelos de Dados
Crie as classes de dados que representam o seu JSON (use `[Serializable]` e `using Newtonsoft.Json` se quiser chaves customizadas):

```csharp
[Serializable]
public class PersonData {
    public string name;
    public int age;
}
```

### Passo 2: Criar o Serviço Assíncrono
Use o `ApiClient` para enviar a requisição de forma assíncrona:

```csharp
public class MyService {
    public async Task<PersonData> GetPersonInfo() {
        var response = await ApiClient.Get<PersonData>("/person/info");
        if (response.Success) {
            return response.Data;
        } else {
            Debug.LogError(response.Error.Message);
            return null;
        }
    }
}
```

---

## 3. Boas Práticas
1. **Async/Await**: SEMPRE utilize chamadas assíncronas para não travar a UI (Thread Principal) do Unity enquanto aguarda a rede.
2. **Gerenciamento de Erros**: Sempre verifique o campo `Success` na resposta antes de tentar acessar os `Data`. Caso ocorra uma falha, o campo `Error` trará os detalhes.
3. **Serialização**: As chaves do seu arquivo C# devem bater EXATAMENTE com as chaves JSON retornadas pelo backend, a menos que use o atributo `[JsonProperty("outra_chave")]`.

---

## 4. Exemplo: sessão e salas

Não há cadastro: o jogador só digita o nome. Os três endpoints ficam em `RoomService`:

```csharp
var sessao = await RoomService.CreateSession("Ana");        // { token, playerId, nome }
var salas  = await RoomService.GetRooms();                  // [{ id, nome, jogadores, max, emAndamento }]
var sala   = await RoomService.CreateRoom("Sala da Ana");   // { id, nome }
```

Na prática, use `MatchJoinFlow.CreateAndJoin` / `MatchJoinFlow.Join`, que também conectam o UDP e
carregam o 1º andar. Ver [MULTIPLAYER.md](../Mechanics/MULTIPLAYER.md).

---

## 5. Exemplos Implementados
- **HealthCheck**: `HealthService.cs`.
- **Sessão e salas**: `RoomService.cs` + `MatchJoinFlow.cs`.
