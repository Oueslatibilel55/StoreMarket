# 015 — Cash on delivery

- **Status:** Done (built with 016)
- **Phase:** 3 — Orders & affiliate tracking
- **Depends on:** 014
- **Created:** 2026-10-19 · **Done:** 2026-10-21

## Goal

v1 payment is **cash on delivery only**: the client pays when the order arrives.

## Acceptance criteria

- [x] Each StoreOrder has `AmountToCollect` (lines + delivery fee, TND, frozen at checkout) and payment status `Unpaid` → `Collected` (`PaidAt`), payment method `CashOnDelivery`
- [x] Marking a StoreOrder **Delivered** (016) sets payment to `Collected` — the store confirms the cash was received ("Livrée et encaissée", with the amount in the confirmation)
- [x] Refused at delivery → StoreOrder `NotDelivered` (016), nothing collected, no coins, stock put back
- [x] Payment kept behind an `IPaymentMethod` abstraction (`CashOnDelivery` implementation, resolved by the store order's `PaymentMethod`) so an online gateway can be added later without touching orders
