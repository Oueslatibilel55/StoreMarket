# 023 — Admin dashboard & stats — Tasks

## Backend
- [x] `StatsPeriod` (from / to defaults and limits) + unit tests
- [x] DTOs: overview, affiliate, store, product stats (`StatsDtos.cs`)
- [x] `IStatsQueries` / `StatsQueries` (aggregates in SQL over order lines + ledger; affiliate breakdown reuses `ICoinQueries.BreakdownAsync`)
- [x] Admin orders list: filters `affiliateId`, `clientId`, `productId`, `from`, `to`, `dateField` (Placed / Delivered)
- [x] Admin products list: effective affiliate % per product
- [x] Endpoints `/api/admin/stats/*` and `/api/me/store/stats` (`StatsEndpoints.cs`)
- [x] Integration tests `StatsEndpointsTests` (numbers checked against a known set of orders, order list drill-down, 404 / 403)

## Frontend
- [x] `StatsApi` (`core/stats`), shared `app-period-picker`, `app-kpi`, `app-conversion-tiles`, `app-stats-table` (CSV), `app-daily-chart`, `app-store-stats-view`; CSV helpers moved to `shared/csv.ts` (wallet uses them too)
- [x] `/admin` dashboard (overview, daily chart, top lists) — default admin page, first nav entry
- [x] Affiliate stats on the user page; links to orders as client / with their code
- [x] Store stats on the store page; product stats page `/admin/products/:id` (linked from the products list, which shows each product's %)
- [x] Admin orders / coins / partnerships lists read the drill-down filters from the URL (`app-filter-chips`)
- [x] Store owner dashboard `/my-store/stats` (new tab)
- [x] i18n fr / ar / en
- [x] Unit tests (`stats-api.spec.ts`: period presets, shares, weekly bars, CSV)

## Done
- [x] Acceptance criteria verified (billing parts wait for 025)
- [x] Docs updated (`features/README.md`, CLAUDE.md, ARCHITECTURE, ADR 0005 `stat-tile`)

## Notes

- Checked in the browser on the demo data (2026-10-23): dashboard, store, product and affiliate pages; every 023 page at 320 px has no horizontal scroll
  (fixed on the way: the admin store page's config card grew with long links).
- The chart shows one measure at a time (sales / placed / delivered — never two scales), weekly bars past 92 days; a table view is under it.
- Days are UTC: an order placed at 00:30 in Tunis (UTC+1) counts on the previous day. Fine for now; a platform time zone setting could fix it later.
- Paid-out tile opens the payouts list with `status=Paid` and the period, but that list filters on the request date, not the paid date.
- Later: billing KPIs and store invoices (025), fraud signals (024), number-filters on the stats tables.
