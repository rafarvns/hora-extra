# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Layout (Monorepo)

This repo bundles a multiplayer game ("Hora-Extra", a PUC Minas TCC project) as a monorepo with two independently-buildable workspaces plus shared docs/rules:

- `hora-extra-backend/` — Node.js + TypeScript authoritative game server (Express REST + native UDP datagram sockets, Prisma ORM).
- `hora-extra-client/` — Unity 6 (`6000.3.12f1`) C# client.
- `.agents/rules/` and `.agents/workflows/` — Project-specific rules (see "Rules" below) that govern how features are implemented. Read these before non-trivial work.
- `documents/`, `guias/`, `arte/` — Design docs, Git guides, and visual assets.

The two workspaces are independent npm/Unity projects — there is no root `package.json`. Always `cd` into the relevant workspace before running commands.

## Backend (`hora-extra-backend/`)

### Commands

```bash
npm install
npm run dev          # nodemon + ts-node ESM loader; runs predev (clean + db:setup) first
npm run build        # rimraf dist && tsc
npm start            # node dist/index.js
npm test             # vitest run (single pass)
npm run test:watch
npm run test:coverage
npm run db:generate  # prisma generate
npm run db:migrate   # prisma migrate dev (MySQL)
npm run db:studio    # prisma studio
```

Run a single test file: `npx vitest run src/services/authService.test.ts`. Tests live alongside source with a `.test.ts` suffix; `vitest.config.ts` includes `src/**/*.test.ts` and `tsconfig.json` excludes them from the build.

### Database modes (SQLite vs MySQL)

`scripts/db-setup.ts` (run automatically by `predev`) rewrites `prisma/schema.prisma` based on `USE_SQLITE` in `.env`:
- `USE_SQLITE=true` — flips the provider to `sqlite`, sets `DATABASE_URL=file:./dev.db`, runs `prisma generate` + `prisma db push`. The MySQL schema is backed up to `prisma/schema.prisma.bak`.
- `USE_SQLITE=false` — restores the MySQL schema from the backup and runs `prisma generate` only (migrations are not run automatically; use `npm run db:migrate` after starting the Docker MySQL via `docker-compose up db`).

Do not edit `schema.prisma.bak` manually — it is the canonical MySQL version restored by the setup script.

### Architecture

Entry point `src/index.ts` boots an Express HTTP server on `PORT` (default 5000) **and** a separate UDP datagram server on `UDP_PORT` (default 5001). HTTP handles auth/room CRUD; UDP handles real-time gameplay sync.

- **REST API** — `src/api/routes/index.ts` aggregates routes under `/api` (`/health`, `/auth`, `/rooms`). Controllers extend `BaseController` (`src/core/BaseController.ts`) and respond via `sendSuccess`/`sendCreated`. Errors throw `ApiError` instances (`src/core/ApiError.ts`) and are funneled through `next(error)` → `middleware/errorHandler.ts`. Protected methods use the `@Authorize()` decorator from `src/core/decorators/Authorize.ts` (requires `experimentalDecorators` — already on in `tsconfig.json`).
- **Service Factory** — Controllers obtain services via `ServiceFactory` (`src/core/factories/Service.Factory.ts`), never by direct import. Add new services to this factory.
- **UDP Sockets** — `UdpSocketManager` (`src/sockets/UdpSocketManager.ts`) is a singleton initialized once from `index.ts` and accessed via `getInstance()`. It owns the per-client `PlayerSession` map keyed by `address:port` and exposes `sendTo`, `broadcastToRoom`, `getSession`.
  - Packet shape on the wire: `{ e: eventName, d: data, token? }`.
  - First packet from any client must be `e: 'CONN'` with a JWT in `token` (the manager replies with `CONN_SUCCESS` or `CONN_ERROR`). Subsequent packets are rejected with `ERROR` until a session exists.
  - Dev bypass: when `NODE_ENV=development` and the client sends `token === DEV_TEST_TOKEN`, the session is created with `playerId = DEV_TEST_USER_ID` and auto-joins room `"dev-room"`. A `data.resetRoom: true` field on CONN wipes that room's sessions and NPC state — useful when iterating in the Unity editor.
  - Inactive sessions (>30s without packets) are reaped every 10s.
- **Socket Handler Factory** — `SocketHandlerFactory` (`src/sockets/factories/SocketHandler.Factory.ts`) maps event names (`join_room`, `player_move`, `npc_move_request`, `npc_register`, `player_sprint`, `ping`) to handler classes implementing `ISocketHandler` (`src/sockets/types/SocketEvent.ts`). To add a new UDP event: create a `*.Handler.ts` in `src/sockets/handlers/`, then register it in this factory's static block — do **not** add `if/else` chains to `UdpSocketManager`.
- **Logging** — All logging goes through `src/utils/Logger.ts` (Winston, daily-rotated). Never use `console.log`. Always include `{ module: 'NAME' }` metadata (e.g. `'HTTP'`, `'UDP_SOCKET'`, `'AUTH'`).
- **ESM imports** — The package is `"type": "module"`. TS source must import other source files with the `.js` extension (e.g. `import './foo.js'`), not `.ts`. NodeNext resolution requires this even though the file on disk is `.ts`.

### Authoritative-server contract

The server is the source of truth: validate every client-supplied position/input before applying it, never echo raw client data. The wire protocol uses compact JSON keys (e.g. `p` for position, `r` for rotation) on high-frequency events to save bandwidth. Whenever you change a UDP event payload **or** add a new event, update `hora-extra-backend/docs/Networking/COMMUNICATION.md` in the same change — this file is the cross-language contract with the Unity client.

## Client (`hora-extra-client/`)

Unity 6 project (`6000.3.12f1`). Open via Unity Hub; main scene is `Assets/Scenes/SCN_Main.unity`. Scripts under `Assets/Scripts/` are split by domain: `AI/`, `Characters/`, `Network/`, `UI/`.

- **Networking** — `SocketManager` (`Assets/Scripts/Network/SocketManager.cs`) is the singleton (`DontDestroyOnLoad`) UDP client mirroring the backend's packet shape. It defaults to `127.0.0.1:5001` and to `UseTestToken=true` with the dev bypass token. Socket callbacks fire on a worker thread — marshal all GameObject/component access back to the main thread via the `_mainThreadQueue` pattern already in place.
- **REST** — `Assets/Scripts/Network/Rest/ApiClient.cs` wraps `UnityWebRequest` + `Newtonsoft.Json` against `http://127.0.0.1:5000/api`. Service classes live in `Rest/Services/` (`AuthService`, `RoomService`, `HealthService`); DTOs in `Network/Models/`.
- **Event constants** — Never hardcode event-name strings in gameplay code; declare them once in `Assets/Scripts/Network/NetworkEvents.cs` and reference the constant.
- **No unit tests** — Per `.agents/rules/no-unit-test-on-unity.md`, do not create `Tests/` folders, `*.Tests.cs` files, or any Unity Test Framework suites in this project. Validate manually in Play Mode with `Debug.Log`/`LogWarning`/`LogError`. This rule is exclusive to the Unity client; backend tests are required.

## Rules (`.agents/rules/`) — enforced project conventions

Read the relevant rule file before working in that area. Highlights:

- **`backend-design-pattern.md`** — Authoritative server, 20 Hz fixed broadcast tick, centralized state, `Logger` only (no `console.log`), compact payload keys.
- **`backend-factory-pattern.md`** — Services and socket handlers must be obtained from their respective factories; files end in `.Factory.ts`.
- **`backend-unit-tests.md`** — Backend follows TDD (Red → Green → Refactor). New services/handlers need `.test.ts` coverage.
- **`client-design-pattern.md`** — Singleton `SocketManager`, Observer pattern for gameplay listeners (subscribe in `OnEnable`, unsubscribe in `OnDisable`), snapshot interpolation (`Vector3.Lerp`) — do not teleport networked objects.
- **`csharp-coding-standards.md`** — `PascalCase` for classes/methods/public fields, `_camelCase` for private fields, `SCREAMING_SNAKE_CASE` for constants. `[SerializeField] private` over `public`. Cache `GetComponent` in `Awake`/`Start`. Use `CompareTag`, not `tag ==`.
- **`communication-sync-rule.md`** — Any network-event/payload change must update `hora-extra-backend/docs/Networking/COMMUNICATION.md` in the same commit; the C# DTOs, TypeScript types, and this doc must stay in lockstep.
- **`docs-files.md`** — New backend features document into `hora-extra-backend/docs/<Category>/`; new client features into `hora-extra-client/Docs/<Category>/`. Categories: `Networking`, `Mechanics`, `Arch`, `Infrastructure`.
- **`git-monorepo-workflow.md`** — Commit messages in Portuguese, imperative mood, scoped: `feat(client):`, `feat(backend):`, `fix(client):`, `fix(backend):`, `docs:`, `assets:`, `chore:`. One atomic concern per commit — do not mix backend logic with Unity asset changes.
- **`unity-asset-management.md`** — Asset prefixes: `PFB_` prefabs, `SPR_` sprites, `MAT_` materials, `SO_` ScriptableObjects, `SCN_` scenes.

`.agents/workflows/` contains step-by-step recipes (`backend.md`, `client.md`, planner variants) describing the expected order of operations for a feature; consult them for non-trivial tasks.

## Claude Code agents (`.claude/`)

Configuração de agentes especializados em `.claude/`, independente de `.agents/`
(que continua sendo referência humana das rules — `.claude/` re-encoda em skills
para consumo automático).

- **Slash command:** `/feature <descrição>` — orquestra skill-selector → planner →
  executor → (test-runner | manual-verifier) → reviewer → linter → git-agent.
  Salva plano em `.claude/plans/NNNN-<slug>.md`.
- **Agentes:** ver `.claude/agents/*.md`. Cada um tem responsabilidade única e
  protocolo HANDOFF.
- **Skills:** ver `.claude/skills/*/SKILL.md`. Catálogo de 14 skills (7 backend,
  4 client, 3 cross-repo) com templates, gotchas e ciclos TDD (quando backend).
- **Branching backend vs client:** `target=backend` roda Vitest via test-runner.
  `target=client` roda manual-verifier (checklist Play Mode para humano) — Unity
  não tem testes automatizados por design (`.agents/rules/no-unit-test-on-unity.md`).
- **Sem write de Git:** git-agent é no-op. Commits manuais com prefixos
  `feat(backend):` / `feat(client):` / `docs:` / `assets:` / `chore:`.

Este `CLAUDE.md` permanece a fonte primária de arquitetura — skills referenciam
de volta a ele para convenções gerais.
