# Backend Factory Pattern Rule

Este documento define as diretrizes para a implementação do padrão **Factory** no backend do projeto **Hora-Extra**. O objetivo é aumentar a robustez, desacoplamento e facilidade de teste do sistema.

## Diretrizes Gerais

1.  **Centralização de Instanciação**: Nenhuma classe de "serviço" ou "handler" de lógica complexa deve ser instanciada diretamente em controladores ou gerenciadores de socket. Use sempre uma Factory.
2.  **Nomenclatura**: Arquivos de factory devem terminar com `.Factory.ts` (ex: `Service.Factory.ts`).
3.  **Encapsulamento**: A Factory deve ser a única responsável por conhecer as dependências necessárias para instanciar um objeto.

## Tipos de Factory Recomendados

### 1. Service Factory (`src/core/factories/Service.Factory.ts`)
Responsável por instanciar e fornecer serviços. 
- Deve gerenciar singletons quando apropriado.
- Deve centralizar a passagem de dependências (como instâncias do Prisma).

### 2. Socket Handler Factory (`src/sockets/factories/Handler.Factory.ts`)
Responsável por retornar a lógica de tratamento para diferentes eventos de Socket.IO.
- Evita que o `SocketManager` cresça indefinidamente com `if/else` ou vários `socket.on`.
- Permite que cada evento tenha seu próprio arquivo de handler.

## Exemplo de Estrutura

```typescript
// src/core/factories/Service.Factory.ts
export class ServiceFactory {
  private static services = new Map<string, any>();

  public static getService<T>(serviceName: string): T {
    // Lógica de cache/instanciação
  }
}
```

## Benefícios Esperados
- **Testabilidade**: Facilita a substituição de implementações reais por Mocks.
- **Manutenibilidade**: Mudanças no construtor de um serviço afetam apenas a Factory, não todos os controladores.
- **Escalabilidade**: Adicionar novos tipos de handlers ou serviços segue um padrão claro.

## Documentação
Toda nova Factory implementada deve ser documentada em `hora-extra-backend/docs/ARCHITECTURE.md` ou arquivo específico de pattern.
