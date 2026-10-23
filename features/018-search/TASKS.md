# 018 — Search with rating-first ranking — Tasks

## Backend
- [x] Domain: `Product.FromPrice` set in `Apply`; unit tests
- [x] Infrastructure: migration `Search` — `unaccent` + `immutable_unaccent()`, `search_vector` generated columns + GIN (products, stores), `from_price` (backfilled) + index
- [x] `SearchText.ToTsQuery` (words → prefix tsquery) and `AttributeFilter.Parse` + unit tests
- [x] `ListShopProductsAsync`: full-text (product + store name), filters (store category, price, min rating, attributes), TopRated default when searching, relevance tiebreak, price sorts on `from_price`
- [x] `ListShopStoresAsync`: full-text, TopRated default when searching, relevance tiebreak
- [x] Api: new query params on `GET /api/products` (`[AsParameters] ShopProductRequest`)
- [x] Integration tests `SearchEndpointsTests` (accents, prefix, Arabic, store name, ranking order, each filter)
- [x] Performance check on 100k products

## Frontend
- [x] `CatalogApi.products(ProductQuery)` new params (repeated `attr`), `categorySchema()`
- [x] Catalogue: stores section when searching, filter panel (price, rating, store category, filterable fields), sort default
- [x] Header search box (icon below `lg`)
- [x] i18n fr / ar / en
- [x] Unit tests (`catalog-filters.spec.ts`)

## Done
- [x] Acceptance criteria verified
- [x] Docs updated (`features/README.md`, CLAUDE.md, ARCHITECTURE)

## Notes

- Performance (2026-10-23, local Docker Postgres 17, 100,061 published products, API in Release, median of 5 warm requests):
  browse 101 ms (newest) / 90 ms (top rated) / 49 ms (price); `creme` 41 ms (5k hits); `creme bio` 43 ms; 3 words 35 ms;
  `produit` (matches every row) 113 ms; `huile` + price + rating 79 ms; page 40 of `sac` 51 ms. All under 300 ms.
- Arabic-only product names need an explicit slug (slugs are Latin, 009) — unchanged here.
- Later: number / text field filters (ranges), "did you mean", search suggestions while typing.
