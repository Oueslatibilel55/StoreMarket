# 016 — Order lifecycle (store owner & client)

- **Status:** Done (coins on delivery: 019; notifications: 022)
- **Phase:** 3 — Orders & affiliate tracking
- **Depends on:** 014, 015
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

Store owners process their orders. **Delivered** is the moment coins are credited to the affiliate.

## Statuses (per StoreOrder)

`Placed → Confirmed → Shipped → Delivered`, plus `Cancelled` (by client before shipping, or by store) and `NotDelivered` (client refused / parcel returned to the store).

**Delivered is final**: there are no returns after it. A refused or returned parcel is marked `NotDelivered`, never `Delivered` — so no coins are created.

| Who | Allowed |
|---|---|
| Store | Placed → Confirmed · Placed / Confirmed → Shipped · Shipped → Delivered · Shipped → NotDelivered (reason: refused / unreachable / wrong address / other) · Placed / Confirmed → Cancelled (reason required) |
| Client | Placed / Confirmed → Cancelled (reason optional) |
| Admin | any change except out of Delivered — reason required, audited |

## Acceptance criteria

- [x] Store owner (`/my-store/orders`): orders list (tabs per status with counts, search by reference / client / phone, "affiliate code" badge; API also filters with / without code), detail (client contact with tap-to-call, address, note, lines with the affiliate commission, history), change status — **allowed transitions only** (the detail lists the next steps)
- [x] **Store owner confirms Delivered** → payment `Collected` (015) + domain event `StoreOrderDeliveredEvent` (019 credits coins) + `StoreOrderStatusChangedEvent` on every change (022)
- [x] Client can cancel before `Shipped` (order page, per store)
- [x] `NotDelivered` requires a reason (refused, unreachable, wrong address, other) + optional details — kept for fraud stats (024)
- [x] A suspended or disabled store **keeps processing orders already placed**; only a deleted store can't
- [x] Status history stored (who — role and id —, when, from, to, reason) and shown to the store, the client and the admin
- [x] Admin (`/admin/orders`): every order (search, status filter) and its history; can force a status with a reason (audited `order.forceStatus`, target Order)
- [x] **Stock**: a cancelled or not-delivered parcel puts its stock back; an admin re-opening one takes it again (refused if the stock is gone)
- [x] **Concurrency**: two changes at once on the same store order → one wins, the other gets `order.changed` (PostgreSQL `xmin` token) — tested with two simultaneous "Delivered"

## Decisions (2026-10-21)

- The store may skip "Confirmed" (Placed → Shipped) but never skip shipping (no Placed → Delivered).
- Cancelling by the store needs a reason (shown to the client); by the client it is optional.
- Delivered stays final **even for an admin** — a mistake there will be corrected in the ledger (019), never by moving the order.
- Store order lists are newest first (like a mailbox), with status tabs as the work queue.
