# Manual de Movimentação FPS - Hora-Extra

Este documento detalha o funcionamento e a configuração do sistema de movimentação em primeira pessoa para o jogador.

## Componentes Necessários

Para que o Player possua movimentação funcional no Unity Editor, o GameObject deve conter os seguintes scripts:
1. **`CharacterController`** (Nativo do Unity): Gerencia a física de colisão.
2. **`Player.cs`**: Gerencia a vida e o estado lógico do jogador.
3. **`PlayerController.cs`**: Gerencia os inputs (`WASD` + `Mouse`) e aplica o movimento.

## Hierarquia Sugerida no Inspector

```text
Player (GameObject)
├── [CharacterController]
├── [Player.cs]
├── [PlayerController.cs]
└── Main Camera (GameObject) - Adicionado como filho
```

No script `PlayerController`, arraste o objeto da **Camera** para o campo `_playerCamera` para habilitar a rotação vertical (olhar para cima/baixo).

## Funcionamento Técnico

### 1. Sistema de Input
Utiliza a API `UnityEngine.InputSystem` em sua forma direta para capturar o estado do teclado (`Keyboard.current`) e do mouse (`Mouse.current`). Isso garante compatibilidade com o novo sistema de input do Unity sem a necessidade obrigatória de criar um `Input Action Asset` para protótipos rápidos.

### 2. Mouse Look (Rotação)
- **Rotação Horizontal**: Aplicada diretamente ao `Transform` do jogador (Eixo Y).
- **Rotação Vertical (Pitch)**: Aplicada apenas à Câmera filha, com limites (*Clamping*) entre -80 e +80 graus, evitando que a câmera gire em torno de si mesma.

### 3. Movimentação e Gravidade
- O movimento WASD é calculado em relação ao `forward` e `right` do jogador, permitindo movimentação relativa à direção para onde se olha.
- A **Gravidade** é aplicada constantemente através do `CharacterController.Move()`. O valor padrão é `-9.81f`, configurável no Inspector.

### 4. Controle de Morte
O `PlayerController` monitora o campo `_isDead` de `CharacterBase`. Quando a vida chega a zero:
- A movimentação e a rotação da câmera são interrompidas.
- O cursor do mouse é desbloqueado e torna-se visível novamente, permitindo a interação com telas de Game Over ou Menus.

## Variáveis do Inspector

| Variável | Descrição | Valor Padrão |
| :--- | :--- | :--- |
| `_walkSpeed` | Velocidade linear do movimento WASD | `6.0` |
| `_lookSensitivity` | Sensibilidade de giro do mouse | `0.1` |
| `_gravity` | Intensidade da queda gravitacional | `-9.81` |
| `_upperLookLimit` / `_lowerLookLimit` | Limita o quanto o jogador pode olhar para cima ou para baixo | `80` / `-80` |
