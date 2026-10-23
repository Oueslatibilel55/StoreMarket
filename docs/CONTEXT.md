# Project Context

## Vision

An **online marketplace with built-in affiliate selling**, in **TND**.
Business owners open a store and list their products. Anyone can become an **affiliate**: they get a unique **affiliate code** (e.g. `#DRF-45`) and bring clients. When an order carrying that code is **delivered**, the affiliate earns **coins**, which are later **converted to money**.
Payment is **cash on delivery**. Every coin must be traceable back to the exact order, client, product and store that produced it.

## Users & roles

At sign-up you choose a **client account** (Buyer) or an **affiliate account** (Affiliate — a person who works with many stores). One account can then hold several roles (Buyer + StoreOwner + Affiliate), but **every extra role is requested and approved by an admin** — never self-service. Store owner is always obtained that way.

| Role | Description | Main needs |
|---|---|---|
| **Visitor** | Not logged in | Browse, search, view stores/products |
| **Buyer (client)** | Customer | Cart, checkout with an optional affiliate code, order history, rate products and stores |
| **Affiliate** | User who brings clients | Own unique code, place orders for clients, see orders and coins earned, request payout |
| **Store owner** | Business owner | Create/configure their store, manage products & images, **set the affiliate % per product**, confirm delivery |
| **Admin** | Platform operator | Manage categories and settings dynamically; see every store, product, user, affiliate, order and coin with stats; disable/delete fake stores and products; handle payouts |

## Core concepts (glossary)

| Term | Meaning |
|---|---|
| **Store** | A business owner's shop. Belongs to one store category; has settings, logo, banner, default affiliate %, rating. Live immediately (no approval). |
| **Store category** | Admin-managed type of store: cosmetics, clothes, perfumes… |
| **Product category** | Admin-managed, hierarchical category for products. |
| **Product attribute** | Admin-defined dynamic field per product category (e.g. *size* for clothes, *volume (ml)* for perfumes). |
| **Affiliate code** | Unique, human-friendly code per affiliate, shown as `#DRF-45`. Generated when an affiliate account is created, or when an admin approves an affiliate role request. Never changes, never reused. |
| **Account type** | What a new account starts as: **client** (Buyer) or **affiliate** (Affiliate, no Buyer role). Every account keeps at least one of these two base roles. |
| **Role request** | A user asks for a role they don't have (Buyer, Affiliate, StoreOwner — never Admin), with an optional message; affiliate requests accept the terms. **Only an admin approves or refuses** (with a reason shown to the user); audited. |
| **Partnership** | Link between an affiliate and a store. The **affiliate sends a request**, **only the store owner accepts** (or refuses, or ends it later). An affiliate can work with **many stores**. |
| **Affiliate order line** | A product line carrying an affiliate code — the code is set **per product** (not per order), by the client or by the affiliate ordering for the client. Valid only if the affiliate works with that product's store. |
| **Affiliate %** | Commission percentage set by the **store owner per product** (e.g. perfumes 5 % or 10 %), with a store-level default. Frozen on the order line at checkout. |
| **Coins** | What the affiliate earns: `line total × affiliate %`, **1 coin = 1 TND** (rate kept as a setting). Not a fixed amount — it depends on each product's price and %. |
| **Coin ledger** | Append-only record of every coin movement: affiliate, order, order line, client, product, store, sale amount, % used, coins. Never edited, only corrected by a new entry. |
| **Expected coins** | Coins of affiliate orders not delivered yet. Shown in stats, **not** in the ledger. |
| **Payout request** | Affiliates never withdraw directly: they **request** a payout, the platform **verifies**, then **schedules a date** for a cash handover or a bank transfer, and marks it paid. |
| **Subscription** | Monthly price the store pays to be on the platform. **Set per store** by the admin, with dynamic **offers** (e.g. pay 3 months → −20 %). |
| **Commission invoice** | Monthly bill of all affiliate commissions earned on a store's products; the store owner pays it, the platform pays the affiliates. |
| **Rating** | 1–5 stars from a client who received the product. Exists for products and stores. |
| **Ranking score** | Weighted rating (Bayesian average) so "4.9 from 300 reviews" beats "5.0 from 1 review". Used to sort search results. |

## Key rules

1. **Everything is dynamic**: categories, attributes and platform settings are data managed from the admin panel; affiliate % is data managed by each store owner.
2. **Coins are credited when the store owner confirms the order as Delivered** — not before.
3. **Full traceability of coins**: no coin exists without a ledger entry linking affiliate → order → order line → client → product → store, with the price and % used.
4. **Cash on delivery only** (v1). The order is paid when it is delivered.
5. **No approval workflow**: stores and products are live as soon as they are created. The admin reviews them afterwards and can **disable or delete** fake ones (e.g. a perfume at 999 999 TND).
6. **Search ranking**: results sorted by ranking score (top-rated stores and products first), then relevance.
7. **Ratings are verified**: only a client who received the product can rate it or its store.
8. **Affiliates only earn on partner stores**: a code on a product is accepted only if the affiliate has an accepted partnership with that product's store — checked in the cart and again at checkout.
9. **No self-affiliation**: an affiliate earns nothing on products from a store they own, or on their own personal purchases — even when one account is both store owner and affiliate.
10. **Single currency: TND.**
11. **No delivery = no coins**: a refused or returned parcel is never marked Delivered by the store owner, so no coins are created. There are no returns after Delivered.
12. **Affiliate % is between 0 and 50 %**.
13. **Store owners fund the commissions.** Each month the store owner pays the affiliate commissions of the month (e.g. 2 perfumes × 50 TND × 50 % = 50 TND). The platform collects it and pays the affiliates. Platform revenue = subscriptions (price per store + dynamic offers).
14. **Unpaid invoice → store suspended, never deleted**: hidden, no new orders, but orders already placed can still be processed and delivered. Paying re-activates it.
15. **Every admin action is audited**: who, what, when, why, and the values before and after — kept forever, visible in the admin panel.

## Scope

### In scope (v1)
- Accounts & roles, admin panel with full stats
- Stores, store categories, store settings
- Products with images, dynamic attributes, stock, affiliate %
- Admin moderation of stores and products (disable / delete)
- Catalog browsing, search with rating-first ranking
- Cart, checkout with affiliate code, cash on delivery, order lifecycle
- Ratings & reviews (products and stores)
- Affiliate codes, coin ledger, wallet, payouts (paid by the platform)
- Monthly store invoice (subscription + commissions), suspension when unpaid

### Out of scope (v1, later candidates)
- Online payment gateway
- Mobile apps
- In-app chat between client and store
- Multi-currency

## Constraints

- **Tech:** Angular 22, .NET 10, PostgreSQL 17 (EF Core, Aspire) — see ADR 0002
- **Currency:** TND only
- **Security / compliance:** personal data (client phone/address), money-related data must be auditable
- **Hosting:** _TBD_
- **Deadlines:** _TBD_

## Decisions taken (2026-10-19)

- [x] Database → PostgreSQL 17 (ADR 0002)
- [x] Payment → **cash on delivery** only
- [x] Affiliate credit → **unique affiliate code** (`#DRF-45`) entered by the client at checkout, or the affiliate places the order for the client
- [x] Coins credited → when the **store owner marks the order Delivered**
- [x] Commission → **% set by the store owner per product** (store default + product override)
- [x] Currency → **TND** only
- [x] Moderation → **no prior approval**; admin can disable / delete stores and products
- [x] Affiliate places orders for clients → yes (in addition to clients typing the code)
- [x] Coin value → **1 coin = 1 TND**
- [x] Guest checkout → **yes**; affiliate code is an **optional** field
- [x] Returns → no coins unless the store owner marks Delivered; no post-delivery returns
- [x] Affiliate % → 0–50 %
- [x] Business model → **monthly subscription** paid by store owners; platform pays affiliates; unpaid store suspended until paid (not deleted)
- [x] Commissions → **paid by the store owner** on the monthly invoice (subscription + commissions); platform pays affiliates
- [x] Suspended store → no new orders, but already-placed orders can still be processed
- [x] UI → **Tailwind CSS**, languages **EN / AR / FR** (ADR 0003)
- [x] Subscription → **price per store**, dynamic offers managed by admin (e.g. 3 months → −20 %)
- [x] Affiliate payouts → **cash or bank transfer** — **request → admin verifies → scheduled date → paid** (no direct withdrawal)
- [x] Minimum payout → **80 TND** (admin setting, can change later)
- [x] Commission payment → store owner gets an **alert** while commissions are unpaid; declares the payment; **admin approves → alert gone**
- [x] Grace period → **3 days** after the due date, with a daily alert "your store will be closed in N days"; then suspended (admin setting)
- [x] Authentication → own accounts, **simple sign-up / sign-in** (JWT + refresh cookie, like RichLife, ADR 0004); **email verification & password reset later** (026)
- [x] Roles → one account, several roles (Buyer, Affiliate, StoreOwner, Admin); the **first admin is set in configuration**, never through public sign-up
- [x] Account type (2026-10-20, feature 027) → chosen **at sign-up**: client (Buyer) **or** affiliate (Affiliate + code, no Buyer); no more self-service "become an affiliate"
- [x] Extra roles (2026-10-20, feature 027) → combinations allowed (Buyer + StoreOwner + Affiliate) **only through a role request approved by an admin** (buyer/store owner → affiliate, affiliate → store owner or buyer); refusal needs a reason shown to the user; audited
- [x] Platform settings (2026-10-20, feature 004) → every business value is an admin setting, audited; **default store subscription 30 TND / month**; **commission invoice due the 10th** of the next month; **affiliate code format is a setting** (letters, digits) for new codes only, every code checked unique; currency fixed TND in v1
- [x] Store categories (2026-10-20, feature 005) → admin-managed, names **FR required + AR / EN optional** (French fallback), unique slug made from the French name, emoji icon until image upload (006), manual order, hide instead of delete once stores use one (007)
- [x] Images (2026-10-20, feature 006) → files on the API server disk, **served as static files** (`/uploads/...`), the database keeps the path; JPEG / PNG / WebP only (checked by content), max size a setting (5 MB)
- [x] Stores (2026-10-20, feature 007) → created by a StoreOwner only (role from an approved request), **one store per owner**, live at once; city and delivery zones = the **24 governorates**; delivery fee + delay; default affiliate % within the settings limits; subscription price **copied from the default (30 TND)** at creation; owner can close / reopen; a category used by stores can only be hidden, not deleted
- [x] Product categories (2026-10-20, feature 008) → tree of **3 levels max**, fields per category of 5 types (text, number + unit, yes/no, one choice, several choices), required / filterable; **sub-categories inherit their parents' fields**; field keys and types never change once created; a hidden category hides its branch
- [x] Products (2026-10-20, feature 009) → **variants in v1** (up to 2 choice fields, own stock / optional price per combination); affiliate % per product (empty = store default) within the limits; required fields only to publish; up to 8 photos; soft delete; **a field or choice used by products can't be deleted** (rename it), nor a category with products
- [x] Public shop (2026-10-20, feature 011, built before 010 at the user's request) → home, store list / page, product catalogue (by category incl. sub-categories, sort by "from" price) / page with variant picker; closed stores stay visible as closed; ordering opens with the cart (013)
- [x] Moderation (2026-10-20, feature 010) → admin **disables / re-enables / deletes** stores and products, always with a reason shown to the owner, all audited, bulk disable for products; delete is a **soft delete** (final, kept for history); a disabled product stays visible and editable for its owner but only an admin brings it back; **revoking StoreOwner disables the store** (re-enabled only by the admin, once the role is given back); the "suspicious products" view (median-based thresholds) was **skipped** — the admin sorts / filters products by price instead
- [x] Coin ledger (2026-10-21, feature 019) → coins = line total × % ÷ coin value (values frozen at checkout); credited **in the same transaction** as Delivered, one entry per affiliate line, never twice; nothing for cancelled / not delivered; append-only, corrected only by signed admin adjustments with a reason (audited); cached balance always equals the sum; expected coins = parcels still on their way (computed, not in the ledger); client phone masked for the affiliate
- [x] Order lifecycle (2026-10-21, features 015 + 016) → per store: Placed → Confirmed → Shipped → Delivered (= cash collected, **final even for an admin**), Cancelled before shipping (store: reason required; client: optional), NotDelivered with a reason (refused / unreachable / wrong address / other); a cancelled or returned parcel puts its stock back; history of every change (who, when, why); admin can force any status but out of Delivered, with a reason, audited; a suspended / disabled store still processes its existing orders; payment behind `IPaymentMethod` (cash on delivery only)
- [x] Checkout (2026-10-21, feature 014) → cash on delivery only; guest checkout allowed (tracked with reference + phone); one order split **per store** (own delivery fee, amount to collect); every line and code **re-checked** at checkout — anything refused stops the whole order; the store must deliver the chosen governorate; lines freeze price, %, affiliate, partnership and **expected coins** (coin value frozen on the order); stock taken atomically in the order transaction; **affiliate mode** = the affiliate's own code on partner stores only; reference `CMD-<year>-<number>`
- [x] Cart (2026-10-21, feature 013) → **grouped by store**, affiliate code **per product line**; signed-in cart on the server, guest cart in the browser merged at sign-in; prices / stock / codes read fresh on every view (never stored); share links `?ref=CODE` remember the code per store for 30 days; lines that can't be ordered (removed, disabled, closed store, stock, variant) are flagged and left out of totals; a **refused code blocks checkout** until removed; delivery fee counted once per store; limits 50 lines / 99 units
- [x] Design system (2026-10-21) → premium look in the Stripe / Lemon Squeezy / Gumroad spirit: slate neutrals, white cards with micro-borders, soft layered shadows, `rounded-xl`, hover lift; reusable Tailwind utilities (`card`, `btn-*`, `field`, `pill-*`…) in `styles.css` (ADR 0005); public footer added
- [x] Payouts (2026-10-21, feature 021) → the affiliate asks for N coins (≥ the minimum setting, ≤ their balance, **one open request at a time**), in cash or by bank transfer (bank, holder, 20-digit RIB — offered again next time); the TND amount is **frozen at request time**; the coins are **locked in the ledger** at once and given back if an admin refuses (reason shown) or the affiliate cancels before a date is set; admin steps: verify → schedule (cash: day, time, place / transfer: date, can be moved) → paid (bank reference or who handed the cash) — final; **the admin decides case by case**: the verification screen lists the earned commissions the request covers (oldest coins first) with their store billing status (025) and the coins from corrections; every admin step audited; no receipt photo in v1; notifications come with 022
- [x] Ratings & reviews (2026-10-22, feature 017) → only the client of a **delivered** parcel rates: **one review per product line and one per store order** (the store's service), 1–5 stars + optional comment (photos later); guest or affiliate-placed orders are rated by the account with **the same phone** (risk accepted until phone verification); editable for **30 days** (setting, frozen per review); shown as "Leila G."; the store owner answers publicly; an admin **hides** abusive reviews with a reason (audited), hidden ones leave the averages; product and store keep average, count and a **Bayesian ranking score** (`ranking.minReviewsPrior`), recomputed in the same transaction; shop cards / pages show stars, "Top rated" sort and home section
- [x] Affiliate wallet (2026-10-21, feature 020) → `/affiliate/wallet`: history with filters (dates, store, product, type, search) and **CSV export in the user's language**, earned coins per product / per store over a period (corrections left out), orders with my code (client name only, phone masked everywhere)
- [x] Partnerships (2026-10-21, feature 012) → affiliate sends a request (optional message), **only the store owner accepts** or refuses (optional reason shown to the affiliate) and can end it later; the affiliate can withdraw a waiting request and ask again after a refusal / end; an affiliate can work with **many stores** (one open partnership per store), never their own; the owner sees name, code, member since and partner-store count — no contact details; an **admin can end** one (fraud, reason required, audited) and **deactivate an affiliate code** (refused everywhere until re-enabled, audited); a code is checked per product (unknown / inactive / not a partner / own store / own purchase each have their own message), rate limited; share links carry `?ref=CODE` for the cart (013)

## Open questions

- [x] ~~Payout verification: must every commission be paid by its store first?~~ → **the admin decides case by case** (2026-10-21, feature 021); the verification screen shows each covered commission and whether its store was billed
