# 018 — Search with rating-first ranking

- **Status:** Draft
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

- [ ] Full-text search on product name/description/store name (PostgreSQL full-text, `tsvector` + GIN index)
- [ ] Results: matching **stores** section + **products** section, each sorted by ranking score, then relevance
- [ ] Filters: category, store category, price range, min rating, dynamic attributes marked *filterable* (008)
- [ ] Other sort options: price ↑/↓, newest
- [ ] Ranking score stored on store/product and recomputed when a rating changes (017)
- [ ] Paging; response < 300 ms on 100k products
