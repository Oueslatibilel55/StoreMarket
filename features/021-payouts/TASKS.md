# 021 — Payout requests — Tasks

## Backend
- [x] Domain: `PayoutRequest` + `PayoutEvent`, `PayoutStatus`, `PayoutMethod`, `DomainErrors.Payout`; `CoinEntry.PayoutLocked / PayoutReleased` + `PayoutRequestId`
- [x] Application: `PayoutService` (request under the account lock, cancel, verify, reject, schedule, paid; audit; `payout.changed`), `PayoutErrors`, DTOs; `ICoinLedger.BalanceAsync / LockBalanceAsync`; `CoinSummaryDto.Locked / PaidOut`
- [x] Infrastructure: `PayoutRequestConfiguration` (sequence, open-request unique index, xmin), `PayoutRepository`, `PayoutQueries` (mine, list, counts, verification screen with covered entries)
- [x] Migration `Payouts` (xmin column removed by hand) — **applied**
- [x] Api: `PayoutEndpoints` (`/api/me/payouts`, `/api/admin/payouts`), error → status mapping
- [x] Unit tests `PayoutRequestTests` (13)
- [x] Integration tests `PayoutEndpointsTests` (7): minimum / balance / one open, full admin flow + audit, reject & cancel release, concurrent requests, bank transfer, list / counts / verification, authorization

## Frontend
- [x] `core/payouts/payout-api.ts` (affiliate + admin), `shared/payouts/payout-timeline`
- [x] Wallet tab "Retraits" (`wallet-payouts.ts`): progress to the minimum, request modal (cash / transfer, RIB check), open request with steps and date, cancel, past requests; counters "réservés" / "retirés"; "Demander un retrait" on `/affiliate`
- [x] Admin `/admin/payouts` (counts, shortcuts, filters in the URL) and `/admin/payouts/:id` (verification screen, step modals); nav entry enabled; audit link for `Payout`
- [x] i18n `payouts.*`, `errors.payout.*`, `audit.actions.payout.*` in fr / ar / en
- [x] Unit test `payout-api.spec.ts`

## Done
- [x] Acceptance criteria verified (Chrome FR + AR: request by transfer, verify, schedule, paid; widths 320 / 375 / 768)
- [x] Docs updated (CONTEXT decision + open question closed, CLAUDE.md, features/README.md)

## Notes
- Left for later: receipt photo on "paid" (needs an image purpose), notifications at each step (022), fraud flags on the verification screen (024), store billing status (025 — shown as "not billed yet" until then).
