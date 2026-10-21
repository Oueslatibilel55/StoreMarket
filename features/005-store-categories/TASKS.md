# 005 — Store categories — Tasks

## Backend
- [x] Domain: `StoreCategory` (names FR/AR/EN, `Slugify`, slug rule, icon, order swap, activate / deactivate), `storeCategory.*` error codes
- [x] Application: `StoreCategoryService` (create, update, activate, deactivate, move, delete, uniqueness checks, audit), DTOs (`LocalizedName`, `StoreCategoryDto`, `AdminStoreCategoryDto`), `IStoreCategoryRepository`, `ICatalogQueries` (public reads), `IAdminQueries.ListStoreCategoriesAsync`
- [x] Infrastructure: `StoreCategoryConfiguration`, `StoreCategoryRepository` (case-insensitive name check with `ILIKE`), `CatalogQueries`; migration `StoreCategories` created **and applied** to the local Aspire DB
- [x] Api: `StoreCategoryEndpoints` (public list + admin CRUD / move / activate)
- [x] Unit tests: `StoreCategoryTests` (slugs, validation, order swap, active flag)
- [x] Integration tests: `StoreCategoryEndpointsTests` (public list + audit, hidden from shop, 409 duplicates, update, 400, move + audit, delete, admin only)

## Frontend
- [x] `/admin/store-categories` lazy route, menu entry enabled
- [x] `AdminApi` store-category calls; `CatalogApi.storeCategories()`
- [x] List (move up / down, show / hide, delete with confirm), create / edit modal (3 names, slug, emoji picker)
- [x] `localized` pipe with French fallback; home page shows active categories
- [x] Audit log: localized names and yes / no shown properly
- [x] i18n FR / AR / EN (286 keys, same in the three files)
- [x] Unit tests: `localized.spec.ts`, `audit-diff.spec.ts` (objects, booleans)

## Done
- [x] Acceptance criteria verified (backend 150 tests, frontend 33 tests; manual check in Chrome FR + AR)
- [x] Seed script creates 6 demo categories (Parfums, Cosmétiques, Vêtements, Chaussures, Bijoux, Électronique)
- [x] Docs updated (`features/README.md`, `docs/CONTEXT.md`, `docs/ARCHITECTURE.md`, `CLAUDE.md`, 007 spec)

## Notes
- "Deactivate instead of delete when in use" moves to 007, the first feature where stores reference a category.
