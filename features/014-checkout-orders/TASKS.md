# 014 — Checkout & orders — Tasks

## Backend
- [x] Domain: `Order` aggregate (`Place`: contact checks, split per store, totals, expected coins, reference, `OrderPlacedEvent`), `StoreOrder` (frozen delivery fee, `AmountToCollect`, status `Placed`), `OrderLine` (frozen snapshot + affiliate, partnership, %, expected coins), `OrderContact`, `OrderAffiliate`, `OrderOption`, `StoreOrderStatus`; error codes `order.*`
- [x] Application: `CheckoutService` (place — normal / guest / affiliate mode, re-check with `CartRules` + `AffiliateCodeService.MatchAsync`, delivery zones, transaction with atomic stock; my orders, detail, guest lookup, affiliate sales); `CartRules` shared with the cart; `IUnitOfWork.InTransactionAsync`; `IProductRepository.TryTakeStockAsync`; `CheckoutErrors`; cart groups now carry the store's delivery cities
- [x] Infrastructure: `OrderConfiguration` / `StoreOrderConfiguration` / `OrderLineConfiguration` (options as jsonb, restrict FKs), sequence `order_number_seq`, `OrderRepository`, `OrderQueries`, `UnitOfWork.InTransactionAsync`; migration `Orders` created **and applied** to the local Aspire DB
- [x] Api: `OrderEndpoints` (`POST /api/orders`, `GET /api/orders/lookup`, `/api/me/orders`, `/api/me/affiliate/orders`), rate limited, error → status mapping
- [x] Unit tests: `OrderTests` (split per store, totals, contact normalisation, frozen affiliate + expected coins, contact errors)
- [x] Integration tests: `CheckoutEndpointsTests` (signed-in order: split, stock taken, cart emptied, code frozen, affiliate sees expected coins, privacy; guest + lookup; delivery zone / bad contact / empty cart; code refused after the partnership ended and disabled product — nothing taken; **last unit race**; affiliate mode)

## Frontend
- [x] `core/orders/order-api.ts`; `CartService.checkoutLines()` / `ordered()`
- [x] `/checkout`: "Pour moi / Pour un client" for affiliates, delivery form (pre-filled), cash on delivery, summary per store with "ne livre pas à …" warnings, errors sending back to the cart
- [x] `/orders/:reference` (thank-you banner after checkout, works for guests through navigation state), `/orders` (my orders), `/orders/track` (guest lookup), shared `OrderView`
- [x] Cart "Passer la commande" enabled; header "Commandes"; account card "Mes commandes"; footer "Suivre une commande"; `/affiliate` "Ventes avec mon code" (summary + lines)
- [x] i18n FR / AR / EN (870 keys in each file)

## Done
- [x] Acceptance criteria verified (tests green: 171 unit, 120 integration, 53 frontend; Chrome: Leila's order `CMD-2026-000001` with a delivery-zone warning, my orders, Sana's sales ≈ 156.8 coins, guest tracking, 375 px)
- [x] Docs updated: `docs/CONTEXT.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`, `features/README.md`, this spec

## Notes
- 016 adds the store-owner screens and the statuses after `Placed`; 015 the payment status on `StoreOrder`.
