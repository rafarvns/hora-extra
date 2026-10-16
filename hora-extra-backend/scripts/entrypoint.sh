#!/bin/sh
set -e

# Garante que o schema Prisma e o client estejam coerentes com o modo de banco
# escolhido ANTES de subir o servidor.
#
# Por que os dois sentidos do sed: `scripts/db-setup.ts` reescreve o provider do
# schema.prisma em tempo de desenvolvimento, então o arquivo versionado pode estar
# em qualquer uma das duas variantes. O `schema.prisma.bak` (usado pelo db-setup
# para restaurar o MySQL) é gitignored e não existe dentro da imagem, então aqui o
# provider é normalizado direto no schema.
SCHEMA=./prisma/schema.prisma

if [ "$USE_SQLITE" = "true" ]; then
  echo "[DOCKER-ENTRYPOINT] Configurando ambiente para SQLite..."
  sed -i 's/provider = "mysql"/provider = "sqlite"/' "$SCHEMA"
  npx prisma generate
  npx prisma db push --accept-data-loss
else
  echo "[DOCKER-ENTRYPOINT] Configurando ambiente para MySQL..."
  sed -i 's/provider = "sqlite"/provider = "mysql"/' "$SCHEMA"
  npx prisma generate

  # `depends_on` do compose não espera o MySQL ficar pronto pra aceitar conexão,
  # então o migrate deploy pode chegar antes do banco subir. Tenta algumas vezes.
  i=1
  until npx prisma migrate deploy; do
    if [ "$i" -ge 10 ]; then
      echo "[DOCKER-ENTRYPOINT] migrate deploy falhou após $i tentativas — abortando."
      exit 1
    fi
    echo "[DOCKER-ENTRYPOINT] Banco indisponível (tentativa $i/10) — novo retry em 3s..."
    i=$((i + 1))
    sleep 3
  done
fi

# Inicia a aplicação
exec npm start
