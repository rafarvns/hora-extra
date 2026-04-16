# Walkthrough - Implementação de Logs de NPC

Esta tarefa consistiu em adicionar logs de rastreamento para a movimentação de NPCs no backend, facilitando a depuração de sincronização entre clientes.

## Alterações Realizadas

### 1. Backend (`hora-extra-backend`)

#### [NpcMove.Handler.ts](file:///e:/PUC/hora-extra/hora-extra-backend/src/sockets/handlers/NpcMove.Handler.ts)
- Adicionado `logger.debug` para cada pacote de movimento recebido. Isso permite ver o tráfego bruto quando o `LOG_LEVEL` estiver definido como `debug`.
- Aumentada a frequência do log de `info` de cada 50 pacotes para cada 20 pacotes.
- Refinada a mensagem de log para incluir a posição atual do NPC de forma mais clara.

## Como Testar

1.  Certifique-se de que o backend está rodando.
2.  Inicie o cliente Unity e movimente-se para spawnar ou ativar NPCs.
3.  Observe os logs no console do backend:
    - Você verá mensagens `[NPC_SYNC]` a cada 20 atualizações de posição de cada NPC.
    - Se desejar ver TODOS os pacotes, defina `LOG_LEVEL=debug` no seu arquivo `.env` ou variável de ambiente.

## Verificação Técnica
- [x] O código compila sem erros (`npm run build`).
- [x] A lógica de broadcast não foi alterada, garantindo que a funcionalidade principal permanece intacta.
