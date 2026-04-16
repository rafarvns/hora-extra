#!/bin/sh

# Se USE_SQLITE for true, precisamos garantir que o schema e o client estejam sincronizados
# dentro do container antes de iniciar o servidor.
if [ "$USE_SQLITE" = "true" ]; then
  echo "[DOCKER-ENTRYPOINT] Configurando ambiente para SQLite..."
  # No container de produção, usamos node para rodar o setup se ele estiver compilado,
  # ou usamos npx prisma diretamente se o schema for simples.
  
  # Como estamos em um container produtivo, o ideal é que o db-setup 
  // tenha sido executado ou que tenhamos as ferramentas.
  # Para simplificar no Docker, vamos apenas garantir o client e o banco:
  sed -i 's/provider = "mysql"/provider = "sqlite"/' ./prisma/schema.prisma
  npx prisma generate
  npx prisma db push --accept-data-loss
fi

# Inicia a aplicação
exec npm start
