# WebApp — affiliate marketplace

Online marketplace in **TND** where business owners open stores and sell their products, and **affiliates** bring clients with a personal code (e.g. `#DRF-45`). When an order is delivered, the affiliate earns coins (1 coin = 1 TND) paid out by the platform. Cash on delivery. UI in **French, Arabic (RTL) and English**.

- Product context and every decision taken so far: [docs/CONTEXT.md](docs/CONTEXT.md)
- Feature backlog and status: [features/README.md](features/README.md)
- Rules for contributors and AI assistants: [CLAUDE.md](CLAUDE.md)

Stack: Angular 22 + Tailwind v4 · .NET 10 Minimal APIs (Clean Architecture) · PostgreSQL 17 · .NET Aspire.

## Online

The demo runs for free like RichLife: **Netlify** (frontend) → **Render** (API, Docker) → **Neon** (PostgreSQL). Each push to `main` redeploys. Setup, environment variables and limits: [CLAUDE.md → Deployed](CLAUDE.md) and [ADR 0006](docs/decisions/0006-hosting.md).

## Prerequisites

- .NET SDK 10.0.302+
- Docker Desktop (PostgreSQL and the integration tests run in containers)
- Node.js **24** (`nvm use 24.21.0`, see `frontend/.nvmrc`) and npm
- `dotnet-ef` tool (`dotnet tool install -g dotnet-ef`)

## Getting started

```bash
# 1. Backend — one-time JWT signing key for local dev (never commit it)
cd backend
dotnet user-secrets set "Jwt:Secret" "<random string, 32+ chars>" --project src/WebApp.Api
dotnet user-secrets set "BootstrapAdmin:Emails:0" "<your email>" --project src/WebApp.Api   # become admin when you sign in

# 2. Backend + database (Aspire starts Postgres, the API on :5185 and a dashboard)
dotnet run --project src/WebApp.AppHost

# 3. Apply the database migrations (new terminal, from backend/, while the AppHost runs)
dotnet ef database update --project src/WebApp.Infrastructure --startup-project src/WebApp.Api

# Optional: demo users + a real-looking catalogue (10 stores, 61 products) — see backend/scripts/dev-users.txt
# Catalogue source: backend/scripts/demo-data/catalog.mjs, photos: backend/scripts/demo-data/images (rebuild with --reset-catalog)
node scripts/seed-dev-users.mjs

# 4. Frontend (new terminal)
cd frontend
npm install
npm start                                # UI on http://localhost:4200
```

Open http://localhost:4200, sign up, and you land on **My account**. API docs: http://localhost:5185/scalar.
No Aspire? See the docker-compose commands in `CLAUDE.md`.

## Tests

```bash
cd backend && dotnet test        # unit + integration (starts a throw-away PostgreSQL container)
cd frontend && npm test          # Vitest
```

## Structure

```
.
├── CLAUDE.md            # AI / contributor context (start here)
├── docs/                # Context, architecture, conventions, ADRs
├── features/            # Feature specs & task tracking (build order 000 → 025)
├── backend/             # .NET 10 solution (Clean Architecture)
│   ├── docker-compose.yml   # Postgres + pgAdmin
│   ├── src/
│   │   ├── WebApp.AppHost         # Aspire orchestration
│   │   ├── WebApp.ServiceDefaults # OpenTelemetry, health checks
│   │   ├── WebApp.Domain          # Entities, rules, Result, error codes
│   │   ├── WebApp.Application     # Use-case services, DTOs, interfaces
│   │   ├── WebApp.Infrastructure  # EF Core + PostgreSQL, JWT, BCrypt, migrations
│   │   └── WebApp.Api             # Minimal API endpoints
│   └── tests/
│       ├── WebApp.UnitTests
│       └── WebApp.IntegrationTests  # Testcontainers PostgreSQL
└── frontend/            # Angular 22 app
    ├── public/i18n/     # fr.json, ar.json, en.json
    └── src/app/
        ├── core/        # Auth, i18n, HTTP helpers (singletons)
        ├── shared/      # Header, language switcher, reusable UI
        └── features/    # Lazy-loaded pages (home, auth, account…)
```
