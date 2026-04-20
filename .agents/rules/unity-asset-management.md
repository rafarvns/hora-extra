---
description: Gestão de Assets no Unity - Hora-Extra
---

# Gestão de Assets no Unity - Hora-Extra

A organização da pasta `Assets` é vital para evitar perda de tempo e referências quebradas. Este documento dita a hierarquia e nomenclatura oficial.

## 1. Hierarquia de Pastas Obrigatória
Todo o conteúdo do projeto deve residir em subpastas descritivas:
- `Assets/Scripts/`: Separado por domínio (Network, Gameplay, UI).
- `Assets/Prefabs/`: Todos os prefabs prontos para uso.
- `Assets/Art/`: Subdividido em `Sprites`, `Materials`, `Fonts`.
- `Assets/Scenes/`: Cenas do jogo e de teste.
- `Assets/Plugins/`: Bibliotecas externas (ex: SocketIOUnity, Newtonsoft).

## 2. Nomenclatura de Arquivos (Prefixos)
Use prefixos para facilitar a busca rápida no editor (CTRL+P):
- `PFB_`: Prefabs (ex: `PFB_Player`, `PFB_Enemy_Zombie`)
- `SPR_`: Sprites/Imagens (ex: `SPR_Logo`, `SPR_Tileset_Office`)
- `MAT_`: Materiais (ex: `MAT_Floor_Concrete`)
- `SO_`: ScriptableObjects (ex: `SO_PlayerStats`)
- `SCN_`: Cenas (ex: `SCN_MainMenu`, `SCN_Level01`)

## 3. Boas Práticas com Prefabs
- **NUNCA** faça modificações permanentes em uma instância de prefab diretamente na cena. Sempre abra o `Prefab Mode` ou aplique as mudanças no asset original.
- Utilize **Nested Prefabs** (Prefabs dentro de Prefabs) para sistemas complexos como a interface do jogador.

## 4. Otimização de Assets
- Verifique as configurações de compressão de Sprites (Max Size 2048, Compressão High Quality).
- Evite duplicar assets. Se um material é igual, use o mesmo em todos os objetos.

---
*Este documento é uma Regra de Agente (Rule). Uma pasta Assets limpa é sinônimo de um projeto profissional.*
