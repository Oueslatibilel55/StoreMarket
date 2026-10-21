# 015 + 016 — Cash on delivery & order lifecycle — Tasks

## Backend
- [x] Domain: `StoreOrderStatus` (+ Confirmed, Shipped, Delivered, Cancelled, NotDelivered), `NotDeliveredReason`, `OrderActorRole`, `PaymentMethod`, `PaymentStatus`; `Order.ChangeStatus` + `Order.IsAllowed` (who may do what); `StoreOrder` history (`StoreOrderEvent`), payment fields, `ShippedAt` / `DeliveredAt`, `MarkPaid`; events `StoreOrderStatusChangedEvent`, `StoreOrderDeliveredEvent`; error codes `order.*`
- [x] Application: `OrderLifecycleService` (store list / counts / detail / change, client cancel, admin list / detail / force audited) — one transaction with payment, stock return / re-take; `IPaymentMethod` + `CashOnDelivery`; `ConcurrencyConflictException` → `order.changed`; `IProductRepository.ReturnStockAsync`, `IOrderRepository.GetByStoreOrderIdAsync`
- [x] Infrastructure: store order history table, payment columns, `xmin` concurrency token (migration edited: no xmin column, defaults 1 for existing orders), `OrderQueries` store / admin views; `UnitOfWork` maps `DbUpdateConcurrencyException`; migration `OrderLifecycle` created **and applied**
- [x] Api: `OrderLifecycleEndpoints` (`/api/me/store/orders…`, `/api/me/orders/{ref}/store-orders/{id}/cancel`, `/api/admin/orders…`, `/api/admin/store-orders/{id}/status`)
- [x] Unit tests: `OrderLifecycleTests` (happy path + history + events, Delivered final, no skipping shipping, reasons, client rules, admin force, cash on delivery)
- [x] Integration tests: `OrderLifecycleEndpointsTests` (store flow + cash collected, client cancel + stock back, store cancel / not delivered + stock back, privacy, **two deliveries at once**, admin force audited + re-open takes stock)

## Frontend
- [x] `core/orders/store-order-api.ts`, order types (`orderStatusTone`, history, payment) + spec; `shared/orders/order-timeline`
- [x] My store: "Commandes" tab, `/my-store/orders` (status tabs with counts, search) and `/my-store/orders/:id` (next-step bar, confirmations, not-delivered reasons, timeline, commission per line)
- [x] Client order page: status colours, payment, timeline, cancel per store (modal); my orders list in colour; affiliate sales statuses in colour
- [x] Admin: "Commandes" menu entry, `/admin/orders` and `/admin/orders/:reference` (force status modal); audit links `Order` targets
- [x] i18n FR / AR / EN (949 keys in each file)

## Done
- [x] Acceptance criteria verified (tests: 182 unit, 127 integration, 54 frontend; Chrome: Karim confirms → ships → delivers `CMD-2026-000001` (1 967 TND collected), admin list and detail)
- [x] Docs updated: `docs/CONTEXT.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`, `features/README.md`, 015 + 016 specs
