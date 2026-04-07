# Guia de Configuração: Banco de Dados e Docker

Este documento explica como configurar e gerenciar o banco de dados MySQL e os containers do backend "Hora-Extra".

## Requisitos
- [Docker Engine](https://docs.docker.com/get-docker/) instalado e rodando.
- [Docker Compose](https://docs.docker.com/compose/install/) (já incluso no Docker Desktop).

---

## 1. Subindo o Ambiente (Docker Compose)

O Docker Compose configurado gerencia dois serviços: `db` (MySQL) e `app` (o backend).

```bash
# Sobe os containers (recomendado fechar outros terminais de npm dev)
docker-compose up -d --build
```
> [!TIP]
> O banco de dados estará acessível em `localhost:3306` com usuário `user` e senha `password`.

---

## 2. Gerenciando o Banco (Prisma)

Sempre que alterar o arquivo `prisma/schema.prisma`, use os comandos abaixo:

### Migrations (Novas Tabelas/Campos)
Este comando cria uma nova migration no banco local e gera o cliente Prisma.
```bash
npm run db:migrate -- "nome_da_minha_migracao"
```

### Visualização Gráfica (Prisma Studio)
Abre uma interface web para gerenciar os dados (jogadores, etc.) visualmente no navegador.
```bash
npm run db:studio
```

---

## 3. Rodando sem Docker (Local Dev)

Se preferir rodar apenas o banco no Docker e o código via `npm run dev`:

1. Garanta que o container `db` esteja rodando: `docker-compose up -d db`
2. Configure seu `.env` com: `DATABASE_URL="mysql://user:password@localhost:3306/hora_extra_db"`
3. Rode as migrations: `npm run db:migrate -- init`
4. Inicie o app: `npm run dev`

---

## Estrutura de Tabelas Inicial
A tabela **Player (`jogadores`)** possui:
- `id`: UUID (chave primária)
- `nome`: Nome visível do jogador
- `email`: Único para cada cadastro
- `senha`: Hash da senha
- `nivel`: Nível atual no jogo (default 1)
- `xp`: Experiência acumulada
- `cadastradoEm`/`atualizadoEm`: Timestamps automáticos
