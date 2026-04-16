# Etapa de Build
FROM node:22-alpine AS build

WORKDIR /app

# Instala dependências necessárias para o Prisma no Alpine
RUN apk add --no-cache openssl

# Copia arquivos de dependência
COPY package*.json ./
COPY prisma ./prisma/

# Instala todas as dependências (incluindo dev)
RUN npm install

# Gera o Prisma Client
RUN npx prisma generate

# Copia o restante dos fontes
COPY . .

# Faz o build do TypeScript
RUN npm run build

# Remove o que não é produtivo
RUN npm prune --production

# Etapa Final
FROM node:22-alpine

WORKDIR /app

# Instala dependências de runtime
RUN apk add --no-cache openssl

# Copia apenas o necessário do build anterior
COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package.json ./package.json

# Expõe as portas do backend (HTTP e UDP)
EXPOSE 5000
EXPOSE 5001/udp

# Variáveis de ambiente padrão
ENV NODE_ENV=production

# Comando para rodar a aplicação
CMD ["npm", "start"]
