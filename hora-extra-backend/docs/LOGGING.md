# Guia de Logging - Hora-Extra Backend

Este documento descreve o sistema de logging centralizado implementado no projeto para garantir robustez, observabilidade e facilidade de depuração tanto em desenvolvimento quanto em produção.

## 1. Visão Geral

O projeto utiliza a biblioteca `winston` em conjunto com `winston-daily-rotate-file` para gerenciar logs. O sistema substitui completamente o uso de `console.log` por um serviço estruturado que suporta:
- Diferentes níveis de prioridade (`error`, `warn`, `info`, `http`, `debug`).
- Metadados estruturados (módulos, IDs de jogadores, stack traces).
- Saída formatada e colorida no console.
- Persistência em arquivos com rotação automática.

## 2. O Serviço Logger

O serviço está localizado em `src/utils/Logger.ts` e exporta uma instância configurada do Winston por padrão.

### Importação
```typescript
import logger from '../utils/Logger.js';
```

### Uso Básico
```typescript
// Mensagem informativa simples
logger.info('Servidor inicializado com sucesso.');

// Log com contexto de módulo (Recomendado)
logger.info('Cliente conectado', { module: 'SOCKET', socketId: socket.id });

// Log de erro com objeto de erro (inclui stack trace no metadata)
try {
  // ... lógica ...
} catch (error) {
  logger.error('Falha crítica no processamento', { module: 'GAME', error });
}
```

## 3. Níveis de Log e Módulos

### Níveis (Winston standard)
| Nível | Descrição | Uso Típico |
| :--- | :--- | :--- |
| `error` | Erros fatais ou exceções não tratadas | Erros 500, falhas de conexão com banco |
| `warn` | Comportamentos inesperados mas não fatais | Falhas de validação, senhas incorretas, tokens expirados |
| `info` | Eventos de ciclo de vida e marcos importantes | Startup, conexões de socket, novos registros |
| `http` | Logs de requisições web | Método HTTP, URL, Status Code, Tempo de resposta |
| `debug` | Informações verbosas para depuração | Dump de estados, variáveis internas (evitar em prod) |

### Módulos Padronizados
Utilize sempre o campo `module` no metadata para facilitar a filtragem:
- `SERVER`: Inicialização de hardware/express.
- `SOCKET`: Eventos de WebSockets e Handlers.
- `HTTP`: Requisições REST via middleware.
- `AUTH`: Registro, Login e Verificação de Tokens.
- `GAME`: Lógica core do jogo e gerenciamento de estados.
- `DATABASE`: Queries e interações com Prisma.

## 4. Persistência e Arquivos

Os logs são automaticamente salvos no diretório raiz `/logs` do backend:

- **application-YYYY-MM-DD.log**: Todos os logs do nível `info` ou superior.
- **error-YYYY-MM-DD.log**: Apenas logs de nível `error` para detecção rápida de falhas.

Os arquivos são rotacionados diariamente e mantidos por um período determinado (14 dias para logs gerais, 30 dias para erros).

## 5. Configuração via Ambiente (.env)

Você pode controlar o nível de verbosidade através da variável de ambiente `LOG_LEVEL`:

```env
# Opções: error, warn, info, debug
LOG_LEVEL=info
```

Em ambiente de desenvolvimento, os logs no console são coloridos e formatados para leitura humana. Em produção, os arquivos de log mantêm a estrutura para serem facilmente consumidos por ferramentas de análise (como ELK Stack ou DataDog).

## 6. Boas Práticas
- **Nunca use `console.log`**: O `stdout` direto não é rotacionado nem estruturado.
- **Forneça contexto**: Sempre que possível, inclua o ID do jogador ou do socket no metadata.
- **Seja moderado no debug**: Logs de debug em loops de alta frequência (como o tick do jogo) podem degradar a performance se não forem controlados.

---
*Este sistema foi implementado seguindo o [Padrão de Design Backend](../docs/Arch/BACKEND_DESIGN_PATTERN.md).*
