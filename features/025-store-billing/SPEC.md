# 025 — Store billing (subscription + affiliate commissions)

- **Status:** Draft
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

- [ ] Admin: set / change the subscription price of each store (history kept); default price for new stores
- [ ] Admin: CRUD offers (rules above); best applicable offer suggested when recording a payment
- [ ] Admin records a **subscription payment**: months, offer applied, amount, method (cash / bank transfer…), reference → paid-until date extended
- [ ] Monthly **commission invoice** generated per store; admin records its payment (full or partial)
- [ ] **Alert for the store owner** while any commission invoice is unpaid: banner on every store-owner page + notification (022) — amount due, due date, link to the invoice lines
- [ ] Store owner **declares a payment** ("I paid": amount, cash / bank transfer, reference, optional receipt photo) → status `PaymentDeclared`, alert switches to "waiting for approval"
- [ ] Admin queue of declared payments: **approve** → invoice `Paid` (or partially paid), **alert disappears**; **reject** with reason → alert back with the reason
- [ ] Admin can also record a payment directly (e.g. cash received in person) without a declaration
- [ ] Reminder notifications: when the invoice is issued, a few days before the due date, on the due date, on suspension
- [ ] **3-day grace period** (`Billing.GraceDays`, default 3): after the due date of the subscription or of a commission invoice, the store owner sees a **red alert "Your store will be closed in 3 / 2 / 1 day(s) — pay now"** on every page + a notification each day
- [ ] Daily job → store **Suspended** when the subscription **paid-until date + 3 days** has passed, **or** a commission invoice is unpaid **3 days after its due date** (a payment declared and waiting for approval stops the countdown)
- [ ] **Suspended store**: hidden from catalog and search, **no new orders**; products and data kept; owner can log in, sees a "payment due" banner, can **keep processing orders placed before the suspension**
- [ ] Payment recorded → store back to `Active` automatically when nothing is overdue
- [ ] Suspension for non-payment is **separate** from admin disable (010)
- [ ] Store owner: sees price, active offers, paid-until date, subscription payments, commission invoices with every line
- [ ] Admin: overdue list, revenue per month split **subscriptions vs commissions collected**, offers usage (also in 023)
- [ ] Every price change, offer, payment and status change audited; reminders before due date and on suspension (022)
- [ ] Invoice / receipt export (PDF) in FR / AR / EN

## Open questions

- Commission invoice due date (e.g. 10th of the next month)?
