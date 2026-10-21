# 023 — Admin dashboard & stats

- **Status:** Draft
- **Phase:** 6 — Admin & operations
- **Depends on:** 019, 021
- **Created:** 2026-10-19

## Goal

The admin sees the full history and stats of every affiliate, store, product and user.

## Acceptance criteria

### Overview
- [ ] KPIs (period filter): orders, delivered orders, sales TND, active stores, active affiliates, coins earned / expected / paid out, payouts pending, subscription revenue, commissions billed vs collected vs paid to affiliates, suspended stores

### Per affiliate
- [ ] **Partner stores** (requests, accepted, refused, ended) with dates
- [ ] Profile, code, status; **history of orders** with their code (status, client, store, products)
- [ ] **Coins history** (every ledger entry) and **coins earned per product** and per store
- [ ] Conversion: orders placed vs delivered vs not delivered vs cancelled

### Per store
- [ ] Store **configuration** (category, default %, delivery settings, status), **products** list with each product's %
- [ ] **Partner affiliates** and pending requests; subscription price, offers used, paid-until date, invoices
- [ ] Stats: orders, sales TND, orders via affiliates, coins generated for affiliates, top products, rating

### Per product
- [ ] Sales, quantity sold, orders via affiliates, **coins earned by affiliates on this product**, rating

### Per user
- [ ] Roles, orders as client, affiliate stats if affiliate, store link if owner, account status

### Common
- [ ] Every list: search, filters, paging, CSV export; every number clickable down to the orders / ledger entries behind it
- [ ] Store owner dashboard: own sales, sales via affiliates, coins given to affiliates per product
