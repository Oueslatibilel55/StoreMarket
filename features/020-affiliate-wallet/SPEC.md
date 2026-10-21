# 020 — Affiliate wallet & history

- **Status:** Done
- **Phase:** 5 — Coins & payouts
- **Depends on:** 019
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

Affiliates see exactly what they earned, from which order, client and product.

## Design

- Page `/affiliate/wallet` ("Mon portefeuille"), linked from the "Mes coins" card on `/affiliate`. Three tabs: **Historique**, **Répartition**, **Commandes avec mon code**. Tab, filters, split and page live in the URL.
- Read-only: everything comes from the ledger (019) and the order lines (014). Nothing new is stored, so no migration.
- `GET /api/me/coins/entries` accepts `type`, `storeId`, `productId`, `search`, `from`, `to` (whole days, inclusive), `page`, `pageSize`.
- `GET /api/me/coins/export`: the same filters, no paging, newest first, at most `CoinLedgerService.MaxExportRows` (5000). The **client** builds the CSV in the active language (`;` separator, UTF-8 BOM so Excel opens it directly, text cells quoted, cells that look like formulas get a leading `'`). That way the server does not need to know the UI language.
- `GET /api/me/coins/breakdown?by=Product|Store&from&to`: `Earned` entries grouped by product (with its store) or by store, biggest first. Adjustments are left out on purpose: they are not sales.
- Client phones stay masked in every affiliate view (list, export). Orders with my code show the client's **name** only.

## Acceptance criteria

- [x] Counter: available coins (≈ TND value), expected coins (orders not delivered yet), total earned, value of one coin. **Total paid out** comes with 021 (payouts).
- [x] Orders with my code: reference, date, client name, store, product, quantity, status, % and expected / earned coins
- [x] Coin history: date, type, product, store, client (name + masked phone), order ref, quantity, line total, %, coins, reason
- [x] Coins earned **per product** and **per store** over a period, with each row's share; a row opens its history
- [x] Filters: date range, store, product, type, search; CSV export
- [x] An affiliate only ever sees their own entries (tested)
