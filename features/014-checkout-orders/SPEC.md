# 014 — Checkout & orders

- **Status:** Done (store-owner order screens come with 016)
- **Phase:** 3 — Orders & affiliate codes
- **Depends on:** 012, 013
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

The client (or an affiliate on their behalf) places a **cash-on-delivery** order. Each product line carries its own optional affiliate code. The order is split per store.

## Acceptance criteria

- [x] Checkout form (`/checkout`): client full name, phone (+216), governorate, address, note; payment = cash on delivery (the only option)
- [x] **Guest checkout** (no account needed, the browser cart is sent); a signed-in client gets name and phone pre-filled and the server cart is ordered
- [x] Affiliate code is **per order line** (from the cart, 013), optional
- [x] **Every line and every code is re-checked at checkout**, with the cart's own rules (`CartRules`) and `AffiliateCodeService.MatchAsync` (012): a product no longer orderable → `checkout.cartNotReady`, a code that stopped counting (partnership ended…) → `checkout.codeRefused`; the client is sent back to the cart, where each line says why. Stock and order are written **in one transaction**
- [x] Each store must **deliver the chosen governorate** (`checkout.cityNotDelivered`); the checkout page warns per store before sending
- [x] **Affiliate mode** ("Pour un client"): an active affiliate checks out for a client (client details entered by the affiliate); their **own code is applied only to products of stores they work with**, other products are ordered without code, codes typed in the cart are ignored; `PlacedByAffiliateId` set
- [x] One **Order** split into one **StoreOrder** per store, each with **OrderLines**; delivery fee per store (frozen), `AmountToCollect` = lines + delivery
- [x] Each order line **freezes**: product name / slug / variant options (FR / AR / EN), quantity, unit price, line total, **affiliate id**, **partnership id**, **code**, **affiliate %**, **expected coins** (`line total × % ÷ coin value`, coin value frozen on the order)
- [x] Order reference shown to the client: `CMD-2026-000123` (PostgreSQL sequence `order_number_seq`)
- [x] Stock decremented **atomically**: one conditional `UPDATE … WHERE stock >= quantity` per line inside the order transaction — two clients on the last unit: one order, one refusal (tested)
- [x] No order possible on a disabled / deleted / suspended store or a closed one (same rule as the cart)
- [x] Client: "Mes commandes" (`/orders`) list and detail (`/orders/:reference`), confirmation page after checkout; guest: "Suivre une commande" (`/orders/track`) with reference + phone; affiliate: "Ventes avec mon code" on `/affiliate` (sale, %, expected coins, status — no client contact unless they placed the order)
- [x] Domain event `OrderPlacedEvent` (notifications: 022)
- [ ] Store owner order list / processing — **016**

## Decisions (2026-10-21)

- A refused code or an unorderable line **stops the whole checkout** (nothing is partially ordered); the cart explains each line.
- The store's **delivery zones are enforced** at checkout.
- Guests find their order with **reference + the phone number** given at checkout (both must match, rate limited).
- In affiliate mode the order has no `ClientId` (the client may have no account); it shows in the affiliate's "Mes commandes" as "Pour <client>".
- Product attributes beyond the variant options are not copied onto the line (name, price and options are); the product row is never deleted, so its page stays reachable.
- Abuse of affiliate mode (an affiliate "ordering for a client" for themselves) is left to the anti-fraud rules (024).
