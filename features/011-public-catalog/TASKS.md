# 011 — Public catalog — Tasks

## Backend
- [x] `ShopDtos` (product / store cards, queries, sort), `ICatalogQueries.ListShopProductsAsync / ListShopStoresAsync`
- [x] `CatalogQueries.Shop` — one `VisibleProducts()` rule; category incl. sub-categories; sort on the "from" price in SQL; store list with product count, open first
- [x] `ShopEndpoints` (`GET /api/products`, `GET /api/stores`)
- [x] Integration tests: `ShopEndpointsTests` (category + sub-categories, drafts hidden, from-price sort, store filter, closed store visible, 404, store filters + count)

## Frontend
- [x] Routes + header links, scroll restoration
- [x] Home (hero, store categories, new products, stores)
- [x] Product catalogue (breadcrumb, sub-categories, search, sort, paging)
- [x] Store list (category chips, city, search) and store page (banner, products, delivery, contact)
- [x] Product page (gallery, variant picker, stock, characteristics, delivery, affiliate box, cart "coming soon")
- [x] `Seo` (title / description, waits for translations)
- [x] i18n FR / AR / EN (569 keys)
- [x] Unit test: `variant-picker.spec.ts`

## Done
- [x] Acceptance criteria verified except the parts waiting for 010 / 017 (backend 221 tests, frontend 44 tests; Chrome FR + AR: home, catalogue, product with variants, store page)
- [x] Docs updated

## Notes
- Fixed while testing: tab title showed "app.name" before translations loaded; old price stuck to the price on product cards in Arabic.
