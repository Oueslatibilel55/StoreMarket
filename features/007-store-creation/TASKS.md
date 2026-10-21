# 007 — Store creation & configuration — Tasks

## Backend
- [x] Domain: `Store` aggregate + `StoreDetails`, `StoreStatus`, `Governorates` (24 codes), shared `Slug` helper (also used by store categories), `store.*` error codes
- [x] Application: `StoreService` (get mine / public, create, update, close, reopen; category active check, own-image check, percent limits from settings, subscription price copied from the setting), `ImageService.RequireOwnAsync`, `StoreCategoryService` refuses deleting a category in use
- [x] Infrastructure: `StoreConfiguration`, `StoreRepository`, `CatalogQueries` (public store, my store), `ImageRepository.IsInUseAsync` covers logos / banners; migration `Stores` created **and applied** to the local Aspire DB
- [x] Api: `StoreEndpoints` (`/api/me/store`, `/api/stores/{slug}`, `/api/cities`)
- [x] Unit tests: `StoreTests` (normalization, limits, links, delivery rules, close / reopen)
- [x] Integration tests: `StoreEndpointsTests` (create + public view + subscription price, store owners only + one each, validation + unique slug, hidden category, someone else's image, closed store, 404 + cities)

## Frontend
- [x] `/my-store` route + header link for store owners
- [x] `StoreApi`, `CITIES`
- [x] My store page: create / edit form (identity, images, contact & links, selling & delivery), close / reopen with confirm
- [x] i18n FR / AR / EN (391 keys, incl. the 24 governorates)

## Done
- [x] Acceptance criteria verified (backend 182 tests, frontend 36 tests; manual check in Chrome FR + AR: load, edit, save)
- [x] Seed: 5 demo stores with logo + banner (one closed)
- [x] Docs updated (`features/README.md`, `docs/CONTEXT.md`, `docs/ARCHITECTURE.md`, `CLAUDE.md`, 005 / 010 / 011 specs, `dev-users.txt`)

## Notes
- Fixed while testing: the phone field showed the +216 prefix twice when editing.
