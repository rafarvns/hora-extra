# Desenvolvimento Backend com TDD e Pensamento Sequencial - Hora-Extra

Este documento impõe o uso sistemático de testes automatizados e raciocínio estruturado no desenvolvimento do backend do "Hora-Extra".

## 1. Regra de Ouro: TDD (Test-Driven Development)
- Todo novo recurso, handler de socket ou lógica de negócio no `hora-extra-backend` **DEVE** ser precedido por um teste unitário.
- **Fluxo TDD**:
  1.  **RED**: Escreva um teste que falhe para o comportamento desejado.
  2.  **GREEN**: Desenvolva o código mínimo para que o teste passe.
  3.  **REFACTOR**: Limpe o código, otimize a performance e garanta que os testes continuem verdes.

## 2. Pensamento Sequencial (Sequential-Thinking)
- Antes de tocar no código, utilize a ferramenta `sequential-thinking` para planejar a implementação.
- **Etapas Obrigatórias**:
  - Detalhar os possíveis payloads de entrada (happy path).
  - Listar edge cases (invalid input, overflow, desconexão).
  - Desenhar o fluxo de dados entre os handlers e o StateManager.
  - Verificar se o plano atende aos requisitos do `COMMUNICATION.md`.

## 3. Padrão de Testes
- **Infraestrutura**: Utilize uma biblioteca de testes moderna (ex: Vitest ou Jest) com suporte nativo a ESM e TypeScript.
- **Localização**: Arquivos de teste devem estar em uma pasta `__tests__` ou ter o sufixo `.test.ts`.
- **Foco**: Priorize testes que validem a "verdade" do servidor (cálculos de movimento, regras de colisão, entrada/saída em salas).

## 4. O que testar obrigatoriamente
- Registros de novos jogadores em salas (ex: limite de jogadores por sala).
- Validação de pacotes de rede (ex: strings vazias, números fora do range).
- Cálculo de ticks de rede (ex: se o estado é de fato emitido no intervalo correto).
- Sanity checks de movimento.

## 5. Práticas de Código
- Escreva código testável: evite dependências globais ou acoplamento excessivo com a biblioteca de sockets.
- Injete dependências (Dependency Injection) para burlar o socket.io durante os testes unitários se necessário.

---
*Este documento é uma Regra de Agente (Rule) e deve ser seguido sem exceções. O backend é o coração autoritário do jogo e deve ser robusto.*
