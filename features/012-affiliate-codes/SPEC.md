# 012 — Affiliate codes & store partnerships

- **Status:** Done (notifications wait for 022)
- **Phase:** 3 — Orders & affiliate codes
- **Depends on:** 002, 007, 011
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

Every affiliate has a **unique code** like `#DRF-45`. An affiliate can only earn on products of **stores they work with**. A code used on a product of a store the affiliate does not work with is **refused**.

## Partnership (affiliate ↔ store)

```
Affiliate sends a request to a store ──> Store owner accepts or refuses   (only the store owner decides)
Accepted ──> active partnership ──> store owner can end it (future orders only)
Requested ──> the affiliate can withdraw it (Cancelled)
```

- An affiliate can work with **multiple stores** (one open partnership — requested or accepted — per store; unique index as backstop).
- Statuses: `Requested` → `Accepted` / `Refused` / `Cancelled`; `Accepted` → `Ended`. Full history kept.
- After a refusal, a withdrawal or an end, the affiliate can send a new request.

## User stories

- As an affiliate, I want to send a request to a perfume store to work with them as an affiliate.
- As an affiliate, I want to work with several stores and see the status of each request.
- As a store owner, I want to accept or refuse each request and see the affiliates who work with my store.
- As a store owner, I want to stop working with an affiliate.
- As a client, I want to type the code of the person who recommended a product.

## Acceptance criteria

### Codes
- [x] Code generated when an affiliate account is created or an affiliate role request is approved — **done in 002 / 027** (`DRF-45`, unique, never reused)
- [x] Input normalization `#DRF-45`, `drf-45`, `DRF45` → `DRF-45` — **done in 002** (`AffiliateCode.Normalize`)
- [x] Affiliate page (`/affiliate`, "My stores") shows the code + copy button, and a **share link** per partner store; the product page shows the share link of that product (`?ref=CODE`, read by the cart in 013)
- [x] Admin can deactivate / re-enable an affiliate code (fraud), reason required, audited (`user.affiliateCode.disable / enable`)

### Partnerships
- [x] Affiliate: "Ask to work with this store" on a store page and on product pages (optional message); one open request per store
- [x] Affiliate: "My stores" — partner stores, waiting requests (withdraw), history with the refusal / end reason
- [x] Store owner: "My affiliates" tab — incoming requests (affiliate name, code, member since, number of partner stores, message), **accept / refuse** (optional reason); list of partners; **end** a partnership (optional reason); history
- [x] Only the owner of that store can accept, refuse or end (another store's partnership is a 404); an admin sees every partnership (`/admin/partnerships`) and can end one for fraud — reason required, audited on the store (`partnership.end`)
- [x] Ending a partnership never affects orders already placed — there are no orders yet; 014 freezes the affiliate on the order line, so an end only blocks new checks
- [ ] Notifications: new request (store owner), accepted / refused / ended (affiliate) — **022**

### Validation (used by cart 013 and checkout 014)
- [x] `AffiliateCodeService.ValidateAsync(code, productId, clientId)` → valid only if: code exists, affiliate can earn (active account, Affiliate role, code not deactivated), the product is visible in the shop, **accepted partnership between the affiliate and the product's store**, affiliate is not the store owner, affiliate is not the client
- [x] One error code per case, translated FR / AR / EN: `affiliateCode.invalid`, `unknown`, `inactive`, `productUnavailable`, `notPartner`, `ownStore`, `ownCode`
- [x] `POST /api/affiliate-codes/check` rate limited (`RateLimiting:AffiliateCodePerMinute`, default 30 / min, per user or per IP)

## Decisions (2026-10-21)

- A request is only possible for an **active affiliate** whose code is not deactivated, towards a store **visible in the shop** (active or closed), never their own store.
- The refusal / end reason is **optional** for the store owner (shown to the affiliate); **required** for an admin.
- The store owner sees no contact details of the affiliate — name, code, member since, number of partner stores, message.
- A successful code check returns a **short name** ("Sana T.") and the product's current commission %, so a valid code reveals little about its owner.
- Deactivating a code keeps the partnerships: re-enabling brings everything back.
- Owner decisions are not audited (not admin actions); admin end and code deactivation are.
