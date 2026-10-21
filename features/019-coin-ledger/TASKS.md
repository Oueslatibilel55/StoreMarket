# 019 — Coin ledger — Tasks

## Backend
- [x] Domain: `CoinEntry` (`Earned` from a delivered line with all traceability fields, `Adjustment` signed with reason), `CoinEntryType`, `CoinAccount` (cached totals), error codes `coins.*`
- [x] Application: `CoinLedgerService` (credit on delivery — idempotent, called by `OrderLifecycleService` inside the order transaction; summary with expected coins; affiliate and admin lists; audited adjustment); `ICoinLedger`, `ICoinQueries`; affiliate sales summary split into expected / earned
- [x] Infrastructure: `CoinEntryConfiguration` (filtered unique index per Earned line, restrict FKs), `CoinAccountConfiguration`, `CoinLedger` (append + atomic account upsert, refuses to run outside a transaction), `CoinQueries` (masked phones for the affiliate); migration `CoinLedger` with **back-fill of already delivered lines**, created and applied (Sana: 156.8 coins for CMD-2026-000001)
- [x] Api: `CoinEndpoints` (`/api/me/coins`, `/api/me/coins/entries`, `/api/admin/coins/entries|totals|affiliates/{id}|adjustments`)
- [x] Unit tests: `CoinEntryTests` (traceability, frozen coin value, no affiliate → nothing, delivered only, adjustment rules)
- [x] Integration tests: `CoinLedgerEndpointsTests` (credit on delivery + expected → earned + masked phone, cancelled / not delivered write nothing, two stores at once, admin adjustment audited + append-only; cached balance = sum checked in the database)

## Frontend
- [x] `core/coins/coin-api.ts`; `/affiliate`: "Mes coins" card (balance ≈ TND, earned, expected, latest entries), sales summary shows earned coins
- [x] Admin: "Coins" menu entry, `/admin/coins` (totals, filters in the URL, ledger rows linking affiliate and order, correction modal from a row or free with affiliate search)
- [x] i18n FR / AR / EN (984 keys in each file)

## Done
- [x] Acceptance criteria verified (tests: 189 unit, 131 integration, 54 frontend; Chrome: admin ledger and Sana's coins)
- [x] Docs updated: `docs/CONTEXT.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`, `features/README.md`, this spec
