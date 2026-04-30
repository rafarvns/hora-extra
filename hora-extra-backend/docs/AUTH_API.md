# Documentação da API de Autenticação

Esta API gerencia o registro e a autenticação de jogadores para o projeto Hora Extra.

## Endpoints

### 1. Registro de Jogador
Cria uma nova conta de jogador.

- **URL**: `/api/auth/register`
- **Método**: `POST`
- **Autenticação**: Nenhuma

#### Request Body
```json
{
  "nome": "Nome do Jogador",
  "email": "jogador@exemplo.com",
  "senha": "senha_segura"
}
```

#### Resposta de Sucesso (201 Created)
```json
{
  "success": true,
  "data": {
    "token": "JWT_TOKEN_HERE",
    "jogador": {
      "id": "uuid",
      "nome": "Nome do Jogador",
      "email": "jogador@exemplo.com",
      "nivel": 1,
      "xp": 0
    }
  }
}
```

---

### 2. Login de Jogador
Autentica um jogador existente e retorna um token de acesso.

- **URL**: `/api/auth/login`
- **Método**: `POST`
- **Autenticação**: Nenhuma

#### Request Body
```json
{
  "email": "jogador@exemplo.com",
  "senha": "senha_segura"
}
```

#### Resposta de Sucesso (200 OK)
```json
{
  "success": true,
  "data": {
    "token": "JWT_TOKEN_HERE",
    "jogador": {
      "id": "uuid",
      "nome": "Nome do Jogador",
      "email": "jogador@exemplo.com",
      "nivel": 1,
      "xp": 0
    }
  }
}
```

#### Erros Comuns
- **401 Unauthorized**: E-mail ou senha incorretos.
- **400 Bad Request**: Campos obrigatórios ausentes ou e-mail já cadastrado.
