# 021 — Payout requests (coins → TND)

- **Status:** Done
- **Phase:** 5 — Money: coins, payouts & billing
- **Depends on:** 004, 019
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

Affiliates **cannot withdraw directly**. They send a **payout request**; the platform **verifies everything**, then **schedules a date** to hand over cash or make the bank transfer, and marks it paid. 1 coin = 1 TND.

## Flow

```
Affiliate: request payout (N coins, cash or bank transfer)
      │  coins locked
      ▼
Admin: verify ──> reject (reason) ──> coins unlocked
      │
      ▼  approved
Admin: schedule ── cash: date + place/time
      │        └─ bank transfer: planned transfer date
      ▼
Admin: mark as paid (date, reference / who handed the cash) ──> done
```

Statuses: `Requested` → `Verified` → `Scheduled` → `Paid`, or `Rejected` (from Requested / Verified), or `Cancelled` by the affiliate before `Scheduled`.

## User stories

- As an affiliate, I want to request a payout of my coins so that I get my money.
- As an admin, I want to verify a request (orders delivered, store payments received, no fraud flags) before accepting it.
- As an admin, I want to set the date when the affiliate gets the cash or the transfer.
- As an affiliate, I want to see the status and the scheduled date of my request.

## Design

- `PayoutRequest` aggregate (reference `PAY-YYYY-NNNNNN` from the `payout_number_seq` sequence) with its steps in `payout_events` (status, who, when, note). Coins, coin value and TND amount are frozen when the request is sent.
- **Ledger:** sending writes `PayoutLocked` (−N) in the same transaction, after locking the affiliate's `coin_accounts` row (`SELECT … FOR UPDATE`). Two requests (or a request and an adjustment) therefore never spend the same coins. Rejected or cancelled writes `PayoutReleased` (+N). Paid writes nothing, because the locked coins are simply gone. A filtered unique index (`payout_request_id`, `type`) makes sure a request locks once and releases at most once.
- One open request per affiliate: a service check plus a filtered unique index on `payout_requests (affiliate_id)` for Requested / Verified / Scheduled.
- **Verification (decision: the admin decides case by case).** The earned entries a request covers are computed oldest coins first, after the coins taken by earlier requests that were not rejected or cancelled. The first and last entries can be partly covered. The screen also shows coins that come from corrections. Each entry shows whether its store was billed (`InvoiceId`, 025).
- Transitions: Requested → Verified → Scheduled → Paid. Rejected (reason required) from Requested / Verified. Cancelled by the affiliate from Requested / Verified. A scheduled request can be moved to another date. Cash needs a place; a transfer needs only a date; the date can't be in the past. Paid needs a bank reference or the name of who handed the cash, and the paid date can't be in the future.
- `payout_requests` uses `xmin` as concurrency token: two people moving it at once → `payout.changed`.
- `CoinSummaryDto` gained `Locked` and `PaidOut`.

## Acceptance criteria

- [x] **Request button disabled until available coins ≥ `payout.minimumCoins`** (default 80, admin setting); the affiliate sees "X / 80 coins — Y left to reach the minimum" with a progress bar
- [x] Affiliate requests N coins (≥ minimum, ≤ available — **enforced by the API**), cash or bank transfer (bank, holder, 20-digit RIB — **offered again next time**), optional note
- [x] Amount in TND = coins × coin value **at request time** (frozen)
- [x] Requested coins **locked** (`PayoutLocked`); rejection or cancellation releases them (`PayoutReleased`)
- [x] One open request at a time per affiliate (tested with 3 requests at once)
- [x] **Verification screen** (admin): the covered coin entries with order, product, store, delivery date and whether the store was billed (025 — "not billed yet" until then), coins from corrections, affiliate (status, balance, earned) and their other requests. *Fraud flags come with 024.*
- [x] Admin actions: verify, reject (reason), **schedule** (cash: day, time, place / transfer: date; can be moved), **mark as paid** (date, bank reference or who handed the cash). *Receipt photo left out of v1.*
- [x] Affiliate sees status, steps and scheduled date (wallet → "Retraits"); *notifications come with 022*
- [x] Full history for affiliate and admin; every admin action audited (`payout.verify`, `payout.reject`, `payout.schedule`, `payout.paid`)
- [x] Admin payout list: filter by status, method, scheduled dates, search; shortcuts "today's cash handovers" and "this week's transfers"; counts per step and locked coins

## Decisions

- Must every covered commission be paid by its store before the payout is approved? → **No, the admin decides case by case.** The verification screen shows the billing status of each one.
