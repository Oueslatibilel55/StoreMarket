# 017 — Ratings & reviews (products and stores)

- **Status:** Done
- **Phase:** 4 — Ratings & search
- **Depends on:** 016
- **Created:** 2026-10-19

## Goal

Buyers rate products and stores; ratings drive search ranking.

## Decisions (2026-10-22)

- **Who rates:** the client of a **delivered** store order. Their own orders (placed signed in), or **guest / affiliate-placed orders whose client phone is the account's phone**. The affiliate who placed the order does not rate it, and a store owner never rates their own store. Phones are not verified yet (026), so for now someone who signs up with another person's phone could rate that person's guest orders. This risk is accepted for v1.
- **One review per order line** (the product as received) **and one per store order** (the store's service: preparation, delivery, contact). Two separate ratings.
- **1–5 stars + optional comment** (≤ 1000 characters). Photos come later.
- **Edit window:** setting `reviews.editWindowDays` (default **30**). The deadline is frozen on the review when it is published, so changing the setting never reopens or closes old reviews.
- **Public name:** first name + initial (`Leila G.`), frozen at publication. Reviews are verified by construction, so there is no "verified purchase" badge per review; the section shows "only delivered customers can review".
- **Ranking score:** Bayesian average `(v·R + m·C) / (v + m)`. `m` = `ranking.minReviewsPrior` (default 10), `C` = average of every visible review of the same kind (products or stores). **Unrated = null**, sorted after every rated item.
- **Recomputed in the same transaction** as each create / edit / hide / unhide, from the visible reviews. The product and store rows are locked first (product before store), so concurrent reviews are counted one after the other.
- **Store owner:** one public answer per review, editable or removable. Not allowed on a hidden review.
- **Admin:** hides / shows again a review with a **reason** (audited `review.hide` / `review.unhide`). A hidden review leaves the public lists and the averages. The author and the store owner still see it, flagged, with the reason. The author can no longer edit it.

## Acceptance criteria

- [x] Only a client with a **delivered** order containing the product (or from the store) can rate it; guest orders can be rated after registering with the same phone. One review per order line / per store order.
- [x] 1–5 stars + optional comment (+ optional photos later)
- [x] Buyer can edit their review within N days (setting, frozen per review)
- [x] Product and store keep `AverageRating`, `RatingCount`, `RankingScore` (updated on each change)
- [x] Store owner can reply publicly to a review
- [x] Admin can hide abusive reviews (audited); hidden reviews excluded from averages
- [x] Shop: stars on product / store cards and pages, reviews section with 1★…5★ breakdown (filter by stars), "Top rated" sort and home section (011)

## Backend

### Domain
- `Review` aggregate (`Target` Product / Store, author, order, store order, order line, product, store, stars, comment, `EditableUntil`, reply, hide). Rules: stars 1–5, comment ≤ 1000, edit window, reply ≤ 1000, hide / unhide with reason ≤ 500.
- `Ranking.Score` / `Ranking.Average` (Domain/Common).
- `Product` / `Store`: `AverageRating`, `RatingCount`, `RankingScore` (maintained by `ReviewRepository.RefreshRatingsAsync`).

### API contract

| Method | Route | Request | Response | Errors |
|---|---|---|---|---|
| GET | `/api/products/{slug}/reviews?stars=&page=` | — | `200 PublicReviewsDto` (stats + page) | `404 product.notFound` |
| GET | `/api/stores/{slug}/reviews?stars=&page=` | — | `200 PublicReviewsDto` | `404 store.notFound` |
| GET | `/api/me/reviews` | — | `200 MyReviewsDto` (to write + written) | |
| POST | `/api/me/reviews` | `{ storeOrderId, orderLineId?, stars, comment? }` | `201 MyReviewDto` | `404 review.orderNotFound`, `409 review.notDelivered / alreadyReviewed / ownStore`, `400 review.starsInvalid / commentTooLong` |
| PUT | `/api/me/reviews/{id}` | `{ stars, comment? }` | `200 MyReviewDto` | `404 review.notFound`, `409 review.editWindowClosed / hidden` |
| GET | `/api/me/store/reviews?target=&stars=&replied=&page=` | — | `200 StoreReviewsDto` | |
| PUT / DELETE | `/api/me/store/reviews/{id}/reply` | `{ reply }` | `200 StoreReviewDto` | `404 review.notFound`, `409 review.hidden` |
| GET | `/api/admin/reviews?target=&hidden=&stars=&storeId=&search=&page=` | — | `200 PagedResult<AdminReviewDto>` | |
| GET | `/api/admin/reviews/counts` | — | `200 { total, hidden, lowStars }` | |
| POST | `/api/admin/reviews/{id}/hide` · `/unhide` | `{ reason }` | `200 AdminReviewDto` | `400 review.reasonRequired`, `409 review.alreadyHidden / notHidden` |

Shop lists: `GET /api/products?sort=TopRated`, `GET /api/stores?sort=TopRated`. Cards and pages carry `rating { average, count }`.

### Persistence
Migration `Reviews`: table `reviews` (check `stars BETWEEN 1 AND 5`, restrict FKs to users / orders / store orders / order lines / products / stores, unique `order_line_id` where target = Product, unique `store_order_id` where target = Store); `products` / `stores` + `average_rating numeric(3,2)`, `rating_count`, `ranking_score numeric(6,4)` (index on `products.ranking_score`).

## Frontend

- **Routes:** `/reviews` (client: to rate + my reviews), `/my-store/reviews` (owner, new tab), `/admin/reviews` (nav entry enabled).
- **Components:** `shared/reviews/star-rating` (read-only, fractions), `star-input` (radio group), `review-section` (public list with stats); `core/reviews/review-api`.
- Product and store pages: rating under the title (scrolls to the section) + reviews section. Cards show stars when rated. Catalogue sort "Mieux notés". Home "Les mieux notés" (rated products only), stores sorted by rating.
- Order detail: "Noter" on delivered parts; "Mes avis" button on `/orders`.

## Out of scope

- Photos on reviews, "helpful" votes, client reports of abusive reviews (admin finds them in the list), notifications (022).

## Open questions

- [x] ~~Edit window N~~ → setting `reviews.editWindowDays`, 30 days, frozen per review
- [x] ~~Ranking formula~~ → Bayesian average, `m` = `ranking.minReviewsPrior`, `C` = global average of the same kind
- [x] ~~Guest orders~~ → matched by the account phone (risk accepted until phone verification)
- [x] ~~Store rating~~ → its own review per store order (service), not derived from product reviews
