---
name: client-unity-asset-prefixes
description: Aplicar quando criar/renomear asset no Unity (`hora-extra-client/Assets/`). Define hierarquia obrigatória de pastas e prefixos `PFB_/SPR_/MAT_/SO_/SCN_`.
applies_to: client
---

# client-unity-asset-prefixes — Convenção de pastas e prefixos no Unity

## Quando aplicar

- Criar prefab, sprite, material, ScriptableObject, scene
- Renomear asset existente que não segue o padrão
- Antes de commitar — checagem visual

## Quando NÃO aplicar

- Scripts C# (`.cs`) — esses seguem outra convenção (`csharp-coding-standards.md`)
- Plugins externos (`Assets/Plugins/`) — preservar nomenclatura do upstream
- Arquivos de configuração Unity (`*.asset` em `ProjectSettings/`)

## Hierarquia de pastas obrigatória

```
hora-extra-client/Assets/
├── Scripts/           ← Código C# por domínio
│   ├── Network/       ← SocketManager, ApiClient, NetworkEvents, Models
│   ├── Characters/    ← Player, NPC controllers
│   ├── AI/            ← Behaviors NPC
│   ├── UI/            ← Canvas controllers, modals
│   └── ...
├── Prefabs/           ← TODOS os prefabs (PFB_*)
├── Art/
│   ├── Sprites/       ← SPR_*
│   ├── Materials/     ← MAT_*
│   └── Fonts/         ← Font files (sem prefixo, são raros)
├── Scenes/            ← SCN_* (cenas do jogo + cenas de teste)
└── Plugins/           ← Bibliotecas externas (SocketIOUnity, Newtonsoft, etc.)
```

> ⚠️ **Não inventar pastas raiz novas** (`Assets/Resources/`, `Assets/Common/`, etc.) sem alinhar com o usuário. Unity tem pastas com semântica especial (`Resources/`, `Editor/`, `StreamingAssets/`, `Plugins/`) — não use por engano.

## Prefixos obrigatórios

| Tipo                | Prefixo | Exemplo                            | Pasta          |
| :------------------ | :------ | :--------------------------------- | :------------- |
| Prefab              | `PFB_`  | `PFB_Player.prefab`                | `Prefabs/`     |
| Sprite / textura    | `SPR_`  | `SPR_Logo.png`, `SPR_Tile_Office`  | `Art/Sprites/` |
| Material            | `MAT_`  | `MAT_Floor_Concrete.mat`           | `Art/Materials/` |
| ScriptableObject    | `SO_`   | `SO_PlayerStats.asset`             | varia por uso (`Assets/ScriptableObjects/` ou junto da feature) |
| Scene               | `SCN_`  | `SCN_Main.unity`, `SCN_Lobby.unity` | `Scenes/`     |

### Sub-naming

Combine prefixo com descrição em `PascalCase` separada por `_`:

```
PFB_Player                       ← prefab básico
PFB_NPC_Boss                     ← NPC com subtipo
PFB_UI_LoginModal                ← UI prefab
SPR_Tile_Office_Floor            ← sprite com hierarquia
MAT_Chair_Wood                   ← material por contexto
SO_PlayerStats_Default           ← ScriptableObject variante
SCN_Lobby                        ← cena
```

**Não** usar `kebab-case` ou `snake_case` no nome (só `_` como separador hierárquico). **Não** começar com número.

## Casos especiais

### Cenas de teste

Mesma pasta `Scenes/`, prefixo `SCN_Test_` ou sufixo `_Test`:

```
SCN_Test_Networking.unity
SCN_Networking_Test.unity
```

Não esconda em pasta separada — Unity Editor lista todas.

### Variantes de prefab

Use sufixo descritivo:

```
PFB_Player.prefab            ← base
PFB_Player_Sprinter.prefab   ← variante
```

Se a variante usa `Prefab Variants` do Unity 2019+, preserve referência (não converta em duplicate à mão).

### ScriptableObjects perto da feature

Aceitável colocar `.asset` ScriptableObject ao lado do script que o consome (`Assets/Scripts/AI/SO_NpcBehavior_Patrol.asset`). Não obrigatório centralizar.

### Nested prefabs

`PFB_UI_LoginModal` contém `PFB_UI_Button_Confirm` como child prefab. **Sempre** referencie como nested prefab; nunca duplique.

## Asset metadata (`.meta`)

Cada asset Unity tem um `.meta` gêmeo com GUID, importer settings, label. **SEMPRE commite os `.meta` junto** do asset.

```
PFB_Player.prefab           ← commit
PFB_Player.prefab.meta      ← commit junto
```

Esquecer um `.meta` quebra referências cruzadas no projeto inteiro. Reviewer flagga.

## Renomeação

Unity rastreia assets por GUID (`.meta`). **Renomeie via Editor**, não direto no filesystem — assim o `.meta` segue e referências em scenes/prefabs não quebram.

> Se você renomear no filesystem por engano: abra Unity, refresh, ele detecta como "novo asset" + "asset perdido". Recuperação: restaurar nome via git, abrir Unity, depois renomear pelo Editor.

## Otimização (mencionar quando relevante)

Da rule `unity-asset-management.md`:

- Sprites: Max Size 2048, Compressão High Quality
- Materiais: reusar mesmo MAT em vários GameObjects (não duplicar)
- Prefabs: usar Prefab Mode pra editar (`Open Prefab`), não modificar instância na scene

Mas essas são otimizações de runtime, fora do escopo "criar o asset". Mencione só se a tarefa pedir.

## Checklist

- [ ] Asset está na pasta correta (`Prefabs/`, `Art/Sprites/`, `Scenes/`, etc.)
- [ ] Prefixo correto pelo tipo (`PFB_`, `SPR_`, `MAT_`, `SO_`, `SCN_`)
- [ ] Nome em `PascalCase` com `_` como separador hierárquico
- [ ] `.meta` companion gerado e commitado junto
- [ ] Não criou pasta nova com nome reservado do Unity por engano
- [ ] Se renomeou: feito via Unity Editor (preserva GUID)

## Gotchas

1. **`Assets/Resources/`** carrega TUDO em memória em runtime. Usar com extrema cautela; geralmente desnecessário com Addressables. Não criar sem motivo.
2. **`Assets/Editor/`** = scripts só-editor. Não use pra runtime. Se um script depende disso e é referenciado em runtime, build quebra.
3. **`Assets/StreamingAssets/`** = arquivos lidos direto do disk no build. Use só pra conteúdo que precisa estar **fora do bundle**.
4. **`.meta` ausente** = referência fantasma em outros assets. Sempre verificar via `git status` se `.meta` ficou unstaged.
5. **Caso vs. case**: Windows é case-insensitive, mas Unity Editor e git case-sensitive. `PFB_player.prefab` ≠ `PFB_Player.prefab` para o Unity em macOS/Linux. Use casing consistente.
6. **`SCN_*.unity` esquecido no `Build Settings`** = build não inclui a cena. Edit > Build Settings > Add Open Scenes.
7. **Prefabs com referência cruzada de scripts deletados** = Missing Script no inspector. Restaurar GUID via git ou reconstruir referência manualmente.

## Referências

- `hora-extra-client/Assets/` — estrutura real do projeto
- `hora-extra-client/Assets/Scenes/SCN_Main.unity` — cena principal
- `.agents/rules/unity-asset-management.md` — rule humana
