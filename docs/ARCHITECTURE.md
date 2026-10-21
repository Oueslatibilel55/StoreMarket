# Architecture

## Overview

```
Browser ──> Angular (:4200) ──/api──> ASP.NET Core API (:5185) ──> PostgreSQL 17
                                                     (all started by WebApp.AppHost / Aspire)
```

In development the Angular dev server proxies `/api` and `/health` to the backend (`frontend/proxy.conf.json`), so no CORS is needed locally. The backend also allows `http://localhost:4200` via the `Cors:AllowedOrigins` setting.

## Backend — Clean Architecture

| Project | Responsibility | References |
|---|---|---|
| `WebApp.Domain` | `Common/` (BaseEntity, AggregateRoot, IDomainEvent, Result), `Entities/`, `Enums/`, `Events/`. No framework dependencies. | — |
| `WebApp.Application` | `Services/` (use cases), `DTOs/`, `Interfaces/` (repositories, IUnitOfWork, IDomainEventDispatcher), `Extensions/AddApplication`. | Domain |
| `WebApp.Infrastructure` | `Persistence/` (AppDbContext, UnitOfWork, `Configurations/`), `Repositories/`, `Events/` (DomainEventDispatcher), `Migrations/`, `Extensions/AddInfrastructure`. PostgreSQL via Npgsql. | Application |
| `WebApp.Api` | Minimal API endpoints, composition root, OpenAPI + Scalar. | Application, Infrastructure, ServiceDefaults |
| `WebApp.ServiceDefaults` | OpenTelemetry, health checks, service discovery, HTTP resilience. | — |
| `WebApp.AppHost` | Aspire orchestration: Postgres container + API. | Api |

Application and Infrastructure each expose an `AddXxx()` extension (in `Extensions/`) called from `Program.cs`.

### Request flow

`Endpoint (Api)` → `Service (Application/Services)` → `Aggregate (Domain)` + `Repository interface` → `IUnitOfWork.CommitAsync` (save, then dispatch domain events)

### Cross-cutting

- Errors: `ProblemDetails` (RFC 9457) via `AddProblemDetails()` + `UseExceptionHandler()`
- OpenAPI document at `/openapi/v1.json` (Development only)
- Health: `/health`, `/alive` (ServiceDefaults) and `/api/ping`
- API reference UI: `/scalar` (Development only)
- Error codes: `Result.Fail("<code>")` → `ResultExtensions.ToProblem()` → ProblemDetails with `code` (status chosen from the code)

### Authentication (ADR 0004)

```
POST /api/auth/login ──> 200 { accessToken (JWT 15 min), user }  + Set-Cookie: webapp_refresh (httpOnly, SameSite=Strict, path /api/auth, 14 days)
API call            ──> Authorization: Bearer <accessToken>
401 (expired)       ──> POST /api/auth/refresh (cookie) ──> new access token + rotated cookie ──> replay the call
old cookie replayed ──> whole token family revoked (stolen-token detection)
```

- `AuthService` (Application) holds the rules; `JwtTokenService` + `BCryptPasswordHasher` (Infrastructure) implement `ITokenService` / `IPasswordHasher`.
- Refresh tokens are stored as SHA-256 hashes in `refresh_tokens`; users in `users` (roles as `integer[]`); role requests in `role_requests` (one pending per user + role, partial unique index); changed platform settings in `platform_settings` (catalog defaults in `Domain/Settings`, cached by `PlatformSettingsStore`); store categories in `store_categories` (names FR/AR/EN); stores in `stores` (one per owner, `delivery_cities text[]`); product categories in `product_categories` with their fields in `product_attributes` (choices as `jsonb`); products in `products` (values `jsonb` by field key, photos `uuid[]`, soft delete, admin disable `disabled_at` + `moderation_reason`) and `product_variants`; stores carry the same moderation columns plus `deleted_at` (010); partnerships in `partnerships` (one requested or accepted per affiliate + store, partial unique index — 012) , carts in `carts` / `cart_lines` (one per user, one line per product + variant — 013), orders in `orders` → `store_orders` → `order_lines` (frozen snapshot, sequence `order_number_seq` — 014), status history in `store_order_events` with an `xmin` concurrency token on `store_orders` (016), the coin ledger in `coin_entries` (append-only) with cached totals in `coin_accounts` (019), affiliate code deactivation in `users.affiliate_code_disabled_at`; uploaded images in `images` (path only — the files are on disk under `Storage:RootPath`, served as static files at `/uploads`, behind `IFileStorage`). Public shop reads go through `ICatalogQueries`, admin reads through `IAdminQueries`.
- Auth endpoints are rate limited per IP (`RateLimiting:AuthPerMinute`, default 10).
- Cart calls that check codes (add, set code, `POST /api/cart/preview`, merge) are rate limited — `RateLimiting:CartPerMinute`, default 60.
- `POST /api/affiliate-codes/check` is rate limited per user (or per IP for visitors) — `RateLimiting:AffiliateCodePerMinute`, default 30.
- Authorization policies `Admin`, `StoreOwner`, `Affiliate` (role claims). First admin from `BootstrapAdmin:Emails`. Blocking an account revokes all its refresh tokens.

### Admin & audit

- `/api/admin/*` requires the Admin policy. Reads use `IAdminQueries` (no tracking, projections, paging); writes use services.
- Every admin change writes an `audit_entries` row (actor, action code, target, reason, before/after JSON) in the **same transaction** via `IAuditLog`. Append-only; failed and no-op actions are not logged.

## Frontend — Angular

- Standalone components only, no NgModules
- Zoneless change detection + **signals** for state
- `core/` — app-wide singletons: `auth/` (AuthService, interceptor, guards), `i18n/`, `http/` (error → translation key), `ApiService`
- `shared/` — reusable UI: `layout/` (header), `language-switcher/`, `ui/` (modal, pagination, role-badge), `i18n/localized` (admin-typed names per language, French fallback)
- `core/catalog/` — public shop API (`CatalogApi`); `core/stores/` — store owner API (`StoreApi`) and the governorate list
- `features/shop/` — public shop: `/products`, `/products/:slug`, `/stores`, `/stores/:slug` (+ `shared/catalog` cards, `core/seo`)
- `features/my-store/` — the store owner's pages (`/my-store`, `/my-store/products[/new|/:id]`, StoreOwner only)
- `features/admin/` — admin area (`/admin`, Admin role only): layout + users + role requests + audit + settings + store categories + product categories + moderation (`moderation/`: stores, store detail, products with bulk disable); later features add their screens here
- `features/account/` — My account: profile, affiliate code card, "Add a role" (role requests)
- `features/<name>/` — one folder per feature, lazy-loaded via `loadComponent` / `loadChildren`
- HTTP: `provideHttpClient(withFetch(), withInterceptors([...]))`
- Styling: **Tailwind CSS v4** via PostCSS (`.postcssrc.json`), tokens and design-system utilities (`card`, `btn-*`, `field`, `pill-*`, ADR 0005) in `src/styles.css`; component styles are plain CSS
- i18n: **Transloco** loads `public/i18n/<lang>.json` at runtime; `core/i18n/LanguageService` sets `<html lang dir>` (RTL for Arabic) and remembers the choice

## Testing

| Level | Where | Tooling |
|---|---|---|
| Domain / Application unit tests | `backend/tests/WebApp.UnitTests` | xUnit v3 (MTP) |
| API integration tests | `backend/tests/WebApp.IntegrationTests` | xUnit v3 + `WebApplicationFactory<Program>` (`ApiFactory`) + **Testcontainers PostgreSQL 17** (migrations applied; Docker required) |
| Frontend unit tests | `*.spec.ts` next to the code | Vitest (`ng test`) |

## Database

- PostgreSQL 17, accessed only from `WebApp.Infrastructure` through `AppDbContext` (Npgsql EF Core 10).
- Connection string lookup: `ConnectionStrings:webapp` (set by Aspire) → `ConnectionStrings:DefaultConnection` (user-secrets / env var).
- Local DB: Aspire container `webapp-postgres-aspire` on `localhost:62750` (user `webapp` / `webapp_dev`, see `WebApp.AppHost/appsettings.json`), or docker-compose `webapp-postgres` on `localhost:5433`.
- Migrations live in `WebApp.Infrastructure/Migrations` and are applied explicitly with `dotnet ef database update`; deployed services set `Database__MigrateOnStartup=true`.
- `dotnet ef` uses `DesignTimeDbContextFactory` (Aspire DB `localhost:62750` by default, override with `WEBAPP_DB`), so it doesn't need to boot the API.
- Applied migrations: `InitialAuth` (users, refresh_tokens), `AffiliateCodeAndBlocking` (users: affiliate code, terms date, block info), `AuditLog` (audit_entries), `RoleRequests` (role_requests), `PlatformSettings` (platform_settings), `StoreCategories` (store_categories), `Images` (images + store_categories.image_id), `Stores` (stores), `ProductCategories` (product_categories, product_attributes), `Products` (products, product_variants).

### Checkout transaction (014)

`CheckoutService` re-checks the cart with `CartRules` and `AffiliateCodeService`, then runs `IUnitOfWork.InTransactionAsync`: one conditional `UPDATE … SET stock = stock - q WHERE stock >= q` per line (`IProductRepository.TryTakeStockAsync`), the next `order_number_seq` value, the new `Order`, the cart emptied — committed together or rolled back together (the change tracker is cleared on rollback). No row is locked while the client fills the form; the conditional update is what prevents overselling.
