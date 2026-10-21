# 009 — Product management — Tasks

## Backend
- [x] Domain: `Product` aggregate (validation against the schema, canonical values, variants sync keeping ids, soft delete, `UsesAttribute` / `UsesOption`), `AttributeValue` (JSON), `ProductVariant`, `ProductStatus`, `product.*` error codes
- [x] Application: `ProductService` (list / get / create / update / delete for the owner, public get; visible category + schema with inherited fields, own photos, percent limits, unique slug with `-2`…), DTOs, `IProductRepository`; `ProductCategoryService` refuses deleting a category with products, a used field or a used choice
- [x] Infrastructure: `ProductConfiguration` / `ProductVariantConfiguration` (`uuid[]`, `text[]`, `jsonb`), `ProductRepository`, `CatalogQueries.Products` (owner list, owner product, public product with labels), image in-use check; migration `Products` created **and applied** to the local Aspire DB
- [x] Api: `ProductEndpoints`
- [x] Unit tests: `ProductTests` (canonical values, schema checks, required only when published, prices / stock / percent / photos, variants rules, ids kept, soft delete)
- [x] Integration tests: `ProductEndpointsTests` (public with inherited labels, drafts private, unique slug, variants, rules, list filters, soft delete, used category / field / choice protected, store owners only)

## Frontend
- [x] Routes under `/my-store` + tabs
- [x] Product list (search, status, paging, photo, "from" price, stock)
- [x] Product editor (basics, affiliate % + coins preview, photos with order / main, dynamic fields per type, variants axes / options / table)
- [x] i18n FR / AR / EN (532 keys)
- [x] Unit tests: `product-api.spec.ts` (`coinsPerItem`, `combinations`)

## Done
- [x] Acceptance criteria verified (backend 216 tests ×2, frontend 42 tests; Chrome FR + AR: list, editor with variants, save keeps variant ids)
- [x] Seed: 16 demo products in the 5 stores (3 with variants, 1 draft, 1 archived, 2 out of stock)
- [x] Docs updated

## Notes
- During the Chrome check the tab once stopped answering for ~30 s right after opening the editor; it could not be reproduced afterwards (editor loads in ~0.1 s, re-renders in ~0.15 s, no request loop, no console error). Watch for it.
- Changed during the check: T-shirt basique coton, variant S · Noir stock 10 → 11.
