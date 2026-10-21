# 013 — Cart (panier)

- **Status:** Done
- **Phase:** 3 — Orders & affiliate codes
- **Depends on:** 011, 012
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

The client collects products before checkout. The cart is **grouped by store**, and the **affiliate code is set per product**, not for the whole cart.

## Layout

```
Panier
├── Store A — Perfumes Tunis                 delivery fee 7 TND
│   ├── Perfume X ×2   100 TND   code: #DRF-45 ✓ Recommandé par Sana T.
│   └── Perfume Y ×1    80 TND   code: —           [+ Ajouter un code affilié]
│   Subtotal store A: 180 TND
└── Store B — Cosmetics Sfax                 delivery fee 8 TND
    └── Cream Z  ×1     30 TND   code: #DRF-45 ✗ "Cet affilié ne travaille pas avec cette boutique."
Récapitulatif: articles · livraison (per store) · total — cash on delivery
```

## Acceptance criteria

- [x] Add / update quantity / remove / empty; **stock checked** on add and when the quantity goes up (going down is always allowed)
- [x] Lines **grouped by store**, with store subtotal + delivery fee, then grand total; delivery is counted once per store that has something orderable
- [x] **Affiliate code per line (product)**, optional; can be set, changed or removed per line; typed in any spelling (`#drf45` → `DRF-45`)
- [x] Code checked with `AffiliateCodeService.ValidateAsync` (012) **every time the cart is read**: refused if the affiliate does not work with that product's store (or own code, own store, inactive…) — error shown on that line only
- [x] Share link `/products/{slug}?ref=DRF-45` or `/stores/{slug}?ref=DRF-45` (012) **remembers the code for that store for 30 days** and pre-fills it on the lines added from that store (still validated); the product page says "Recommandé par Sana T." when the code counts there
- [x] **Guest cart** kept in the browser (`localStorage`), shown through `POST /api/cart/preview`, **merged into the account at sign-in** (quantities add up, the account's code wins)
- [x] Products that can't be ordered are **flagged per line** and left out of the totals: removed / disabled product or store (`productUnavailable`), closed store (`storeClosed`), variant missing or removed, out of stock, not enough stock
- [x] Checkout is blocked (`canCheckout = false`) while a line is flagged **or carries a refused code** — the client fixes or removes it; the checkout itself comes with 014

## Decisions (2026-10-21)

- Signed-in users: cart **on the server** (`carts`, `cart_lines`), one per user, kept between devices. Guests: in the browser, merged at sign-in.
- Prices, stock and code validity are **never stored** in the cart: they are read fresh on every view and checked again at checkout (014).
- A refused code **blocks checkout** until removed or fixed (rather than silently dropping it), so the client knows whose code counts.
- Limits: 50 different products per cart, 99 units per line (`Cart.MaxLines`, `Cart.MaxQuantity` — technical bounds, the real limit is the stock).
- Cart calls that check codes (add, set code, preview, merge) are rate limited (`RateLimiting:CartPerMinute`, default 60 / min per user or IP).
- Variants: the line keeps the variant id; if the owner removes that combination the line shows `variantUnavailable` instead of disappearing.
