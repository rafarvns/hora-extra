---
description: Padrão de Código C# (Unity) - Hora-Extra
---

# Padrão de Código C# (Unity) - Hora-Extra

Este documento estabelece as normas de estilo e boas práticas de programação em C# para garantir a legibilidade, performance e consistência no projeto "Hora-Extra".

## 1. Convenções de Nomenclatura
- **Classes e Métodos**: Use `PascalCase`. (ex: `PlayerController`, `ShootProjectile()`)
- **Variáveis Públicas/Serializadas**: Use `PascalCase`. (ex: `MoveSpeed`, `CurrentHealth`)
- **Campos Privados/Protegidos**: Use `_camelCase` (prefixando com underscore). (ex: `_rigidbody`, `_isDead`)
- **Parâmetros de Funções e Variáveis Locais**: Use `camelCase`. (ex: `targetPosition`, `distance`)
- **Constantes**: Use `SCREAMING_SNAKE_CASE`. (ex: `MAX_PLAYERS`)

## 2. Padrões Unity
- **Header e Tooltip**: Sempre use `[Header("Nome")]` e `[Tooltip("Explicação")]` para organizar variáveis no Inspector.
- **SerializeField**: Prefira usar `[SerializeField] private` em vez de `public` para variáveis que precisam ser editadas no editor mas não acessadas por outras classes.
- **Component Access**:
    - Cachear referências (ex: `GetComponent<Rigidbody>()`) em `Awake` ou `Start`.
    - **NUNCA** use `GetComponent` ou `GameObject.Find` dentro do `Update` ou `FixedUpdate`.
- **Tags e Camadas**: Utilize `CompareTag("Player")` em vez de `tag == "Player"` para economizar memória (GC).

## 3. Organização de Scripts
Minimize o acoplamento entre scripts:
- Utilize o **Padrão Observer** (System.Action ou UnityEvent) para eventos de gameplay (ex: o `PlayerHealth` dispara um evento e a `UIManager` o escuta).
- Scripts devem ter uma única responsabilidade (Single Responsibility Principle).

## 4. Performance e Alocação
- Evite `StopAllCoroutines()` a menos que seja estritamente necessário.
- Use `Vector3.sqrMagnitude` para comparações de distância quando a precisão absoluta não for necessária (é muito mais rápido que `Vector3.Distance`).
- Tome cuidado com alocações frequentes no `Update` (Strings concatenadas, novos arrays).

---
*Este documento é uma Regra de Agente (Rule) e deve ser mantido sempre atualizado com as melhores práticas da equipe.*
