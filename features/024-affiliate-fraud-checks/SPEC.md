# 024 — Affiliate anti-fraud rules

- **Status:** Draft
- **Phase:** 6 — Admin & operations
- **Depends on:** 019
- **Created:** 2026-10-19

## Goal

Protect the coin system from abuse.

## Acceptance criteria

- [ ] Block self-affiliation (own store, own orders) — already enforced in 012, tested here end-to-end
- [ ] Flag suspicious patterns: many orders with one code to the same phone/address, affiliate and store owner sharing phone/address, high "not delivered" rate, store marking its own orders delivered too fast
- [ ] Admin can freeze an affiliate's coins and review flagged entries
