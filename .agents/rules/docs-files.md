# Regras de Documentação de Arquivos

Estas regras garantem que o conhecimento técnico do projeto **Hora-Extra** seja mantido atualizado tanto no Cliente (Unity) quanto no Backend (Node.js).

## 📅 Sempre que uma Nova Feature for Implementada
- **Obrigatoriedade**: Cada nova funcionalidade (ex: Novo sistema de networking, nova mecânica de jogo, novo endpoint de API) DEVE vir acompanhada de um arquivo de documentação.
- **Localização**: 
  - **Cliente**: `hora-extra-client/Docs/<Categoria>/<Nome_da_Feature>.md`
  - **Backend**: `hora-extra-backend/docs/<Categoria>/<Nome_da_Feature>.md`
- **Conteúdo Mínimo**:
  - Descrição da funcionalidade.
  - Como ela foi implementada (detalhes técnicos).
  - Como utilizá-la (exemplo de código/uso).

## 🔄 Sempre que algo for Modificado
- **Atualização**: Se uma funcionalidade existente sofrer alterações que mudem seu comportamento, API ou estrutura de dados, o arquivo de documentação correspondente DEVE ser atualizado no mesmo commit/conversa.
- **Consistência**: Garanta que os nomes de variáveis, endpoints e payloads documentados reflitam exatamente o código atual.

## 📁 Estrutura de Pastas Padrão
As pastas de documentação devem seguir esta hierarquia:

- `docs/Networking`: Protocolos de comunicação, WebSockets, APIs.
- `docs/Mechanics`: Lógicas de jogo, sistemas de movimento, combate, etc.
- `docs/Arch`: Arquitetura do sistema, padrões de design (ex: Singleton, EventBus).
- `docs/Infrastructure`: Setup, builds, CI/CD, variáveis de ambiente.

## 🛠️ Documentações Avulsas
- Documentos como `COMMUNICATION.md` localizados na raiz devem ser movidos para as pastas `docs/Networking` de ambos os projetos (ou mantidos de forma espelhada/referenciada) para garantir visibilidade direta aos desenvolvedores de cada módulo.
