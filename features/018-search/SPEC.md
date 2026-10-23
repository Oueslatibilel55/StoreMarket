# 018 — Search with rating-first ranking

- **Status:** Done
- **Phase:** 4 — Ratings & search
- **Depends on:** 011, 017
- **Created:** 2026-10-19

## Goal

Searching shows the **best-rated stores and products first**.

## Ranking

`score = (v / (v + m)) * R + (m / (v + m)) * C`  (Bayesian average)

- `R` = item average rating, `v` = number of ratings
- `C` = global average rating, `m` = `Ranking.MinReviewsPrior` setting (004)

So a product with 4.8★ from 200 reviews ranks above one with 5★ from a single review.

## Acceptance criteria

- [x] Full-text search on product name/description/store name (PostgreSQL full-text, `tsvector` + GIN index) — accents ignored, prefixes, all words required
- [x] Results: matching **stores** section + **products** section, each sorted by ranking score, then relevance
- [x] Filters: category, store category, price range, min rating, dynamic attributes marked *filterable* (008)
- [x] Other sort options: price ↑/↓, newest
- [x] Ranking score stored on store/product and recomputed when a rating changes — done in 017 (`ReviewRepository.RefreshRatingsAsync`, null = unrated); `?sort=TopRated` already exists on `/api/products` and `/api/stores`
- [x] Paging; response < 300 ms on 100k products — measured 35–113 ms (see `TASKS.md`)

## Decisions (2026-10-23)

- **One search page: the catalogue.** `/products?search=` shows a **stores** section (top 4 matches, with a link to
  `/stores?search=`) above the **products** section. The header gets a search box that lands there. There is no separate `/search` route.
- **Full-text:** stored generated `tsvector` columns with a GIN index. Products: name (weight A) + description (B).
  Stores: name (A) + description (B). The `simple` config (FR / AR / EN are mixed, no stemming) runs over `unaccent`,
  wrapped in an immutable `immutable_unaccent()` so it can feed a generated column: `creme` finds `crème`.
  Each typed word is a prefix (`parf` → `parfum`) and all words must match. At most 8 words; punctuation is ignored.
- A product also matches when its **store name** matches: matching stores are found first, then `OR store_id IN (…)`.
- **Order when searching:** the default sort becomes `TopRated`: open stores first (stores only), then ranking score
  (unrated last), then relevance (`ts_rank`), then newest. Without a search the defaults stay as they are (products: newest;
  stores: open first, then name). An explicit `sort=PriceAsc|PriceDesc|Newest` still wins, with relevance as the tiebreak.
- **From price stored:** `Product.FromPrice` (cheapest variant price, else the price) is set by the domain on every
  create / update and indexed. The price filter and the price sorts use it, so there is no sub-query over variants at 100k rows.
- **Filters** on `/api/products`: `category`, `storeCategory` (store category slug), `minPrice` and `maxPrice` (on the
  from price), `minRating` (1–5, on the average), `attr=<field>:<choice>,<choice>` (repeatable). Attribute filters
  apply only to **filterable SingleChoice / MultiChoice / Boolean** fields of the selected category (and its ancestors).
  A product matches a choice through its values **or a variant**. Several choices of one field = any; several fields = all.
  Unknown or non-filterable fields are ignored, so stale links keep working. Number / text filters come later.
- Performance is measured with `EXPLAIN ANALYZE` on 100k generated products (see `TASKS.md`).
