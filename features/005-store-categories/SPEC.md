# 005 — Store categories (admin)

- **Status:** Done
- **Phase:** 2 — Stores & catalog
- **Depends on:** 003
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

Admin manages the list of store categories (cosmetics, clothes, perfumes…).

## Decisions (2026-10-20)

- **Names in 3 languages:** French required, Arabic and English optional — the shop falls back to French (`LocalizedName { fr, ar, en }`, `localized` pipe).
- **Slug** (URL name, `parfums`, `pret-a-porter`): made from the French name when left empty (accents removed), editable, unique; lower-case letters, digits, dashes, 2–60.
- **French name unique** (case-insensitive).
- **Icon = an emoji** until image upload exists (006); an image can be added then.
- **Order:** move up / down (swap with the neighbour); new categories go last.
- **Deactivate** hides a category from the shop; stores already in it keep it.
- **Delete** is allowed only while no store uses the category; a category in use → `409 storeCategory.inUse`, hide it instead (since 007).

## Acceptance criteria

- [x] Admin CRUD: names FR / AR / EN, slug, icon, display order, active flag
- [x] Every admin action audited (`storeCategory.create / update / activate / deactivate / move / delete`), no-ops not audited
- [x] Duplicate slug / French name → `409 storeCategory.slugTaken / nameTaken`; bad input → `400 storeCategory.*`
- [x] Public `GET /api/store-categories` returns active categories in display order
- [x] Home page lists the active categories (links to store lists come with 011)
- [x] Deactivate instead of delete when stores use the category — done in 007 (`409 storeCategory.inUse`)

## API contract

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/store-categories` | — | — | `200 StoreCategoryDto[]` (active, ordered) |
| GET | `/api/admin/store-categories` | Admin | — | `200 AdminStoreCategoryDto[]` (all, ordered) |
| POST | `/api/admin/store-categories` | Admin | `{ nameFr, nameAr?, nameEn?, slug?, icon? }` | `201` · `400` · `409` |
| PUT | `/api/admin/store-categories/{id}` | Admin | same | `200` · `400` · `404` · `409` |
| POST | `/api/admin/store-categories/{id}/activate` · `/deactivate` | Admin | — | `200` |
| POST | `/api/admin/store-categories/{id}/move` | Admin | `{ direction: up \| down }` | `200` |
| DELETE | `/api/admin/store-categories/{id}` | Admin | — | `204` · `404` |

## Persistence

Migration `StoreCategories`: table `store_categories` (`name_fr`, `name_ar`, `name_en`, `slug` unique, `icon`, `sort_order`, `is_active`), index `(is_active, sort_order)`.

## Frontend

- **Route:** `/admin/store-categories` (menu entry enabled)
- **Components:** `features/admin/store-categories/store-category-list` (list + form modal + confirm modal), `shared/i18n/localized` (pipe + `pickName`), `core/catalog/catalog-api`
- **States:** loading / empty / error; inline validation; hidden categories greyed out
