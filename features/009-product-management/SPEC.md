# 009 — Product management (store owner)

- **Status:** Done
- **Phase:** 2 — Stores & catalog
- **Depends on:** 006, 007, 008
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

Store owners add and manage their products with all information, including **the affiliate % they give per item sold**. Products are live as soon as they are published (no admin approval).

## Decisions (2026-10-20)

- **Variants are part of 009.** The owner picks **up to 2 choice fields** of the category as variant axes (e.g. Taille × Couleur), ticks the options they have, and gives **each combination its own stock**, an optional **price** (else the product price) and SKU (60 variants max). A variant keeps its id while the combination still exists (orders will point to it).
- **A field or choice used by products can't be deleted** by the admin (`productCategory.attributeInUse` / `optionInUse`) — rename it instead; a category with products (deleted ones included) can't be deleted (`productCategory.hasProducts`).
- Field values are stored **by field key** as canonical strings (numbers invariant, booleans `true`/`false`, choices = option keys); they include the **inherited** fields of the parent categories.
- **Required fields are enforced only when publishing**: a draft can be saved half-filled.
- **Unique URL name** across the platform: made from the name, `-2`, `-3`… added automatically when taken; kept when the name doesn't change.
- **Up to 8 photos** (the owner's own `ProductPhoto` uploads), the first is the main one; an image used by a product can't be deleted.
- **Affiliate %**: empty = the store's default; else within the settings limits. Preview: coins per item = `price × % ÷ coin value in TND` (the spec's `× Coin.ValueInTnd` was a slip: 1 coin worth 2 TND halves the coins).
- **Soft delete** (`deleted_at`): the product leaves the store and the shop, past orders keep it.
- Owner actions are not audited (not admin actions).

## Acceptance criteria

- [x] Create/edit product: name, description, category, price (TND), compare-at price, stock quantity, SKU, attribute values (form generated from 008), status (Draft / Published / Archived)
- [x] **Affiliate %** per product: defaults to the store's default %, can be overridden, within the settings limits
- [x] Owner sees an "affiliate earns ≈ X coins per item" preview
- [x] Changing price or % only affects future orders (frozen on order lines, 014)
- [x] Multiple images, reorder, choose main image (first one) — `ProductPhoto` images, products added to `ImageRepository.IsInUseAsync`
- [x] Variants (choice axes × own stock / price / SKU)
- [x] Validation: required attributes filled (to publish), price > 0, stock >= 0, values checked against the category schema (inherited fields included)
- [x] A product category with products can't be deleted; a field or a choice used by products can't be deleted / removed
- [x] Owner product list with search (name / SKU), filter by status, paging
- [x] Products are **soft-deleted**
- [x] Public `GET /api/products/{slug}`: published, not deleted, store active or closed — labels resolved (field and choice names, units), variants with price / in stock (for 011)

## API contract

| Method | Route | Auth | Response |
|---|---|---|---|
| GET | `/api/me/store/products?search=&status=&page=&pageSize=` | StoreOwner | `200 PagedResult<ProductListItem>` · `404 store.notFound` |
| GET | `/api/me/store/products/{id}` | StoreOwner | `200 ProductDto` · `404` |
| POST | `/api/me/store/products` | StoreOwner | `SaveProductRequest` → `201` · `400 product.*` · `409 product.slugTaken` |
| PUT | `/api/me/store/products/{id}` | StoreOwner | `200` · `400` · `404` · `409` |
| DELETE | `/api/me/store/products/{id}` | StoreOwner | `204` (soft) |
| GET | `/api/products/{slug}` | — | `200 PublicProductDto` · `404` |

## Persistence

Migration `Products`: `products` (store / category FKs restrict, slug unique, `image_ids uuid[]`, `variant_axes text[]`, `values jsonb`, `deleted_at`), `product_variants` (product FK cascade, `selection jsonb`).

## Frontend

- **Routes:** `/my-store/products`, `/my-store/products/new`, `/my-store/products/:id` (StoreOwner); tabs "My store / My products"
- **Components:** `features/my-store/products` (list, editor: basics, affiliate preview, photos, dynamic fields, variants), `core/stores/product-api.ts` (+ `coinsPerItem`, `combinations`)

## Out of scope

- Admin moderation of products (disable / delete, suspicious prices) → 010
- Public product page → 011 (API ready)
