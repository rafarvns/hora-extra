# Guia da API REST - Hora-Extra

Este documento descreve como funciona a arquitetura REST do backend e como adicionar novos recursos.

## 1. Arquitetura Modular
O projeto utiliza um padrão de separação de responsabilidades para garantir escalabilidade:

- **Controllers (`src/api/controllers/`)**: Recebem a requisição (`Request`), chamam os serviços necessários e devolvem a resposta (`Response`).
- **Routes (`src/api/routes/`)**: Definem os caminhos (URLs) e quais métodos HTTP (GET, POST, etc) levam a quais controllers.
- **Middleware (`src/middleware/`)**: Funções executadas antes ou depois da lógica principal (ex: autenticação, tratamento de erros).
- **Core (`src/core/`)**: Classes básicas reutilizáveis (BaseController, ApiError, ApiResponse).

---

## 2. Como Criar um Novo Endpoint

### Passo 1: Criar o Controller
Crie um arquivo em `src/api/controllers/ExemploController.ts` estendendo `BaseController`:

```typescript
export class ExemploController extends BaseController {
  public meuMetodo = async (req: Request, res: Response, next: NextFunction) => {
    try {
      // Lógica aqui...
      return this.sendSuccess(res, { some: 'data' });
    } catch (err) {
      next(err); // Passa o erro para o middleware global
    }
  }
}
```

### Passo 2: Definir as Rotas
Crie um arquivo em `src/api/routes/exemploRoutes.ts`:

```typescript
const router = Router();
const controller = new ExemploController();
router.get('/info', controller.meuMetodo);
export default router;
```

### Passo 3: Registrar no Agregador
Adicione a nova rota em `src/api/routes/index.ts`:

```typescript
router.use('/exemplo', exemploRoutes);
```

---

## 3. Tratamento de Erros
Nunca use `res.status(400).send(...)` manualmente dentro de um serviço ou controller complexo. Em vez disso, utilize a classe `ApiError`:

```typescript
throw ApiError.notFound('Usuário não encontrado');
// OU
throw ApiError.badRequest('Dados inválidos');
```

O middleware global (`errorHandler.ts`) capturará o erro e formatará a resposta JSON automaticamente.

---

## 4. Autenticação (JWT)

A API utiliza JSON Web Tokens (JWT) para autenticação.

### Endpoints
- `POST /api/auth/register`: Cria um novo jogador.
- `POST /api/auth/login`: Autentica um jogador e retorna o token.

### Protegendo Rotas
Para proteger uma rota, utilize o `authMiddleware.authenticate`:

```typescript
import authMiddleware from '../../middleware/authMiddleware.js';

router.get('/perfil', authMiddleware.authenticate, controller.getPerfil);
```

As rotas protegidas esperam o header `Authorization`:
`Authorization: Bearer <TOKEN>`

O ID do jogador autenticado estará disponível em `req.jogadorId`.

---

## 5. Endpoints Atuais
- `GET /api/health`: Verifica o status do servidor.
- `POST /api/auth/register`: Cadastro de jogador.
- `POST /api/auth/login`: Login de jogador.
