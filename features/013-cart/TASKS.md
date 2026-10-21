# 013 — Cart — Tasks

## Backend
- [x] Domain: `Cart` aggregate (one per user) + `CartLine` (product, variant, quantity, canonical affiliate code): `Add` (adds up same product + variant), `SetQuantity`, `SetCode`, `Remove`, `Clear`, `Merge` (guest cart); limits 50 lines / 99 units; error codes `cart.*`
- [x] Application: `CartService` (get, add with stock / variant / store checks, update quantity, set / remove code, remove, clear, merge, guest preview) building `CartDto` grouped by store with per-line issues and code status through `AffiliateCodeService` (012); `CartErrors`, `CartIssues`
- [x] Infrastructure: `CartConfiguration` / `CartLineConfiguration` (unique cart per user, unique product + variant per cart with NULLS NOT DISTINCT, restrict FK on product, no FK on variant), `CartRepository`, `CatalogQueries.GetCartProductsAsync` (visibility through `VisibleProducts()`, variant labels from the category schema); migration `Carts` created **and applied** to the local Aspire DB
- [x] Api: `CartEndpoints` (`/api/me/cart`, `/api/me/cart/lines`, `…/code`, `/api/me/cart/merge`, `POST /api/cart/preview`), rate limit policy `cart`, error → status mapping
- [x] Unit tests: `CartTests` (add / add up / code replace, limits, malformed code, set / remove, merge rules, clear)
- [x] Integration tests: `CartEndpointsTests` (grouping and totals, stock checks, variants priced and labelled, code valid on partner store / refused elsewhere / own code, disabled product and closed store flagged, guest preview + merge, privacy)

## Frontend
- [x] `core/cart/cart-api.ts`, `cart.service.ts` (server cart when signed in, browser cart + preview for guests, merge at sign-in), `referrals.ts` (share-link code per store, 30 days) + spec
- [x] Header: cart icon with item count (everyone)
- [x] Product page: quantity selector + "Ajouter au panier" (variant required, store open, in stock), confirmation with "Voir le panier", "Recommandé par …" for share links
- [x] Store page: `?ref=` remembered for the store
- [x] `/cart`: groups per store (logo, delivery fee, subtotal), lines (photo, options, unit price, quantity stepper, total, remove), code per line (add / change / remove, ✓ name or ✗ reason), issues per line, summary (items, delivery, total, cash on delivery) with checkout disabled until 014
- [x] i18n FR / AR / EN (797 keys in each file)

## Done
- [x] Acceptance criteria verified (tests green: 163 unit, 114 integration, 53 frontend; Chrome: guest cart from a share link, refused code on another store, merge at sign-in, quantity at stock limit, 375 px in Arabic)
- [x] Docs updated: `docs/CONTEXT.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`, `features/README.md`, this spec

## Notes
- Checkout (014) must re-check every line with the same rules (`CartService` issues + `AffiliateCodeService`) inside the order transaction.
