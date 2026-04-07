# Padrão Factory no Backend

Este documento descreve a implementação do padrão **Factory** no backend do projeto **Hora-Extra**, visando desacoplamento, testabilidade e escalabilidade.

## 1. Service Factory

O `ServiceFactory` centraliza a obtenção de instâncias dos serviços de domínio.

- **Localização**: `src/core/factories/Service.Factory.ts`
- **Uso em Controllers**:
  ```typescript
  import { ServiceFactory } from '../../core/factories/Service.Factory.js';
  const authService = ServiceFactory.getAuthService();
  ```

### Vantagens:
- **Abstração**: O controlador não precisa saber como o serviço é construído ou quais suas dependências.
- **Mocks**: Fácil substituir por um mock em testes de integração visualizando apenas a Factory.

## 2. Socket Handler Factory

O `SocketHandlerFactory` gerencia a criação de tratadores específicos para eventos do Socket.IO.

- **Localização**: `src/sockets/factories/SocketHandler.Factory.ts`
- **Registro de Eventos**: Novos eventos devem ser registrados no bloco `static {}` da classe.
- **Implementação de Handlers**: Cada handler deve implementar a interface `ISocketHandler` (`src/sockets/types/SocketEvent.ts`).

### Exemplo de Handler:
```typescript
export class MyNewEventHandler implements ISocketHandler {
    public async handle(socket: Socket, io: Server, data: any): Promise<void> {
        // Lógica aqui...
    }
}
```

### Funcionamento no SocketManager:
O `SocketManager` percorre todos os eventos registrados na factory e cria um listener dinâmico. Isso mantém o arquivo `SocketManager.ts` pequeno e focado apenas no ciclo de vida da conexão.

## 3. Guia de Manutenção

- **Adicionar Novo Serviço**: 
  1. Crie o serviço em `src/services/`.
  2. Adicione um método estático `getNewService()` em `ServiceFactory`.
- **Adicionar Novo Evento Socket**:
  1. Crie a classe em `src/sockets/handlers/YourEvent.Handler.ts`.
  2. Registre na `SocketHandlerFactory` vinculando a string do evento à classe.
