# 017 — Ratings & reviews (products and stores)

- **Status:** Draft
- **Phase:** 4 — Ratings & search
- **Depends on:** 016
- **Created:** 2026-10-19

## Goal

Buyers rate products and stores; ratings drive search ranking.

## Acceptance criteria

- [ ] Only a client with a **delivered** order containing the product (or from the store) can rate it; guest orders can be rated after registering with the same phone — one review per order line / per store order
- [ ] 1–5 stars + optional comment (+ optional photos later)
- [ ] Buyer can edit their review within N days
- [ ] Product and store keep `AverageRating`, `RatingCount`, `RankingScore` (updated on each change)
- [ ] Store owner can reply publicly to a review
- [ ] Admin can hide abusive reviews (audited); hidden reviews excluded from averages
