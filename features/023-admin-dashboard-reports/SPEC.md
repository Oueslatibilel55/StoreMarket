# 023 — Admin dashboard & stats

- **Status:** Done
- **Phase:** 6 — Admin & operations
- **Depends on:** 019, 021
- **Created:** 2026-10-19

## Goal

The admin sees the full history and stats of every affiliate, store, product and user.

## Acceptance criteria

### Overview
- [x] KPIs (period filter): orders, delivered orders, sales TND, active stores, active affiliates, coins earned / expected / paid out, payouts pending, suspended stores
- [x] Billing KPIs (subscription revenue, commissions billed vs collected vs paid to affiliates) — added with 025

### Per affiliate
- [x] **Partner stores** (requests, accepted, refused, ended) with dates
- [x] Profile, code, status; **history of orders** with their code (status, client, store, products)
- [x] **Coins history** (every ledger entry) and **coins earned per product** and per store
- [x] Conversion: orders placed vs delivered vs not delivered vs cancelled

### Per store
- [x] Store **configuration** (category, default %, delivery settings, status), **products** list with each product's %
- [x] **Partner affiliates** and pending requests
- [x] Subscription price, offers used, paid-until date, invoices — the billing section of the store page (025)
- [x] Stats: orders, sales TND, orders via affiliates, coins generated for affiliates, top products, rating

### Per product
- [x] Sales, quantity sold, orders via affiliates, **coins earned by affiliates on this product**, rating

### Per user
- [x] Roles, orders as client, affiliate stats if affiliate, store link if owner, account status

### Common
- [x] Every list: search, filters, paging, CSV export; every number clickable down to the orders / ledger entries behind it
- [x] Store owner dashboard: own sales, sales via affiliates, coins given to affiliates per product

## Decisions (2026-10-23)

- **Reuse what exists.** Lists already built (orders 016, ledger 019, partnerships 012, products / stores 010, payouts 021)
  are the drill-down targets; 023 adds the **numbers** and the links into those lists with the right filters.
  The admin orders list gains filters `affiliate`, `client`, `product`, `from`, `to` so a number opens the orders behind it.
- **One stats read model**: `IStatsQueries` (Infrastructure, `AsNoTracking`, aggregates in SQL), endpoints under
  `/api/admin/stats/*` (`overview`, `affiliates/{id}`, `stores/{id}`, `products/{id}`) and `/api/me/store/stats`
  for the store owner (same numbers as the admin's store view, owner from the token).
- **Period** = `from` / `to` inclusive UTC days, default the last 30 days, at most 366 days for the daily series.
- **Definitions** (one place, `StatsQueries`):
  - *Orders* = store orders (one per store per checkout) **placed** in the period; *conversion* splits that same set
    by current status: open (placed / confirmed / shipped), delivered, cancelled, not delivered.
  - *Delivered* and *sales TND* = store orders **delivered** in the period (`DeliveredAt`), sales = items subtotal
    (delivery fees excluded). *Via affiliates* = delivered lines with an affiliate code.
  - *Coins earned* = `Earned` ledger entries created in the period; *adjustments* shown apart; *expected* = now
    (open lines); *paid out* = payout requests paid in the period; *pending* = open requests now.
  - *Active stores / suspended stores* = now; *active affiliates* = affiliates who earned in the period.
- **Overview** also shows a daily chart (placed orders, delivered sales) and top 5 stores, products (by delivered sales)
  and affiliates (by coins earned) of the period.
- **CSV** on the stats tables (top lists, per store / per product breakdowns), built by the client like the wallet (020);
  the existing lists keep their own exports where they have one.
- Billing numbers (added with 025): subscriptions and commissions collected, commissions billed, due now, payments to check.
