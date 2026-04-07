# Guia de Testes Unitários - Hora-Extra Backend

Este documento orienta sobre como escrever e executar testes unitários no backend do projeto **Hora-Extra**.

## 🛠️ Tecnologias Utilizadas
- **[Vitest](https://vitest.dev/)**: Um framework de testes rápido e nativo para ESM, similar ao Jest.
- **Node.js + TypeScript**.

## 🚀 Como Executar os Testes

No terminal, dentro da pasta `hora-extra-backend`:

### 1. Rodar todos os testes uma vez:
```bash
npm run test
```

### 2. Rodar em modo Watch (Re-executa ao salvar):
```bash
npm run test:watch
```

### 3. Gerar relatório de cobertura (Coverage):
```bash
npm run test:coverage
```
O relatório detalhado será gerado na pasta `/coverage`.

## ✍️ Como Escrever novos Testes

1. Crie um arquivo com a extensão `.test.ts` ao lado do arquivo que deseja testar.
   - Exemplo: `authService.ts` -> `authService.test.ts`.

2. Use os blocos `describe` para agrupar e `it` (ou `test`) para definir o caso de teste.

### Exemplo de Estrutura:

```typescript
import { describe, it, expect } from 'vitest';
import meuServico from './meuServico.js';

describe('MeuServico', () => {
    it('deve fazer algo planejado', () => {
        const resultado = meuServico.executar(1, 2);
        expect(resultado).toBe(3);
    });
});
```

## 📐 Padrões e Boas Práticas
- **Isolamento**: Testes unitários não devem acessar o banco de dados real. Use mocks se necessário.
- **Nomenclatura**: Os nomes dos testes devem ser claros e em português (ou inglês, desde que consistente).
- **Foco**: Teste a lógica de negócio dos *Services*. Evite testar Controllers (que seriam testes de integração).

---
*Dúvidas? Consulte o time de desenvolvimento.*
