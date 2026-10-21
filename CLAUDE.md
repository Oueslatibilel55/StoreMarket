# CLAUDE.md

Context for AI assistants working in this repository. Read this first, then the docs it links to.

## What this is

An **affiliate marketplace** (stores sell products; affiliates earn traceable coins per sale, converted to money) with an **Angular 22** frontend and a **.NET 10** backend (ASP.NET Core Minimal APIs, Clean Architecture + DDD tactical patterns, PostgreSQL).
The backend and database setup mirror the RichLife project. The product goal and domain are described in [docs/CONTEXT.md](docs/CONTEXT.md).

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 / C# 14, `global.json` pins SDK 10.0.302 |
| Solution | `backend/WebApp.slnx` |
| API | ASP.NET Core Minimal APIs, ProblemDetails |
| Orchestration | .NET Aspire 9.5.2 (`WebApp.AppHost` + `WebApp.ServiceDefaults`) |
| Database | PostgreSQL 17 via Npgsql EF Core 10, snake_case names (`EFCore.NamingConventions`) |
| Auth | JWT access token (15 min, in memory) + rotating refresh token in an httpOnly cookie, BCrypt (ADR 0004) |
| API docs | Built-in OpenAPI + Scalar UI at `/scalar` (dev only) |
| Observability | OpenTelemetry via ServiceDefaults (OTLP → Aspire dashboard) |
| Testing | xUnit v3 (`xunit.v3.mtp-v2`) on Microsoft.Testing.Platform; integration tests on a real PostgreSQL via Testcontainers |
| Frontend | Angular 22 (standalone, signals, zoneless) at `http://localhost:4200`, Vitest |
| UI | Tailwind CSS v4, design tokens + design-system utilities in `src/styles.css` (ADR 0003, 0005) |
| i18n | Transloco, runtime switch **FR (default) / AR (RTL) / EN**, files in `frontend/public/i18n/` |

## Repository map

| Path | Contents |
|---|---|
| `backend/` | .NET solution, `docker-compose.yml` (Postgres + pgAdmin) |
| `frontend/` | Angular workspace |
| `docs/` | Project context, architecture, conventions, decisions (ADRs) |
| `features/` | One folder per feature: spec, tasks and status. **Start here for any new work.** |

## Commands

```bash
# Backend (from backend/) — Docker must be running
# One-time: JWT signing key for local dev (never commit it)
dotnet user-secrets set "Jwt:Secret" "<random string, 32+ chars>" --project src/WebApp.Api
dotnet user-secrets set "BootstrapAdmin:Emails:0" "<your email>" --project src/WebApp.Api   # become admin when you sign in

dotnet run --project src/WebApp.AppHost         # Postgres + API (:5185) + Aspire dashboard
dotnet build
dotnet test                                     # integration tests start their own Postgres container

# Without Aspire: database from docker-compose (host port 5433), then the API alone
docker compose up -d postgres pgadmin           # pgAdmin on http://localhost:5051
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=webapp;Username=webapp;Password=webapp_dev" --project src/WebApp.Api
dotnet run --project src/WebApp.Api

# EF Core migrations (from backend/)
dotnet ef migrations add <Name> --project src/WebApp.Infrastructure --startup-project src/WebApp.Api --output-dir Migrations
dotnet ef database update      --project src/WebApp.Infrastructure --startup-project src/WebApp.Api

# Demo users in the local DB, through the real API (API must be running; Node 24)
node scripts/seed-dev-users.mjs

# Frontend (from frontend/) — requires Node 24 (`nvm use 24.21.0`, see `frontend/.nvmrc`)
npm install
npm start                                       # proxies /api to :5185
```

Ports (chosen to not clash with RichLife): API **5185**, Aspire Postgres **62750**, compose Postgres **5433**, pgAdmin **5051**, Aspire dashboard **15256**.

### Deployed (free tier, ADR 0006) — like RichLife

Code: `https://github.com/Oueslatibilel55/StoreMarket`. Each push to `main` redeploys both halves. Never commit secrets.

| Part | Host | Notes |
|---|---|---|
| Frontend | **Netlify** — `netlify.toml` at the repo root | Builds `frontend/` (Node 24), publishes `dist/webapp/browser`, **proxies `/api/*` and `/uploads/*` to Render** (same origin: no CORS, the refresh cookie works) and falls back to `index.html`. |
| API | **Render** web service `https://storemarket.onrender.com` | Docker, root directory `backend`, Dockerfile `src/WebApp.Api/Dockerfile`, region Frankfurt. Free tier **sleeps after ~15 min idle**; the first request then takes 30–60 s. If the service URL differs, update both proxies in `netlify.toml`. |
| Database | **Neon** Postgres 17, `eu-central-1` | Started **empty** (migrations applied at startup). Images are stored here too (`file_blobs`). |

Render environment variables (values only in Render): `ASPNETCORE_ENVIRONMENT=Production`, `Jwt__Secret`,
`ConnectionStrings__DefaultConnection` (Npgsql key=value form, Neon's **direct** host — not `-pooler` — with
`SSL Mode=Require`), `Database__MigrateOnStartup=true`, `Storage__Provider=Database`, `BootstrapAdmin__Emails__0`
(the first admin), optional `DemoSeed__Password` (test data: see below). `/health` and `/alive` answer in production. Build the image locally with
`docker build -f src/WebApp.Api/Dockerfile .` from `backend/`.

**Test data on the hosted app:** when `DemoSeed__Password` is set, `DemoSeed/DemoSeeder.cs` creates once, at startup and through the application services, one account per role (`leila.gharbi` client, `sana.trabelsi` affiliate, `karim.bensalah` and `amel.zouari` store owners, all `@storemarket.demo`, password = that variable), the categories, two stores with 4 products each, an accepted partnership and an order with the affiliate code (one store delivered). The admin is the BootstrapAdmin account, which must exist first. Skipped once the demo client exists. Data: `DemoSeed/demo-seed.json` + images, rebuilt by `node backend/scripts/demo-data/export-hosted-seed.mjs`.


## Architecture rules

Dependencies point **inward only**: `Api -> Application -> Domain`, `Infrastructure -> Application`. Domain references nothing (no EF, no ASP.NET, no DI).

- **Entities** derive from `BaseEntity` (Guid v7 id, `CreatedAt`, `UpdatedAt`); aggregate roots from `AggregateRoot` and raise events via `RaiseDomainEvent`.
- **Private setters + static factories** on entities (`Thing.Create(...)`); EF uses a private parameterless constructor.
- **Domain returns `Result` / `Result<T>`** for expected failures; exceptions are for bugs.
- **Persistence:** one `IEntityTypeConfiguration<T>` per entity in `Infrastructure/Persistence/Configurations`. Repositories in `Infrastructure/Repositories`, interfaces in `Application/Interfaces`.
- **Commit through `IUnitOfWork.CommitAsync`**, never `SaveChanges` directly — it dispatches domain events after the save.
- Guid keys are set by the domain; `AppDbContext` marks them `ValueGenerated.Never`. Don't undo that.
- **Connection string:** `ConnectionStrings:webapp` (injected by Aspire) else `ConnectionStrings:DefaultConnection`. Never put secrets in `appsettings.json`.
- **Migrations** are explicit locally; `Database:MigrateOnStartup=true` only on deployed services.
- Endpoints live in `WebApp.Api/Endpoints/<Feature>Endpoints.cs`; services in `WebApp.Application/Services`.
- **Errors are codes, not messages**: domain/application return `Result.Fail("user.emailInvalid")`; `ResultExtensions.ToProblem()` maps the code to an HTTP status and returns ProblemDetails with a `code` extension; the frontend translates `errors.<code>`. Add every new code to `DomainErrors` / `*Errors` **and** to the three i18n files.
- **Auth:** endpoints under `/api/auth` (`register`, `login`, `refresh`, `logout`, `me`). Protect an endpoint with `.RequireAuthorization()`; read the user id with `HttpContext.User.GetUserId()`. Roles travel as `role` claims (`Buyer`, `Affiliate`, `StoreOwner`, `Admin`). Auth endpoints are rate limited per IP.
- **Roles & policies:** `.RequireAuthorization(AuthPolicies.Admin | StoreOwner | Affiliate)`. A role change only reaches the client after a token refresh. Admin endpoints live under `/api/admin/*`; an admin can never change their own account.
- **Account types & role requests (027):** sign-up creates a **client** (Buyer) or an **affiliate** account (Affiliate + code, no Buyer). No role is self-service: extra roles go through `RoleRequest` (`/api/me/role-requests`) and an admin approves / refuses (`/api/admin/role-requests`). An account always keeps a base role (Buyer or Affiliate). Affiliate is never granted directly — only sign-up or an approved request, because the user must accept the terms.
- **First admin:** emails in `BootstrapAdmin:Emails` (user-secrets / env var) become Admin on sign-up or sign-in — the only way to create an admin.
- **Audit every admin action:** call `IAuditLog.Record(actorEmail, actorId, "<area>.<action>", target, id, label, before, after, reason)` before `CommitAsync` — it is saved in the same transaction. Snapshot DTOs, not entities. Skip no-ops. Add the action code to `audit.actions.*` in the three i18n files.
- **Platform settings (004):** business values (coin rate, affiliate % limits, payout minimum, billing days, code format…) are never hard-coded: add the key to `PlatformSettingCatalog` (type, default, limits, unit, group) **and** `settings.<key>.label/hint` to the three i18n files, then read it with `IPlatformSettings.GetIntAsync / GetDecimalAsync` (cached). When a value must stay fixed for a record (order line %, ledger rate, invoice), copy it onto that record — a setting change never rewrites history.
- **Images (006):** upload through `POST /api/images` (purpose decides folder and allowed role), stored through `IFileStorage` — on disk locally (static files at `/uploads`), in the `file_blobs` table when hosted (`Storage:Provider=Database`, ADR 0006, same URLs) — the DB keeps the path. Entities store the image **id**, check it with `ImageService.RequireAsync(id, purpose)` and expose `ImageUrls.For(path)`. A new entity referencing images must be added to `ImageRepository.IsInUseAsync` and get a restrict FK. Never build a storage path from user input. Frontend: `<app-image-upload purpose="…" [url]="…" (changed)="…" />`.
- **Stores (007):** one per StoreOwner (`/api/me/store`, `StoreOwner` policy, owner id from the token — never from the body). Cities and delivery zones are governorate codes (`Governorates`, frontend `CITIES`, labels `cities.<code>`). Public shop reads go through `ICatalogQueries`; only `Active` / `Closed` stores are public. Slugs for any entity use `Common.Slug`.
- **Product categories (008):** a tree (≤ 3 levels) loaded whole into `CategoryTree` for rule checks. A product fills the fields of its category **and its ancestors** — read them with `ICatalogQueries.GetProductCategorySchemaAsync(slug)`. Field and choice **keys** are the stable identifiers (store product values by key); a field's type never changes.
- **Products (009):** owned by a store, managed at `/api/me/store/products` (owner from the token). `Product.Create/Update` validate values against the schema (category + ancestors); values are canonical strings by field key; variants = up to 2 choice axes, ids kept across edits. Never hard-delete a product (soft delete — orders point to it). Anything that removes schema (category, field, choice) must check `Product.UsesAttribute / UsesOption` first. Public reads: only Published, not deleted, store Active/Closed.
- **Public shop (011):** every public product read goes through `CatalogQueries.VisibleProducts()` (published, not deleted, store Active/Closed) — change visibility there, not per endpoint. Pages set their title with `Seo.set(name, description)` / `Seo.setKey('shop.…')`.
- **Moderation (010):** admin disable / enable / delete of stores and products goes through `ModerationService` (`/api/admin/stores`, `/api/admin/products`), always with a reason (`Store.MaxModerationReasonLength`) and audited. A product's admin disable (`DisabledAt`) is separate from its owner status; a deleted store refuses owner writes (`store.deleted`). Revoking StoreOwner disables the store (`Store.OwnerRoleRevokedReason`, shown with `<app-moderation-reason>`). New public reads must keep going through `VisibleProducts()` so disabled products stay hidden.
- **Partnerships & codes (012):** an affiliate earns on a store only through an **accepted `Partnership`** (`/api/me/partnerships` for the affiliate, `/api/me/store/partnerships` for the owner — owner/affiliate id from the token, someone else's partnership is a 404; `/api/admin/partnerships` to end one, audited). Whether a code may be used on a product is decided **only** by `AffiliateCodeService.ValidateAsync(code, productId, clientId)` (active affiliate via `User.CanEarnAsAffiliate`, visible product, accepted partnership, not the store owner, not the client) — the cart (013) and checkout (014) must call it, never re-implement it. `POST /api/affiliate-codes/check` is rate limited (`RateLimitPolicies.AffiliateCode`). Frontend: `<app-partnership-cta [storeId] [sharePath] />` for affiliates; share links carry `?ref=CODE`.
- **Cart (013):** signed-in cart on the server (`/api/me/cart`, one `Cart` per user), guest cart in `localStorage` shown through `POST /api/cart/preview` and merged at sign-in — the frontend always goes through `CartService` (`core/cart`), never the API directly. The cart stores **only** product, variant, quantity and code: prices, stock, availability and code validity are recomputed on every read by `CartService` (issues `CartIssues.*`, code via `AffiliateCodeService`). Checkout (014) must re-check with the same rules. Share-link codes are remembered per store by `Referrals`.
- **Orders (014):** placed only through `CheckoutService.PlaceAsync` (`POST /api/orders`, guest allowed): it re-checks every line with `CartRules` and every code with `AffiliateCodeService.MatchAsync`, enforces the store's delivery zones, then takes stock and saves the order inside `IUnitOfWork.InTransactionAsync` (conditional `TryTakeStockAsync`, never read-then-write). An `OrderLine` is a **frozen snapshot** (name, options, price, affiliate, partnership, %, expected coins; coin value on the `Order`) — never recompute it from the catalogue. One `StoreOrder` per store.
- **Order lifecycle (015 / 016):** every status change goes through `Order.ChangeStatus` (rules in `Order.IsAllowed` per `OrderActorRole`) via `OrderLifecycleService`, which applies the side effects in the same transaction: payment through `IPaymentMethod` on Delivered, stock back on Cancelled / NotDelivered (and re-taken if an admin re-opens). **Delivered is final** — never add a way out of it (corrections go to the ledger, 019). Coins are credited by `CoinLedgerService.CreditDeliveredAsync` **inside** that transaction — not by an event handler (after-commit handlers may fail silently).
- **Coin ledger (019):** `coin_entries` is **append-only** — never update or delete an entry; corrections are `CoinEntry.Adjustment` (signed, reason, audited). Every write goes through `ICoinLedger.AppendAsync`, which also increments `coin_accounts` atomically and refuses to run outside `IUnitOfWork.InTransactionAsync`; the cached balance must always equal the sum of the entries (tests check it). Coins = line total × % ÷ coin value, all frozen on the order line / order — never recompute from current settings. Expected coins are computed from open store orders, never written. `store_orders` uses `xmin` as concurrency token: a conflict surfaces as `order.changed`; migrations must never add / drop an `xmin` column.
- **Payouts (021):** every request goes through `PayoutService` (`/api/me/payouts`, `/api/admin/payouts`). A request locks its coins with `CoinEntry.PayoutLocked` **in the same transaction**, after `ICoinLedger.LockBalanceAsync` (row lock on `coin_accounts`): never check a balance outside that lock. Rejected / cancelled requests write `CoinEntry.PayoutReleased`; Paid writes nothing (the coins are already gone). The amount is frozen on the request. One open request per affiliate (service check + filtered unique index). Steps live on `PayoutRequest` (Verify / Reject / Schedule / MarkPaid / Cancel); admin steps are audited (`payout.*`, target `Payout`); `payout_requests` uses `xmin` → `payout.changed`. Covered commissions are computed oldest-first (`PayoutQueries.CoveredAsync`), never stored.
- **Affiliate wallet (020):** `/affiliate/wallet` is read-only over the ledger. List, export and breakdown share one filter (`CoinQueries.Filter`), so a new filter goes there. The CSV is built by the client in the active language (`;`, BOM, formula-safe cells); the server returns JSON capped at `CoinLedgerService.MaxExportRows`. The breakdown counts `Earned` entries only. Client phones stay masked in every affiliate view.
- **Admin reads** go through `IAdminQueries` (Infrastructure, `AsNoTracking`, projections, `PagedResult<T>` via `Paging.From(page, pageSize)`); writes go through services + repositories.
- **Never return the refresh token in JSON** and never store the access token in `localStorage` — see ADR 0004.

## Frontend rules

- **No hard-coded UI text**: every string goes through Transloco (`{{ 'key' | transloco }}`) and is added to **all three** files `public/i18n/{fr,ar,en}.json`.
- **RTL-safe Tailwind**: use logical utilities (`ms-*`, `me-*`, `ps-*`, `pe-*`, `start-*`, `end-*`, `text-start`) — never `ml-*`, `mr-*`, `left-*`, `text-left`. Arabic flips `<html dir="rtl">` automatically (`LanguageService`).
- **Colors via tokens** (`bg-brand`, `text-ink`, `text-muted`, `border-line`…) defined in `src/styles.css` `@theme`; no raw hex in templates.
- **Design system (ADR 0005)**: build screens from the utilities in `src/styles.css` — `page` / `page-title`, `card` / `card-interactive` / `card-header`, `btn btn-primary|secondary|ghost|danger`, `field-label` + `field`, `alert-danger`, `empty-state`, `pill pill-success|warning|danger|brand|neutral`, `code-chip`, `nav-link`. `rounded-xl`, `shadow-card` → `hover:shadow-card-hover`, no `rounded-2xl`. Add a utility (and a row in the ADR) rather than repeating a long class string. Every screen must work at **320 px** with no horizontal scroll (ADR 0005, mobile rules).
- Prices are always TND, formatted with the active locale; numbers, phone numbers and codes like `#DRF-45` stay `dir="ltr"`.
- **Auth on the client:** `AuthService` (signals: `user()`, `isAuthenticated()`, `hasRole()`), `authInterceptor` adds the bearer token and silently refreshes once on 401, `authGuard` / `guestGuard` / `roleGuard('Admin')` protect routes. The session is restored at startup from the refresh cookie.
- **Admin screens** live in `features/admin/` (lazy `ADMIN_ROUTES` under `AdminLayout`). Enable a "soon" entry in `admin-layout.ts` when its feature ships. List filters live in the URL (router input binding — missing params arrive as `undefined`, so don't rely on input defaults). Load data with `rxResource`. Confirm actions with `shared/ui/modal`, never `window.confirm`. Reuse `shared/ui/pagination` and `role-badge`.
- **Names typed by the admin** (categories…) come as `LocalizedName { fr, ar, en }` — French required, the others optional. Display with `{{ x.name | localized: language.lang() }}` (falls back to French); never translate them through Transloco.
- Show API errors with `errorKey(err)` (`core/http/problem.ts`) → `{{ key | transloco }}`.
- Dates / numbers: pass `language.locale()` (`fr-TN`, `ar-TN`, `en`) to Angular pipes.
- Run frontend commands with Node 24: `nvm use 24.21.0` (or prefix PATH with the nvm v24 folder).

## Working rules

1. **Feature-first.** Before coding a feature, create `features/<NNN-name>/` from `features/_template/`. Update its `TASKS.md` as you go.
2. Warnings are errors (`Directory.Build.props`). Don't suppress one without a comment saying why.
3. Every new endpoint gets an integration test; every domain rule gets a unit test.
4. Record significant technical decisions as an ADR in `docs/decisions/`.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/CONVENTIONS.md](docs/CONVENTIONS.md) for details.
