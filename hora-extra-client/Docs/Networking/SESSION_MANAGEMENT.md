# Gestão de Sessão (Unity Client)

O sistema de sessão é responsável por persistir o estado do jogador logado e fornecer acesso global ao token de autenticação e dados do perfil.

## SessionManager (Singleton)

O `SessionManager` é um script anexado a um GameObject persistente (`DontDestroyOnLoad`) que gerencia os dados da sessão.

### Funcionalidades Principais

1.  **Persistência**: O token JWT e o nome do jogador são salvos no `PlayerPrefs`, permitindo que a sessão seja recuperada ao reiniciar o jogo.
2.  **Acesso Global**: Pode ser acessado de qualquer script via `HoraExtra.Network.SessionManager.Instance`.
3.  **Estado do Login**: A propriedade `IsLoggedIn` indica se há um token válido em memória.

### Como Usar

#### Verificar se o jogador está logado:
```csharp
if (SessionManager.Instance != null && SessionManager.Instance.IsLoggedIn) {
    Debug.Log("Jogador autenticado!");
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
