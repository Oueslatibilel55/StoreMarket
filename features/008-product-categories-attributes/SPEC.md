# 008 — Product categories & dynamic attributes (admin)

- **Status:** Done
- **Phase:** 2 — Stores & catalog
- **Depends on:** 003
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

Admin defines a product category tree and, for each category, the attributes ("fields") a product must/can have — so the product form is fully dynamic.

## Decisions (2026-10-20)

- **Tree of at most 3 levels** (Vêtements › Femme › Robes). Names FR (required) / AR / EN, unique URL name, order among sisters, active flag. **A hidden category hides its whole branch** from the shop.
- **Field types:** Text, Number (with an optional **unit**: ml, g, mois…), Yes / no, **One choice**, **Several choices** (2–50 choices, each named in 3 languages). Each field can be **required** and/or **filterable** (search, 018). 30 fields max per category.
- **Inheritance:** a product fills the fields of its category **and of all its ancestors** (parents first). A field **key** (`taille`, `volume`) is therefore **unique along a branch** (checked on create, and when a category moves under another parent).
- **Keys never change** once created (products store values by key); **the type never changes** either. Names, unit, required, filterable and choices can be edited (existing choices keep their key).
- A category can move under another parent with its branch (no cycle, still ≤ 3 levels). **Delete** only without sub-categories (009 adds: and without products).
- Every admin change is audited on the category (`productCategory.*`, `productCategory.attribute.*`).

## Examples (seeded)

| Category | Fields |
|---|---|
| Vêtements | Taille (one choice XS–XXL, required, filterable), Couleur (several, filterable), Matière (text) |
| › T-shirts | Coupe (Slim / Regular / Oversize) |
| › Robes | Longueur (Courte / Midi / Longue, filterable) |
| Parfums | Volume (number, ml, required, filterable), Genre (Homme / Femme / Unisexe), Concentration (EDT / EDP / Parfum) |
| Électronique › Smartphones | (Marque, Garantie inherited) + Stockage (64–512 Go), RAM (Go) |

## Acceptance criteria

- [x] Hierarchical categories (parent/child, ≤ 3 levels, no cycle), active flag, order among sisters
- [x] Attribute definitions per category: name (FR/AR/EN), type (text, number, boolean, single choice, multi choice), options, required, unit (numbers), filterable
- [x] Child categories inherit parent attributes; keys unique along the branch
- [x] Public endpoint returns the attribute schema of a category (inherited first, with the defining category) — for the product form (009) and search filters (018)
- [x] Admin screen: tree + selected category with inherited (grey) and own fields, add / edit / reorder / delete fields, choice editor
- [x] Every change audited

## API contract

| Method | Route | Auth | Response |
|---|---|---|---|
| GET | `/api/product-categories` | — | `200 ProductCategoryNode[]` (visible tree, depth-first) |
| GET | `/api/product-categories/{slug}/schema` | — | `200 CategorySchemaDto { category, path, attributes[] }` · `404` |
| GET | `/api/admin/product-categories` | Admin | `200 AdminProductCategoryDto[]` (whole tree, own attributes) |
| POST / PUT | `/api/admin/product-categories[/{id}]` | Admin | `{ parentId?, nameFr, nameAr?, nameEn?, slug? }` → `201 / 200` · `400` · `404` · `409 slugTaken / keyUsedInBranch` · `400 tooDeep / cycle` |
| POST | `/api/admin/product-categories/{id}/activate` · `/deactivate` · `/move` | Admin | `200` |
| DELETE | `/api/admin/product-categories/{id}` | Admin | `204` · `409 hasChildren` |
| POST | `/api/admin/product-categories/{id}/attributes` | Admin | `SaveAttributeRequest` → `201` · `400` · `409 attributeKeyTaken / keyUsedInBranch` |
| PUT / DELETE | `/api/admin/product-categories/{id}/attributes/{attributeId}` | Admin | `200` |
| POST | `/api/admin/product-categories/{id}/attributes/{attributeId}/move` | Admin | `200` |

## Persistence

Migration `ProductCategories`: `product_categories` (parent FK restrict, slug unique, index `(parent_id, sort_order)`), `product_attributes` (category FK cascade, `(category_id, key)` unique, **`options jsonb`**).

## Frontend

- **Route:** `/admin/product-categories?id=` (menu entry enabled)
- **Components:** `features/admin/product-categories` (tree, fields panel, category / field / confirm modals), `product-category-api.ts` (+ `pathOf` / `depthOf` helpers)

## Out of scope

- Product values for these fields → 009 (validates required fields, choice keys, numbers)
- Search filters → 018
