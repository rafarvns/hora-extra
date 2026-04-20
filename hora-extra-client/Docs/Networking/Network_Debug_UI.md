# Network Debug UI

Este sistema permite monitorar o estado da rede e visualizar os logs do console da Unity diretamente na interface do jogo, facilitando o diagnóstico em builds de deploy onde o console da Unity não está disponível.

## Setup
1. Crie um GameObject vazio (ex: `Network`) na cena.
2. Adicione o script `NetworkUI` a este objeto.
3. Configure a hierarquia de UI como filhos deste objeto:
   - `pingImage` (Image)
   - `pingText` (TextMeshProUGUI)
   - `logDebug` (TextMeshProUGUI)
4. O script tentará capturar essas referências automaticamente pelo nome caso não existam.

## Funcionalidades

### 1. Monitoramento de Ping
O script se comunica com o `SocketManager` para obter a latência atual (RTT).
- **Verde (< 80ms)**: Conexão excelente.
- **Laranja (80ms - 180ms)**: Conexão instável ou distante.
- **Vermelho (> 180ms)**: Conexão ruim.
- **Cinza**: Desconectado.

### 2. Log Interceptor
Captura todas as chamadas de `Debug.Log`, `Debug.LogWarning` e `Debug.LogError` da aplicação.
- Os logs são exibidos com cores correspondentes à sua severidade.
- Mantém um buffer circular (padrão 15 linhas) para evitar sobrecarga de memória e queda de performance no TextMeshPro.
- Inclui timestamps para facilitar o rastreio de eventos.

## Configurações no Inspector
- `Max Log Lines`: Define quantas linhas de log serão mantidas na tela simultaneamente.
- `UI References`: Permite configurar manualmente os objetos caso os nomes na hierarquia sejam diferentes do padrão.
