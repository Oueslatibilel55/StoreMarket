# 020 — Affiliate wallet & history — Tasks

## Backend
- [x] `CoinEntryQuery.ProductId`, `CoinBreakdownBy`, `CoinBreakdownRow` (Application DTOs)
- [x] `ICoinQueries.ExportAsync` / `BreakdownAsync`; `CoinQueries` uses one `Filter` for list, export and breakdown
- [x] `CoinLedgerService.ExportMineAsync` (max 5000) / `BreakdownAsync`
- [x] Endpoints `/api/me/coins/entries` (more filters), `/export`, `/breakdown`
- [x] `AffiliateOrderLine.ClientName`
- [x] Integration test `Wallet_FiltersBreakdownAndExport_OnlyMyEntries`
- [x] No migration (read-only feature)

## Frontend
- [x] Route `/affiliate/wallet` (children under `/affiliate`), `features/affiliate/wallet/`
- [x] `CoinApi.export` / `breakdown`, `productId` filter
- [x] Tabs: history (filters, pagination, CSV in the active language), breakdown (product / store, period, share bar), orders with my code (client name)
- [x] Loading / empty / error states; link from the "Mes coins" card
- [x] i18n `wallet.*` in fr / ar / en

## Done
- [x] Acceptance criteria verified (Chrome FR + AR, widths 320 → 1024, no horizontal scroll)
- [x] Docs updated (`features/README.md`, `CLAUDE.md`)

## Notes
- The CSV export is written in the browser, not on the server, so the column names follow the language the user picked. The browser download itself was not clicked during the Chrome check; the data comes from the tested `/export` endpoint.
