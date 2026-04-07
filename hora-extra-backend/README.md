# Hora-Extra - Backend 🖥️

O servidor do jogo **Hora-Extra**, responsável pelo gerenciamento de partidas, estados de lobby e persistência básica.

---

## 🛠️ Tecnologias
- **Node.js**: (Recomendado v25+)
- **TypeScript**: v6+
- **Express**: v5+
- **ts-node-dev**: Para desenvolvimento acelerado.

---

## 🚀 Como Rodar
1. Instale as dependências:
   ```bash
   npm install
   ```
2. Inicie o servidor em modo de desenvolvimento (com auto-reload):
   ```bash
   npm run dev
   ```
3. O servidor estará rodando em um endereço definido (ex: `http://localhost:3000`).

---

## 📁 Estrutura de Pastas
- `src/`: Todo o código fonte TS.
- `src/index.ts`: Ponto de entrada do servidor.
- `dist/`: Build de produção (gerado ao rodar `npm run build`).

---

## 🏗️ Build para Produção
Para compilar o código TypeScript em JavaScript puro:
```bash
npm run build
```
Depois, para iniciar o servidor em modo de produção:
```bash
npm start
```

---

## 🔐 Autenticação (JWT)

O sistema de autenticação utiliza JSON Web Tokens (JWT) para proteger as rotas do backend.

### Configuração
1.  Renomeie o arquivo `.env.example` para `.env`.
2.  Defina a variável `JWT_SECRET` com uma chave segura.

### Endpoints (Base: `/api/auth`)
| Método | Endpoint | Descrição |
| :--- | :--- | :--- |
| `POST` | `/register` | Registra um novo jogador (Requer: `nome`, `email`, `senha`) |
| `POST` | `/login` | Realiza login e retorna o `token` (Requer: `email`, `senha`) |

### Como usar o Token
Para acessar rotas protegidas, inclua o token no header `Authorization`:
```http
Authorization: Bearer <SEU_TOKEN_AQUI>
```

---
*Em caso de dúvidas técnicas, consulte o time de programação.*
