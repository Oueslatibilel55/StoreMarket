# 025 — Store billing — Tasks

## Backend
- [x] Domain: `BillingOffer`, `StorePayment`, `CommissionInvoice` (+ lines), `Store.PaidUntil` / `Suspend` / `Reactivate` / `SetSubscriptionPrice`, `BillingRules.Evaluate` + unit tests (`BillingTests`)
- [x] Setting `subscription.trialDays`; error codes (`DomainErrors.Billing`, `BillingErrors`); audit actions `billing.*`; image purpose `PaymentReceipt`
- [x] Persistence + migration `Billing` (sequences, unique billed ledger entry, one invoice per store + month, `xmin` tokens, drop `coin_entries.invoice_id`, existing stores paid 30 days ahead)
- [x] `BillingService` (admin: price, offers, quote, record / approve / reject payments, run checks; owner: statement, status, declare) + hourly `BillingJob` (`Billing:JobEnabled`)
- [x] `IBillingQueries` (store statement, invoice detail, payment queue, overdue list, revenue per month, offers usage)
- [x] Endpoints `/api/admin/billing/*`, `/api/me/store/billing/*`
- [x] Payout screen reads the billing invoice from the lines; 023 overview billing numbers
- [x] Integration tests `BillingEndpointsTests` (invoice once per month with lines, suspension after grace, declaration → approval, rejection reason, partial payments, offer + paid-until, lapse → suspension → reactivation, audit, access)

## Frontend
- [x] `BillingApi` (`core/billing`); shared `app-billing-banner`, `app-payment-fields`, `app-statement-table`, `app-payment-list`, `app-invoice-view`, `app-declare-payment`, `app-reject-dialog`
- [x] Admin `/admin/billing` (to check, behind, offers + editor, revenue, run checks); billing section on the admin store page; `/admin/billing/invoices/:id`
- [x] Owner `/my-store/billing` + `/my-store/billing/invoices/:id`; banner on every store-owner page (in the tabs); declare with receipt photo
- [x] Print-ready invoice (`print:hidden` on header, footer, menus, buttons)
- [x] i18n fr / ar / en, unit tests (`billing-api.spec.ts`)

## Done
- [x] Acceptance criteria verified (reminder notifications wait for 022)
- [x] Docs updated

## Notes

- Checked in the browser on the demo data (2026-10-23): admin store billing section and its dialog, offers editor, owner billing page and
  "Pay the subscription" dialog; every billing page has no horizontal scroll at 320 px. Fixed on the way: selects bound with `[value]`
  showed the first option until `[selected]` was added on the options.
- Tests move through months with `BillingService.RunForStoreAsync(storeId, date)` — only that store is touched, so tests stay parallel.
- A declared payment stops the countdown for what it pays (spec): declaring lifts a suspension at the next check; a rejection puts it back at once.
- Suspensions / reactivations by the job are not in the audit log (no admin actor); the store's status and `SuspendedAt` show them.
- Receipt photos are served like other images (`/uploads/…`, unguessable path) — a private file area could come later.
- Later: reminders and suspension notifications (022), server PDFs if a print is not enough, credit carried when a month's net is negative.
