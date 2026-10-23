# 017 — Ratings & reviews — Tasks

## Backend
- [x] Domain: `Review` (+ `ReviewTarget`), `Ranking.Score / Average`, `DomainErrors.Review`; rating columns on `Product` / `Store`; setting `reviews.editWindowDays`
- [x] Application: `ReviewService` (create / edit, owner reply, admin hide / unhide audited), `ReviewErrors`, DTOs, `IReviewRepository` / `IReviewQueries`; public reads on `ICatalogQueries`; `rating` on shop DTOs; `ProductSort.TopRated`, `StoreSort`
- [x] Infrastructure: `ReviewConfiguration`, `ReviewRepository` (ownership incl. phone match, `RefreshRatingsAsync` with row locks), `ReviewQueries`, `CatalogQueries.Reviews`
- [x] Migration `Reviews`: **applied**
- [x] Api: `ReviewEndpoints`, error → status mapping
- [x] Unit tests `ReviewTests` (17)
- [x] Integration tests `ReviewEndpointsTests` (6): delivered only / once / averages / top-rated sort, strangers & owner refused, edit window (setting), owner reply, admin hide + audit + unhide, guest order rated after sign-up with the same phone

## Frontend
- [x] `core/reviews/review-api.ts` (+ spec), `Rating` on catalog types, `TopRated` sort
- [x] `shared/reviews`: `star-rating`, `star-input`, `review-section`
- [x] `/reviews` (`features/reviews/my-reviews.ts`), `/my-store/reviews` (+ tab), `/admin/reviews` (+ nav, audit link)
- [x] Product / store pages, cards, catalogue sort, home section, order detail "Noter", orders list "Mes avis"
- [x] i18n `reviews.*`, `errors.review.*`, `audit.actions.review.*`, `settings.reviews.*`, `home.topRatedProducts`, `shop.sort.TopRated`, `myStore.tabs.reviews` in fr / ar / en

## Done
- [x] Acceptance criteria verified (Chrome FR + AR: rate a product, public page, owner reply; 320 px without horizontal scroll on reviews, product, store, home, orders)
- [x] Docs updated (CONTEXT decision, CLAUDE.md, ARCHITECTURE, features/README.md, 011 / 018 specs)

## Notes
- 018 (search) reuses `RankingScore`. When the global average `C` changes, only the items whose reviews change get a new score. 018 may add a periodic recompute of all scores.
- Notifications (new review for the owner, reply for the client) come with 022.
