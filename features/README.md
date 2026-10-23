# Features

Every piece of work starts as a feature folder here. It is the single source of truth for **what** we build and **where it stands**.
Features are numbered in **build order**: each one only depends on lower numbers.

## Workflow

1. Pick the next `Draft` feature, review its `SPEC.md` together, resolve its open questions → status `Ready`.
2. Break it down in `TASKS.md` (backend / frontend / tests) → `In progress`.
3. Implement, test, tick the acceptance criteria → `Review` → `Done`.
4. Update the status in this table and in the spec header.

New feature? Copy `_template/` to `NNN-short-name/`.

Statuses: `Draft` → `Ready` → `In progress` → `Review` → `Done`

## Backlog

### Phase 1 — Foundation
| # | Feature | Depends on | Status |
|---|---|---|---|
| 000 | [Project setup](000-project-setup/SPEC.md) | — | Done |
| 001 | [Authentication & accounts (simple sign-up / sign-in)](001-authentication/SPEC.md) | 000 | **Done** |
| 002 | [Roles & permissions (affiliate code, admin roles / block API — activation replaced by 027)](002-roles-and-permissions/SPEC.md) | 001 | **Done** |
| 003 | [Admin panel shell, user management & audit log](003-admin-panel-shell/SPEC.md) | 002 | **Done** |
| 027 | [Account types (client / affiliate at sign-up) & admin-approved role requests](027-account-types-role-requests/SPEC.md) — amends 001–003 | 002, 003 | **Done** |
| 004 | [Platform settings (dynamic config)](004-platform-settings/SPEC.md) | 003 | **Done** |

### Phase 2 — Stores & catalog
| # | Feature | Depends on | Status |
|---|---|---|---|
| 005 | [Store categories (admin)](005-store-categories/SPEC.md) | 003 | **Done** |
| 006 | [Image upload & storage](006-image-upload/SPEC.md) | 001 | **Done** (v1 — thumbnails pending) |
| 007 | [Store creation & configuration (default affiliate %)](007-store-creation/SPEC.md) | 002, 005, 006 | **Done** |
| 008 | [Product categories & dynamic attributes](008-product-categories-attributes/SPEC.md) | 003 | **Done** |
| 009 | [Product management (affiliate % per product, variants)](009-product-management/SPEC.md) | 006, 007, 008 | **Done** |
| 010 | [Store & product moderation — disable / delete fakes](010-store-product-moderation/SPEC.md) | 007, 009 | **Done** (suspicious view skipped) |
| 011 | [Public catalog: store & product pages](011-public-catalog/SPEC.md) | 009, 010 ("disabled" filter added) | **Done** (top-rated added with 017) |

### Phase 3 — Orders & affiliate codes
| # | Feature | Depends on | Status |
|---|---|---|---|
| 012 | [Affiliate codes & store partnerships (request → store owner accepts)](012-affiliate-codes/SPEC.md) | 002, 007, 011 | **Done** (notifications with 022) |
| 013 | [Cart — grouped by store, affiliate code per product](013-cart/SPEC.md) | 011, 012 | **Done** |
| 014 | [Checkout & orders (client or affiliate for client)](014-checkout-orders/SPEC.md) | 012, 013 | **Done** (store screens with 016) |
| 015 | [Cash on delivery](015-cash-on-delivery/SPEC.md) | 014 | **Done** |
| 016 | [Order lifecycle (store confirms Delivered; refused = no coins)](016-order-lifecycle/SPEC.md) | 014, 015 | **Done** (coins with 019) |

### Phase 4 — Ratings & search
| # | Feature | Depends on | Status |
|---|---|---|---|
| 017 | [Ratings & reviews (products & stores)](017-ratings-reviews/SPEC.md) | 016 | **Done** (photos later; notifications with 022) |
| 018 | [Search with rating-first ranking](018-search/SPEC.md) | 011, 017 | **Done** (number / text field filters later) |

### Phase 5 — Money: coins, payouts & billing
| # | Feature | Depends on | Status |
|---|---|---|---|
| 019 | [Coin ledger (credited on delivery)](019-coin-ledger/SPEC.md) | 004, 014, 016 | **Done** |
| 020 | [Affiliate wallet & history](020-affiliate-wallet/SPEC.md) | 019 | **Done** |
| 021 | [Payout requests (request → verify → scheduled cash / transfer → paid)](021-payouts/SPEC.md) | 004, 019 | **Done** |
| 025 | [Store billing (per-store subscription price + offers, monthly commissions, suspend if unpaid)](025-store-billing/SPEC.md) | 004, 007, 010, 019 | **Done** (reminders with 022) |

### Later
| # | Feature | Depends on | Status |
|---|---|---|---|
| 026 | [Email verification & password reset](026-email-verification-password-reset/SPEC.md) | 001, 022 | Draft — later |

### Phase 6 — Admin & operations
| # | Feature | Depends on | Status |
|---|---|---|---|
| 022 | [Notifications (email + in-app)](022-notifications/SPEC.md) | 016 | Draft |
| 023 | [Admin dashboard & stats (affiliates, stores, products, users)](023-admin-dashboard-reports/SPEC.md) | 019, 021 | **Done** |
| 024 | [Affiliate anti-fraud rules](024-affiliate-fraud-checks/SPEC.md) | 019 | Draft |

## How the money trail works

```
Store owner sets affiliate % per product (009)   e.g. Perfume A 200 TND → 10 %
                         │
Affiliate requests to work with the store → store owner accepts (012)
                         │
Affiliate gives code #DRF-45 to a client (012)
                         │
 ┌───────────────────────┴───────────────────────┐
 Client types #DRF-45 on a product   Affiliate orders for the client
 └───────────────────────┬───────────────────────┘
                         ▼
Code checked per product: affiliate must work with that store (012)
Checkout (014) freezes on each order line: price, qty, affiliate, %, expected coins
                         │                                  cash on delivery (015)
Store owner marks the order DELIVERED (016)
                         │
                         ▼
Ledger (019): +20 coins  [affiliate, order, client, product, store, 200 TND × 10 %]
Refused / returned parcel → marked NotDelivered → no coins (Delivered is final)
                         │
Affiliate wallet (020) · Admin stats per affiliate / store / product / user (023)
Store monthly commission invoice (025) = every commission (e.g. 2 × 50 TND × 50 % = 50 TND)
      store owner pays → affiliate requests payout → admin verifies → schedules cash / transfer date → paid (021)
      unpaid → store suspended (not deleted): no new orders, existing orders still processed
```
