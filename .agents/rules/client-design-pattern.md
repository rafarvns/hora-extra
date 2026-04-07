# Padrão de Design Cliente-Servidor (Unity) - Hora-Extra

Este documento define as diretrizes arquiteturais para a implementação de rede no cliente Unity, garantindo um código desacoplado, performático e escalável para o modo co-op.

## 1. Arquitetura do Singleton (SocketManager)
- A conexão de rede é gerenciada exclusivamente pela classe `SocketManager`.
- **Singleton**: Utilize o padrão Singleton rigoroso com `DontDestroyOnLoad` para garantir persistência entre cenas.
- **Namespace**: Todas as classes de rede devem residir em `Assets/Scripts/Network/`.

## 2. Centralização de Strings (NetworkEvents)
- **Hardcoding Proibido**: Nunca use strings literais para nomes de eventos (ex: "player_move").
- **NetworkEvents.cs**: Todos os identificadores de eventos devem ser constantes estáticas em `NetworkEvents.cs`.
- **Separação**: Diferencie claramente eventos de saída (C->S) e entrada (S->C) no arquivo.

## 3. Threading e Sincronização
- **Main Thread**: O Socket.IO opera em threads separadas. Sempre utilize `OnUnityThread` do `SocketIOUnity` para manipular GameObjects ou componentes do Unity.
- **Update Loop**: Evite lógica de negócio pesada dentro dos callbacks de rede. Prefira atualizar variáveis de estado e processá-las no `Update` ou `FixedUpdate`.

## 4. Modelagem de Dados e DTOs
- **Serialization**: Utilize `Newtonsoft.Json` para parsing.
- **Payloads**: Crie classes C# (Data Transfer Objects - DTOs) em uma pasta `Models/` para representar os dados recebidos/enviados.
- **CamelCase vs snake_case**: Siga o padrão do servidor (geralmente camelCase para propriedades JSON) mapeando com `[JsonProperty("name")]` se necessário.

## 5. Padrão Observer para Gameplay
- Componentes de Gameplay (ex: PlayerController, EnemyAI) **não** devem se inscrever diretamente no `SocketManager`.
- **Events Dispatcher**: O `SocketManager` (ou uma classe intermediária) deve expor `System.Action` ou `UnityEvents`.
- **Assinatura**: Scripts de gameplay se inscrevem nesses eventos no `OnEnable` e desinscrevem no `OnDisable`.

## 6. Sincronização de Estado (Networking Professional)
- **Snapshot Interpolation**: O cliente deve tratar o `state_update` como um snapshot. Não mova o objeto instantaneamente; interpole a posição (`Vector3.Lerp`) entre o estado atual e o alvo.
- **Prediction (Opcional)**: Para o jogador local, use "Client-Side Prediction" para evitar sensação de lag, reconciliando a posição se o servidor divergir significativamente.

## 7. Logs e Debugging
- Padronize os logs com o prefixo `[NETWORK]`.
- Mantenha um log limpo: Diferencie `Debug.Log`, `Debug.LogWarning` e `Debug.LogError` para facilitar a filtragem no console do Unity.

---
*Este documento é uma Regra de Agente (Rule) e deve ser seguido em todas as implementações de rede.*
