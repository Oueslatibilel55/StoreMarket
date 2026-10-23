# 025 — Store billing (subscription + affiliate commissions)

- **Status:** Done
- **Phase:** 5 — Money: coins, payouts & billing
- **Depends on:** 004, 007, 010, 019
- **Created:** 2026-10-19

## Goal

The store owner pays the platform two things:
1. **Subscription** — price set **per store** by the admin, with **dynamic offers** (e.g. pay 3 months → −20 %).
2. **Affiliate commissions** — every commission earned on their products, billed monthly. The platform then pays the affiliates (021).

If what is due is not paid, the store is **suspended — never deleted** — until it is paid.

## Subscription pricing (all dynamic, admin-managed)

- **Base price per store** (TND / month); a default price is used for new stores (setting 004).
- **Offers**: rules the admin creates, e.g.

| Offer | Rule | Applies to |
|---|---|---|
| Quarterly | pay **3 months** → **−20 %** | all stores |
| Yearly | pay **12 months** → **−30 %** | all stores |
| Launch | **first month free** | new stores, until 31/12 |
| Partner | −50 % | selected stores |

  Each offer: name (FR/AR/EN), months to pay (or any), discount % or fixed TND, start/end dates, target (all stores / store categories / chosen stores), active flag. Price and discount used are **frozen on the payment**.
- Paying N months extends the store's **paid-until date** by N months.

## Commission invoice (monthly)

Example: perfume **50 TND**, affiliate % **50 %**, client buys **2** via `#DRF-45`, store marks **Delivered** → the store owes **2 × 25 TND = 50 TND** on that month's commission invoice.

- One line per `Earned` ledger entry of the month (order ref, product, qty, line total, %, affiliate code, amount); each entry billed **exactly once** (`InvoiceId` on the ledger entry).
- Admin adjustments of a past period go on the next invoice.
- "Current month so far" view for the store owner.

## Commission payment alert

```
Commissions unpaid ──> store owner sees an alert (dashboard banner + notification)
      │                 "You owe 50 TND of affiliate commissions — pay before 10/11"
      ▼
Store owner pays (cash or bank transfer) and clicks "I paid" (amount, method, reference, optional receipt photo)
      │                 alert shows "Payment sent — waiting for approval"
      ▼
Admin checks and approves ──> invoice Paid ──> alert gone
      └─ or rejects (reason) ──> alert back, owner sees the reason
```

## Acceptance criteria

- [x] Admin: set / change the subscription price of each store (history kept); default price for new stores
- [x] Admin: CRUD offers (rules above); best applicable offer suggested when recording a payment
- [x] Admin records a **subscription payment**: months, offer applied, amount, method (cash / bank transfer…), reference → paid-until date extended
- [x] Monthly **commission invoice** generated per store; admin records its payment (full or partial)
- [x] **Alert for the store owner** while any commission invoice is unpaid: banner on every store-owner page + notification (022) — amount due, due date, link to the invoice lines
- [x] Store owner **declares a payment** ("I paid": amount, cash / bank transfer, reference, optional receipt photo) → status `PaymentDeclared`, alert switches to "waiting for approval"
- [x] Admin queue of declared payments: **approve** → invoice `Paid` (or partially paid), **alert disappears**; **reject** with reason → alert back with the reason
- [x] Admin can also record a payment directly (e.g. cash received in person) without a declaration
- [ ] Reminder notifications: when the invoice is issued, a few days before the due date, on the due date, on suspension — **with 022** (banners meanwhile)
- [x] **3-day grace period** (`Billing.GraceDays`, default 3): after the due date of the subscription or of a commission invoice, the store owner sees a **red alert "Your store will be closed in 3 / 2 / 1 day(s) — pay now"** on every page + a notification each day
- [x] Daily job → store **Suspended** when the subscription **paid-until date + 3 days** has passed, **or** a commission invoice is unpaid **3 days after its due date** (a payment declared and waiting for approval stops the countdown)
- [x] **Suspended store**: hidden from catalog and search, **no new orders**; products and data kept; owner can log in, sees a "payment due" banner, can **keep processing orders placed before the suspension**
- [x] Payment recorded → store back to `Active` automatically when nothing is overdue
- [x] Suspension for non-payment is **separate** from admin disable (010)
- [x] Store owner: sees price, active offers, paid-until date, subscription payments, commission invoices with every line
- [x] Admin: overdue list, revenue per month split **subscriptions vs commissions collected**, offers usage (also in 023)
- [x] Every price change, offer and payment decision audited — the hourly suspension / reactivation is not (no admin actor: the store status and `SuspendedAt` show it); reminders with 022
- [x] Invoice / receipt export (PDF) in FR / AR / EN — print-ready pages ("Print → Save as PDF")

## Decisions (2026-10-23)

- **Due date** of a commission invoice: day `commissionInvoice.dueDayOfMonth` (default **10**) of the month after the invoiced month.
- **Paid-until** (`Store.PaidUntil`, a date): a new store starts with `subscription.trialDays` (default 30) free; the
  migration gives existing stores 30 days from the deploy, so nothing is suspended the day billing ships.
  Paying N months extends paid-until by N months **from paid-until, or from today if it is already past**
  (months of a suspended store are not charged afterwards).
- **One payment model** `StorePayment` (reference `PAY-S-YYYY-NNNNNN`): kind *Subscription* (months, monthly price, offer,
  discount and covered period frozen) or *Commission* (the invoice it pays); status *Declared* (by the owner, optional
  receipt photo) → *Approved* / *Rejected* (reason), or *Approved* directly when the admin records a cash payment.
  Only an approved payment counts; a declared one stops the suspension countdown for what it pays.
- **Offers** (`BillingOffer`): names FR/AR/EN, months (exact number of months, or any), discount % **or** fixed TND
  (whole payment), start / end dates, target (all stores / store categories / chosen stores), "first payment only"
  (the launch offer), active flag. The best offer (biggest discount) is suggested; the admin may pick another valid one or none.
- **Commission invoice** (`CommissionInvoice`, reference `COM-YYYYMM-NNNNNN`): issued per store for a past month with
  every not-yet-billed `Earned` entry of its products up to the end of that month (one line each, frozen: order, product,
  qty, line total, %, affiliate code, amount = coins × coin value of the order), plus admin adjustments whose related
  entry belongs to the store (signed). Each entry is billed **once**: unique index on the line's ledger entry — the
  ledger itself is never written (the unused `coin_entries.invoice_id` column is dropped; the payout screen reads the
  invoice from the lines). A month whose net is ≤ 0 is not issued: its lines wait for the next month.
- **Status**: invoice *Issued* → *PartiallyPaid* → *Paid* (approved payments). A payment can't exceed what is left.
- **Suspension** is `StoreStatus.Suspended` (already hidden from the shop and checkout like a disabled store) with
  `SuspendedAt`; paying everything overdue puts it back to *Active* automatically. An admin disable (010) still wins:
  a disabled store is never suspended or unsuspended by billing. A suspended store's owner keeps processing old orders.
- **Overdue** = subscription `PaidUntil + graceDays < today` with no declared subscription payment, or an invoice
  unpaid `DueOn + graceDays < today` with no declared payment for it. "Closes in N days" = during the grace days.
- **Job**: `BillingJob` (hosted service, every hour, `Billing:JobEnabled`) issues the invoices of past months and
  suspends / reactivates stores; it is idempotent, so a missed run (free hosting sleeps) catches up. The admin can run it
  now. Days are UTC.
- **Notifications / reminders** wait for 022: until then the owner sees banners on every store-owner page
  (amount due, due date, days left, waiting for approval, rejection reason, suspended).
- **PDF**: invoice and receipt pages are print-ready (browser "Print → Save as PDF") in the active language — no server PDF library.
- The subscription price change is audited (before / after), which is its history.
