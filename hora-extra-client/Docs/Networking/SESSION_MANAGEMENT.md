## SessionManager (Singleton)

O `SessionManager` é um script persistente (`DontDestroyOnLoad`) que gerencia os dados da sessão. 

### Funcionalidades Principais

1.  **Persistência**: O token JWT e o nome do jogador são salvos no `PlayerPrefs`.
2.  **Auto-inicialização**: O sistema é capaz de se auto-instanciar caso não exista na cena no momento do login.
3.  **Acesso Global**: Acessível via `HoraExtra.Network.SessionManager.Instance`.
4.  **Sistema de Eventos**: Utiliza `OnSessionUpdated` para notificar componentes de UI (como o Menu Principal) sempre que o estado da sessão mudar.

### Como Usar

#### Se inscrever para atualizações de UI:
```csharp
private void OnEnable() {
    SessionManager.Instance.OnSessionUpdated += UpdateMyUI;
}
```

#### Acessar dados do jogador:
```csharp
string nome = SessionManager.Instance.CurrentPlayer.Nome;
```

#### Encerrar Sessão (Logout):
```csharp
SessionManager.Instance.ClearSession();
```

## Fluxo de Autenticação

1.  **Login/Cadastro**: O `LoginController` ou `CreateAccountController` chama o `AuthService`.
2.  **Sucesso**: O `AuthService` retorna um objeto `AuthData` (Token + Jogador).
3.  **Definição**: O controlador chama `SessionManager.Instance.SetSession(authData)`.
4.  **Menu Principal**: O `MainMenuController` verifica a sessão no `Start()` e atualiza a interface (esconde botões de login, mostra nome do usuário).

## Configuração no Editor

1.  Certifique-se de que o script `SessionManager.cs` esteja em um GameObject na sua cena inicial.
2.  O script lidará automaticamente com a criação do Singleton e a persistência entre cenas.
