# Sistema de Autenticação - Hora-Extra

Este documento detalha o funcionamento técnico da autenticação no backend.

## 🔑 Tecnologia
- **JWT (JSON Web Token)**: Utilizado para gerar tokens de acesso sem estado (stateless).
- **BcryptJS**: Utilizado para hash de senhas antes de salvar no banco de dados.

## 📡 Endpoints de Autenticação

### 1. Registro (`POST /api/auth/register`)
Cria um novo jogador no sistema.

**Body (JSON):**
```json
{
  "nome": "Meu Nome",
  "email": "jogador@exemplo.com",
  "senha": "minha_senha_segura"
}
```

**Resposta (201 Created):**
Retorna os dados do jogador e o token de acesso.

---

### 2. Login (`POST /api/auth/login`)
Autentica um jogador existente.

**Body (JSON):**
```json
{
  "email": "jogador@exemplo.com",
  "senha": "minha_senha_segura"
}
```

**Resposta (200 OK):**
Retorna os dados do perfil (nível, xp, etc) e o token.

---

## 🛡️ Protegendo Rotas (Uso do Middleware)

Para exigir que um jogador esteja autenticado para acessar um endpoint, utilize o `authMiddleware`.

### Exemplo no Código (Controller):
```typescript
import { Router } from 'express';
import authMiddleware from '../../middleware/authMiddleware.js';

const router = Router();
router.get('/meu-perfil', authMiddleware.authenticate, (req, res) => {
  const jogadorId = req.jogadorId; // Disponível após o middleware
  // Busca dados no banco usando jogadorId...
});
```

### Exemplo de Chamada (Client Unity/C#):
O token deve ser enviado no header `Authorization` em todas as requisições protegidas:
`Authorization: Bearer <TOKEN_JWT>`

---

## 🎨 O Jeito Fácil (Usando Annotations)

Você pode usar o decorator `@Authorize()` diretamente nos métodos do seu controlador para automatizar a proteção.

### Como usar:
Basta adicionar a anotação acima do método que deseja proteger.

```typescript
import { Request, Response, NextFunction } from 'express';
import { Authorize } from '../../core/decorators/Authorize.js';
import { AuthRequest } from '../../types/AuthRequest.js';

class PerfilController extends BaseController {
    
    @Authorize()
    public async getDados(req: AuthRequest, res: Response, next: NextFunction) {
        // Se chegar aqui, o jogador já está autenticado!
        const id = req.jogadorId;
        this.sendSuccess(res, { id, status: 'VIP' });
    }
}
```

> **Atenção:** O decorator `@Authorize()` funciona melhor com métodos de classe padrão (como no exemplo acima). Se você usar arrow functions (`public metodo = () => {}`), prefira passar o middleware manualmente na rota.

### Na Rota:
Ao usar o decorator, você **não precisa** passar o middleware no arquivo de rotas:
```typescript
// router.get('/perfil', authMiddleware.authenticate, controller.getDados); <- Não precisa mais!
router.get('/perfil', controller.getDados); // Protegido automaticamente pelo @Authorize()
```

---

## 📡 Autenticação em WebSockets

Diferente das rotas REST tradicionais, o WebSocket utiliza os dados de **Handshake** para autenticação. Isso evita que pacotes individuais precisem carregar o token, economizando banda e processamento.

### Como conectar (Client Unity/C#)

Para se conectar ao servidor Socket.IO, você deve enviar o token JWT no objeto `auth`. 

Exemplo usando `SocketIOUnity`:

```csharp
var options = new SocketIOOptions
{
    Auth = new Dictionary<string, string>
    {
        { "token", userToken } // O token obtido via login REST
    }
};

socket = new SocketIOUnity(serverUrl, options);
socket.Connect();
```

### Comportamento no Servidor:
1.  **Middleware**: O servidor intercepta a conexão antes de disparar o evento `OnConnect`.
2.  **Validação**: O token é extraído de `handshake.auth.token` e validado via `jwt.verify`.
3.  **Recusa**: Se o token for inválido, o cliente receberá um erro de conexão (`connect_error`) e a conexão será fechada.
4.  **Dados**: Após autenticado, o ID do jogador fica disponível no lado do servidor em `socket.data.jogadorId`.

---

## 🛠️ Sessões de Teste (Development Bypass)

Para agilizar o desenvolvimento do frontend Unity, o backend suporta um **bypass de autenticação** quando executado em modo de desenvolvimento (`NODE_ENV=development`). Isso permite conectar ao socket sem precisar realizar o login REST previamente.

### Como usar (Client Unity/C#)

Configure o token de teste (definido no `.env` do servidor) no objeto de autenticação do socket:

```csharp
var options = new SocketIOOptions
{
    Auth = new Dictionary<string, string>
    {
        { "token", "horaextra_dev_test_token_2026" } // Valor padrão de desenvolvimento
    }
};

socket = new SocketIOUnity(serverUrl, options);
socket.Connect();
```

### Configurações no Servidor (`.env`):
- `NODE_ENV`: Deve ser `development`.
- `DEV_TEST_TOKEN`: A chave mestra que o cliente deve enviar.
- `DEV_TEST_USER_ID`: O ID fictício que o servidor atribuirá a essa conexão (ex: `dev-test-player-id-001`).

> [!WARNING]
> Este recurso é desabilitado automaticamente se o servidor for iniciado em modo de produção por segurança.

---
*Em caso de dúvidas técnicas, consulte o time de programação.*
