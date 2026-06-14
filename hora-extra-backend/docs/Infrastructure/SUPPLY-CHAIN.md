# Segurança de Supply Chain (npm)

Práticas adotadas no `hora-extra-backend` para mitigar ataques de cadeia de
suprimentos no ecossistema npm (worms self-propagantes tipo **Shai-Hulud**,
**Mini Shai-Hulud** e **Miasma**, que roубam credenciais durante o `npm install`
via hooks `preinstall`/`postinstall` e a técnica *"phantom gyp"* com `binding.gyp`).

## Resumo da postura atual

| Controle | Estado |
|---|---|
| `package-lock.json` versionado (lockfileVersion 3) | ✅ |
| Todos os pacotes resolvidos de `registry.npmjs.org` com `integrity` (SRI) | ✅ |
| `save-exact=true` no `.npmrc` (novas deps pinadas sem `^`) | ✅ |
| `npm audit` | ✅ 0 vulnerabilidades |
| Pacotes comprometidos conhecidos na árvore | ❌ nenhum |
| `binding.gyp` / hooks de install suspeitos em `node_modules` | ❌ nenhum (só Prisma, legítimo) |

## Regras de instalação

### Use `npm ci`, não `npm install`

`npm ci` instala **exatamente** o que está no `package-lock.json` e **falha** se o
lock divergir do `package.json`. É a defesa concreta contra re-resolução silenciosa
de versões — que é a janela explorada pelos worms quando uma nova versão maliciosa
é publicada dentro do range `^`.

```bash
npm ci          # instalação reproduzível (CI e setup limpo)
npm install     # APENAS ao adicionar/atualizar uma dependência de propósito
```

### Adicionando uma dependência nova

Como `.npmrc` tem `save-exact=true`, `npm install <pkg>` grava a versão **exata**
(ex.: `"1.2.3"`), não `"^1.2.3"`. Para evitar puxar uma versão recém-publicada
(a maioria dos pacotes maliciosos é despublicada em horas/dias), prefira um pacote
com alguns dias de "maturação":

```bash
# instala a versão mais recente publicada ATÉ a data (cooldown manual)
npm install <pkg> --before="$(date -d '7 days ago' +%Y-%m-%d)"
```

> npm 10 não tem cooldown rolante nativo (`--before` é por data). Se um dia migrarmos
> para pnpm, usar `minimumReleaseAge`.

### Instalação com hardening máximo (opcional)

O principal vetor dos worms é o lifecycle script de dependências. Para uma
instalação que **não executa** esses scripts:

```bash
npm run install:safe   # = npm ci --ignore-scripts && npm run db:generate
```

Isso é seguro neste projeto porque a única dependência que precisa de postinstall
é o **Prisma**, e o `prisma generate` é rodado explicitamente (pelo `db:generate` e
pelo `predev` em `scripts/db-setup.ts`) — não dependemos do hook de install dele.

> ⚠️ **Não** colocar `ignore-scripts=true` global no `.npmrc`: além de bloquear
> deps, o npm também pula os hooks `pre*`/`post*` dos **nossos próprios** scripts
> (ex.: `predev`), o que quebraria o setup do banco. Por isso o controle é um
> comando opt-in, não config global.

## Em caso de incidente (suspeita de pacote comprometido)

1. **Não rode `npm install`.** Pare CI/CD.
2. Verifique a árvore contra os IOCs do incidente:
   ```bash
   grep -iE "<nome-do-pacote>" package-lock.json
   find node_modules -name binding.gyp           # vetor "phantom gyp"
   ```
3. Se houver match, **rotacione credenciais** que estiveram na máquina/CI:
   tokens GitHub, chaves SSH, segredos cloud (AWS/GCP/Azure), tokens de publish npm,
   conteúdo de `.env`.
4. Limpe e reinstale de um lock conhecido-bom: `rm -rf node_modules && npm ci`.

## Referências

- Wiz — *Miasma: Supply Chain Attack Targeting RedHat npm Packages*
- StepSecurity — *Phantom Gyp: npm worm via `binding.gyp`*
- Unit 42 (Palo Alto) — *The npm Threat Landscape*
