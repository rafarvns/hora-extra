---
name: backend-prisma-migration
description: Aplicar quando precisar editar o schema Prisma, rodar migrations, ou trocar entre SQLite/MySQL. O script `scripts/db-setup.ts` reescreve o `schema.prisma` baseado em `USE_SQLITE` — não editar manualmente o `.bak`.
applies_to: backend
---

# backend-prisma-migration — Schema, migrations e troca SQLite/MySQL

## Quando aplicar

- Adicionar / alterar / remover model Prisma
- Adicionar / alterar enum, índice, relação
- Trocar provider de SQLite pra MySQL ou vice-versa
- Resolver bug de "Prisma client está desalinhado com schema"

## Quando NÃO aplicar

- Mudança apenas de query no service (`prisma.user.findMany(...)`) — isso é uso, não migration
- Adicionar coluna `userId` mas relação já existe — só `db:generate`

## Arquitetura SQLite vs MySQL — não óbvio

O projeto **suporta os dois providers** via flip controlado por env:

| Modo                | Trigger                                  | Resultado                                                                                |
| :------------------ | :--------------------------------------- | :--------------------------------------------------------------------------------------- |
| **SQLite (dev)**    | `.env` com `USE_SQLITE=true`             | `scripts/db-setup.ts` rewriting `schema.prisma` pra `provider = "sqlite"`, `url = "file:./dev.db"`. Roda `db push`. |
| **MySQL (prod-ish)**| `.env` com `USE_SQLITE=false`            | Script **restaura** o schema do backup `schema.prisma.bak` (que é a versão MySQL canônica). NÃO roda migrations automaticamente — precisa `npm run db:migrate` manual. |

### Fluxo automático (predev)

`npm run dev` chama `predev` que roda `scripts/db-setup.ts`. Você quase nunca precisa rodar manualmente — mas precisa saber pra **não editar** o arquivo errado.

## O arquivo que VOCÊ edita

**Sempre** edite `prisma/schema.prisma` (a versão "ao vivo"). Se você está em modo SQLite, ele já está com `provider = "sqlite"` no topo. Se está em MySQL, com `provider = "mysql"`.

**Nunca edite `prisma/schema.prisma.bak`** — esse é gerado e regenerado pelo `db-setup.ts`. Editar à mão é perdido na próxima troca.

> Se você precisar editar um model que tem **comportamento diferente entre os 2 providers** (ex: `@db.Text` só existe no MySQL), edite `schema.prisma` quando estiver em MySQL e deixe o script reescrever ao trocar. Faça testes em ambos os modos.

## Workflow: adicionar campo a model existente

### 1. Edite `prisma/schema.prisma`

```prisma
model User {
  id        String   @id @default(uuid())
  email     String   @unique
  password  String
  nickname  String?                       // ← NOVO campo opcional
  createdAt DateTime @default(now())
  updatedAt DateTime @updatedAt
}
```

### 2. Gere o client (sempre)

```bash
cd hora-extra-backend && npm run db:generate
```

Equivale a `prisma generate`. Atualiza os tipos TypeScript em `node_modules/.prisma/client`. **Obrigatório** ou TS vai reclamar que `nickname` não existe em `User`.

### 3. Aplique no banco

**SQLite (dev)**: o `predev` já rodou `db push` quando você iniciou `npm run dev`. Se a app já está rodando, mate-a e reinicie — ou rode manualmente:

```bash
cd hora-extra-backend && npx prisma db push
```

> `db push` sincroniza schema com DB **sem migration**. Útil em dev. **Não usar em prod.**

**MySQL (prod-ish / staging)**: rode migration de verdade:

```bash
cd hora-extra-backend && npm run db:migrate
# pergunta nome da migration → use kebab-case: add-user-nickname
```

Equivale a `prisma migrate dev`. Gera `prisma/migrations/<timestamp>_add_user_nickname/migration.sql` e aplica.

## Workflow: novo model

### 1. Schema

```prisma
model Room {
  id        String   @id @default(uuid())
  name      String
  maxPlayers Int     @default(4)
  ownerId   String
  owner     User     @relation(fields: [ownerId], references: [id], onDelete: Cascade)
  createdAt DateTime @default(now())
  updatedAt DateTime @updatedAt

  @@index([ownerId])
}

// Atualize o lado da relação no model existente:
model User {
  // ...existing fields...
  rooms     Room[]
}
```

### 2. Generate + migrate (como acima)

### 3. Use no código

```ts
import prisma from '../database/prisma.js';

const room = await prisma.room.create({
    data: { name: 'Sala 1', ownerId: userId },
});
```

> Casing do client Prisma segue snake_case→camelCase: `Room` → `prisma.room`. `NPCGroup` → `prisma.nPCGroup` (caso raro de PascalCase com sigla — autocomplete revela).

## Convenções (alinhar com models existentes)

- **`id String @id @default(uuid())`** sempre. Não use `Int @id @default(autoincrement())`.
- **`createdAt` e `updatedAt`** em models de longa duração (`User`, `Room`). Ephemera (`PlayerSession` em memória — não persiste) não tem.
- **Relações com `onDelete: Cascade`** quando o filho não faz sentido sem o pai. `SetNull` se sobrevive (FK opcional).
- **Enums dedicados** em vez de string mágica:
  ```prisma
  model User {
    role UserRole @default(USER)
  }
  enum UserRole { USER ADMIN }
  ```
- **Índices** em FKs e em campos de busca frequente: `@@index([ownerId])`.

### Diferenças que importam entre SQLite e MySQL

| Feature              | SQLite           | MySQL                       |
| :------------------- | :--------------- | :-------------------------- |
| `@db.Text`           | Ignora           | LONGTEXT                    |
| `@db.VarChar(N)`     | Ignora (TEXT)    | VARCHAR(N)                  |
| Enum nativo          | Não tem          | `ENUM(...)` no DDL          |
| `@unique` composto   | Suportado        | Suportado                   |
| JSON                 | `String` na real | `JSON` nativo               |

> Se sua app **só roda em SQLite local** durante TCC e MySQL em prod, fique atento: usar `@db.Text` é OK (SQLite ignora). Usar `Json` type só funciona se ambos suportam. Em dúvida, use `String` e parse no service.

## Comandos seguros / inseguros

### ✅ Seguros

```bash
cd hora-extra-backend && npm run db:generate          # regenera client; sempre OK
cd hora-extra-backend && npx prisma db push           # dev SQLite, idempotente
cd hora-extra-backend && npm run db:migrate           # cria + aplica migration MySQL
cd hora-extra-backend && npm run db:studio            # GUI read-only-ish; OK
cd hora-extra-backend && npm run db:setup             # roda scripts/db-setup.ts manualmente
```

### ❌ Bloqueados (em `.claude/settings.local.json` deny list)

```bash
npx prisma migrate reset            # APAGA o banco — bloqueado
npm run db:migrate -- --reset       # mesmo efeito
```

Se você acha que precisa de reset, **pare e pergunte ao usuário**. Em dev, deletar `dev.db` à mão (fora do shell) é alternativa controlada.

## Predev / pós-clone

```bash
cd hora-extra-backend && npm install                  # instala deps
cd hora-extra-backend && npm run db:setup             # roda o rewriter baseado em .env
cd hora-extra-backend && npm run db:generate          # se db:setup não fez
```

Em MySQL: subir Docker primeiro: `docker-compose up db` na pasta `hora-extra-backend/`.

## Checklist

- [ ] Editei `prisma/schema.prisma` (não `.bak`)
- [ ] Rodei `npm run db:generate` (tipos TS atualizados)
- [ ] Em SQLite: `npx prisma db push` ou reiniciei `npm run dev`
- [ ] Em MySQL: `npm run db:migrate` com nome descritivo da migration
- [ ] Code novo usa `prisma.<modelCamelCase>` corretamente
- [ ] Não commitei `dev.db`, `node_modules`, migration aplicada já no banco mas não no repo
- [ ] Doc atualizada em `hora-extra-backend/docs/Infrastructure/` se mudou setup, ou no model docs

## Gotchas

1. **`schema.prisma.bak`** é a versão MySQL canônica restaurada pelo `db-setup.ts`. **Nunca edite.** Se precisar mudar comportamento MySQL, edite `schema.prisma` em modo MySQL e deixe o flip salvar.
2. **`prisma migrate reset` está bloqueado** — apaga dados. Não tente.
3. **TS reclamando que campo não existe** = `db:generate` faltando.
4. **`Could not find Prisma Client`** após pull = `db:generate` faltando (lockfile mudou).
5. **Migration "drifted"** (banco e migration table desalinhados) = problema de SQLite que mexeu sem migration, ou MySQL que rolou query manual. Investigar com `npx prisma migrate status` antes de tentar reset.
6. **Casing do client**: `prisma.user` (PascalCase model `User`). Com siglas: `NPC` → `prisma.nPC`. Use autocomplete.
7. **`binaryTargets`** no `generator client` inclui Linux musl pra Docker. Não remover ao editar.
8. **`onDelete: Cascade`** é poderoso — em SQLite às vezes requer `PRAGMA foreign_keys=ON`. Confira o connect string se cascade não dispara.

## Referências

- `hora-extra-backend/prisma/schema.prisma` — versão "ao vivo" (depende de USE_SQLITE)
- `hora-extra-backend/prisma/schema.prisma.bak` — versão MySQL canônica (NÃO EDITE)
- `hora-extra-backend/scripts/db-setup.ts` — script rewriter
- `hora-extra-backend/.env` — `USE_SQLITE`, `DATABASE_URL`
- `hora-extra-backend/docs/DB_DOCKER_GUIDE.md` — setup MySQL via Docker
- `CLAUDE.md` §"Database modes (SQLite vs MySQL)" — overview oficial
