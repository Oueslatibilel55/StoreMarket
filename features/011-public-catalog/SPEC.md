# 011 — Public catalog: store & product pages

- **Status:** Done (v1 — top-rated sections wait for ratings, 017)
- **Phase:** 2 — Stores & catalog
- **Depends on:** 009 (010 only adds the "disabled by the admin" filter)
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

Visitors and buyers browse the marketplace: stores, products, prices, variants, delivery.

## Decisions (2026-10-20)

- Built **before 010** (the user wanted to see the shop): visibility is *published, not deleted, store Active or Closed*. **010 added "not disabled"** (done 2026-10-20) in one place: `CatalogQueries.VisibleProducts()` (+ the store list / store page / product page filters).
- **Closed stores stay visible**, marked closed (their products too) — orders resume when they reopen.
- No ratings yet → the home shows **store categories, newest products and stores**; "top-rated" sections and rating-first sorting come with 017 / 018.
- **Buying is not possible yet**: the "Add to cart" button is shown disabled ("coming soon") until the cart (013).
- Affiliates see on a product: the % it gives, the coins they'd earn per item (`price × % ÷ coin value`) and their code to copy — with a note that it counts once the store accepts them (partnerships, 012).

## Acceptance criteria

- [x] Home page: store categories (→ stores of the category), newest products, stores
- [ ] Home "top-rated stores / products" → 017 (ratings)
- [x] Store list `/stores`: store category chips, city, search, paging; open stores first
- [x] Store page `/stores/{slug}`: banner, logo, category, city, description, delivery (fee, delay, zones), contact & links, products with sort and paging; closed stores shown as closed
- [x] Product catalogue `/products`: by product category (sub-categories included, breadcrumb + sub-category chips), search, sort (newest, price ↑ / ↓ on the "from" price), paging
- [x] Product page `/products/{slug}`: gallery, price ("from" until a variant is chosen), old price, **variant picker** (impossible combinations crossed out, stock per variant), characteristics with labels / units, description, store card, delivery & cash on delivery; for affiliates: code, % and coins
- [x] Only published, not deleted products of active / closed stores are visible
- [x] … and not disabled by the admin (added by 010 in `VisibleProducts()`, which the product page now uses too)
- [x] Slugs in URLs; page title + meta description (+ og:title / og:description) per page
- [ ] Rating & reviews on store / product pages → 017

## API contract

| Method | Route | Response |
|---|---|---|
| GET | `/api/products?category=&store=&search=&sort=Newest\|PriceAsc\|PriceDesc&page=&pageSize=` | `200 PagedResult<ProductCardDto>` · `404` unknown category / store |
| GET | `/api/stores?category=&city=&search=&page=&pageSize=` | `200 PagedResult<StoreCardDto>` |
| GET | `/api/stores/{slug}` (007) · `/api/products/{slug}` (009) | `200` · `404` |

## Frontend

- **Routes:** `/products`, `/products/:slug`, `/stores`, `/stores/:slug`; header links "Produits / Boutiques"; scroll to top on navigation
- **Components:** `features/shop` (catalogue, product page + `variant-picker.ts`, store list, store page), `shared/catalog` (product card, store card), `core/seo/seo.ts`, `CatalogApi`
