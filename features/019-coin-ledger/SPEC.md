# 019 — Coin ledger

- **Status:** Done (store billing of the entries: 025; payouts: 021)
- **Phase:** 5 — Coins & payouts
- **Depends on:** 004, 014, 016
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

When a store owner confirms an affiliate order as **Delivered**, the affiliate's counter goes up — with **full traceability** of every coin.

## Coin calculation (per order line)

`coins = line total (TND) × affiliate % ÷ coin value (TND per coin)`

- Affiliate % = the product's % (or the store default), **frozen on the order line** at checkout (014), together with the expected coins and the coin value (on the order).
- Not a fixed amount: a 200 TND perfume at 10 % → 20 coins; a 50 TND cream at 5 % → 2.5 coins (coin value 1 TND).
- Coins stored as decimal with 3 decimals.
- *Correction 2026-10-21:* the first draft wrote `× Coin.ValueInTnd`; a coin worth 0.5 TND must give **more** coins for the same commission, so it is a division (already what 014 freezes).

## Ledger entry (append-only) — `coin_entries`

| Field | |
|---|---|
| Id, CreatedAt | |
| AffiliateId | who earns |
| Type | `Earned`, `PayoutLocked`, `PayoutReleased` (021), `Adjustment` |
| Coins (signed) | |
| OrderId, StoreOrderId, OrderLineId, OrderReference | which order |
| ClientName / ClientPhone / ClientUserId | which client |
| ProductId, ProductName, StoreId, StoreName | which product / store |
| Quantity, LineTotal, AffiliatePercent, CoinValueInTnd | values used |
| RelatedEntryId | for adjustments / payouts |
| InvoiceId | store invoice (025) this commission was billed on — set once |
| Reason, CreatedById | for adjustments |

Cached totals per affiliate in `coin_accounts` (balance, total earned), always equal to the sum of the entries.

## Acceptance criteria

- [x] Delivered → one `Earned` entry per order line that has an affiliate, **written in the same transaction as the status change** (not by an after-commit handler, which may fail silently)
- [x] No coins for `Cancelled` / `NotDelivered` orders (nothing is written)
- [x] Corrections only through an admin `Adjustment` entry (signed, reason required, optional link to the corrected entry, audited `coins.adjustment`) — entries are never edited or deleted
- [x] Balance = sum of entries; the cached balance always equals the sum (tested, including two stores delivering for the same affiliate at once — atomic `INSERT … ON CONFLICT DO UPDATE` increments, no lock)
- [x] Idempotent: a line is never credited twice (filtered unique index on OrderLineId for `Earned`, plus a check before writing)
- [x] Each entry carries `StoreId` + `InvoiceId` (null) so it can be billed once on the store's monthly invoice (025)
- [x] **Expected coins** (lines with a code whose parcel is still Placed / Confirmed / Shipped) are computed from order lines — shown to the affiliate and the admin, never written to the ledger
- [x] Orders delivered before the ledger existed are credited by the migration (same values, accounts rebuilt from the entries)
- [x] Affiliate: "Mes coins" on `/affiliate` (balance ≈ TND, earned, expected, latest entries with the client's phone masked); admin: `/admin/coins` (totals, ledger with filters, adjustment)
