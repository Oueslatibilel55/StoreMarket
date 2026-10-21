# 008 — Product categories & dynamic attributes — Tasks

## Backend
- [x] Domain: `ProductCategory` aggregate (tree node, attributes, order), `AttributeDefinition` (key, type locked, unit, required, filterable, options), `AttributeOption` (JSON), `AttributeType`, `LocalizedNames`, `productCategory.*` error codes
- [x] Application: `CategoryTree` (path, depth, height, descendants, visibility), `ProductCategoryService` (create, rename / move under another parent, activate, deactivate, move among sisters, delete; add / edit / move / delete attribute; tree rules; audit), DTOs + mapping, `IProductCategoryRepository`, queries
- [x] Infrastructure: `ProductCategoryConfiguration` + `AttributeDefinitionConfiguration` (attributes as a child entity — EF can't put JSON in an owned table —, options `jsonb`), repository, `CatalogQueries` (visible tree, schema), `AdminQueries`; migration `ProductCategories` created **and applied** to the local Aspire DB
- [x] Api: `ProductCategoryEndpoints` (public tree + schema, admin CRUD)
- [x] Unit tests: `ProductCategoryTests` (slugs, option keys incl. single letters, attribute rules, key / type locked, options keep keys, order, tree walks + visibility)
- [x] Integration tests: `ProductCategoryEndpointsTests` (inherited schema, hidden branch, 3 levels + cycle, key unique in branch, validation, edit / move / delete audited, delete only without children, move under another parent, admin only)

## Frontend
- [x] `/admin/product-categories` lazy route, menu entry enabled
- [x] Tree (indent, reorder, add sub-category), selected category panel (inherited + own fields), category / field / confirm modals, choice editor
- [x] Audit log: product category links, field types, choice lists, translated "moved up / down"
- [x] i18n FR / AR / EN (464 keys)
- [x] Unit tests: `product-category-api.spec.ts`, `audit-diff.spec.ts` (types, options)

## Done
- [x] Acceptance criteria verified (backend 199 tests, frontend 38 tests; Chrome FR + AR: tree, inherited fields, add a choice field)
- [x] Seed: 13 categories (8 top, 5 sub), 22 fields
- [x] Docs updated

## Notes
- Fixed while testing: the choice editor overflowed the dialog; the tree arrow didn't mirror in Arabic.
- Added from the admin screen during the manual check: field "Motif" (Uni / Fleuri / Rayé) on Robes — kept as demo data.
