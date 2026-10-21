# 010 — Store & product moderation (admin)

- **Status:** Done
- **Phase:** 2 — Stores & catalog
- **Depends on:** 007, 009
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

There is **no approval step**: stores and products go live immediately. The admin reviews them afterwards and **disables or deletes fake ones** (e.g. a perfume at 999 999 TND).

## User stories

- As an admin, I want to see every store with its configuration and products so that I can spot fake stores.
- As an admin, I want to quickly find suspicious products (absurd prices, no images, copied names) so that I can disable or delete them.

## Decisions (2026-10-20)

- **Revoking StoreOwner auto-disables the store** (reason `store.ownerRoleRevoked`, translated on screen, audited as `store.disable`). Giving the role back does **not** re-enable it: the admin does, and enabling is refused while the owner lacks the role (`store.ownerNotStoreOwner`).
- **Disabled product:** the owner still sees it (badge + reason) and can edit it, but it stays out of the shop until an admin enables it — product status (Draft / Published / Archived) stays the owner's, the disable is a separate flag (`DisabledAt`).
- **Suspicious-products view (median × 10, 0 / ≥ 100 000 TND, no photo): skipped** by the product owner. The product list sorts by price, filters by price range and flags "no photo" instead.
- **Delete = soft delete, final.** Deleted store: status Disabled + `DeletedAt`, listed apart (`deleted=true`), the owner sees the notice and can no longer edit the store or its products (`store.deleted`). Deleted product: `DeletedAt` + reason, as in 009.

## Acceptance criteria

- [x] Admin store list: filters (status, category, city, created date), search (name, URL name, owner name / email); store detail shows config, owner, product counts, link to its products (stats link waits for 023)
- [x] Admin product list across all stores: filters (store, category incl. sub-categories, price range, state), sort by price / newest, search (name, URL name, SKU)
- [ ] ~~Suspicious products view~~ — skipped (see decisions)
- [x] Actions on a store: **disable** / re-enable / **delete**, with a reason (`Store.Status = Disabled`; the owner can't lift it — 007)
- [x] Revoking the owner's StoreOwner role disables the store (decided above)
- [x] Actions on a product: **disable** / re-enable / **delete**, with a reason
- [x] Disabled store → store and all its products hidden from catalog and search; owner sees the reason and cannot reopen
- [x] Shop filter: `CatalogQueries.VisibleProducts()` excludes disabled products; the product page now goes through it too; store queries already only show Active / Closed (a disabled or deleted store is neither); the store card count skips disabled products
- [x] Delete is a **soft delete** (hidden everywhere, kept for order and coin history)
- [x] Every admin action is audited (admin, date, reason, before/after snapshot: status, deleted, disabled, reason)
- [x] Bulk actions (select several products → disable, ≤ 100, one audit line each, one transaction)

## API

| Method | Path | |
|---|---|---|
| GET | `/api/admin/stores?search=&status=&category=&city=&deleted=&createdFrom=&createdTo=&page=` | paged list |
| GET | `/api/admin/stores/{id}` | detail (config, owner, counts, moderation) |
| POST | `/api/admin/stores/{id}/disable` · `/enable` · `/delete` | body `{ reason }` (not for enable) |
| GET | `/api/admin/products?search=&storeId=&category=&minPrice=&maxPrice=&state=Live\|Draft\|Archived\|Disabled\|Deleted&sort=&page=` | paged list |
| POST | `/api/admin/products/{id}/disable` · `/enable` · `/delete` | 204 |
| POST | `/api/admin/products/disable` | `{ ids, reason }` → `{ changed }` |

Error codes: `moderation.reasonRequired`, `moderation.selectionInvalid`, `store.deleted` (409), `store.ownerNotStoreOwner` (409), `product.deleted` (409).
