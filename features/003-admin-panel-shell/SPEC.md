# 003 — Admin panel shell & user management

- **Status:** Done
- **Phase:** 1 — Foundation
- **Depends on:** 002
- **Created:** 2026-10-19 · **Done:** 2026-10-19

## Goal

A separate admin area in the Angular app (`/admin`) with layout, navigation, user management and an audit log. Later features plug their admin screens into it.

## Acceptance criteria

- [x] `/admin` routes protected by `roleGuard('Admin')`, lazy-loaded; "Admin" link in the header for admins only
- [x] Layout: side menu (top tabs on mobile) — Users, Audit log, and the future sections (Settings 004, Store categories 005, Product categories 008, Stores / Products 010, Orders 016, Reviews 017, Coins 019, Payouts 021, Billing 025) shown greyed out with a "soon" badge
- [x] Users list: search (name, email, phone digits, affiliate code — case-insensitive), filter by role and status, 20 per page; filters kept in the URL (`?search=&role=&status=&page=`) so the back button and shared links work; totals (users, affiliates, store owners, blocked)
- [x] User detail: profile, roles (grant/remove StoreOwner and Admin; remove Affiliate; Buyer permanent), block with a required reason / unblock — all behind a confirmation dialog; own account read-only
- [x] **Audit log**: every admin action stored with who, what, when, target, reason and **before / after** snapshots, in the **same transaction** as the change; no entry for failed or no-op actions
- [x] User page shows its admin history; `/admin/audit` lists every action, newest first, with the changed fields translated (statuses, roles, dates)
- [x] FR / AR (RTL) / EN
- [x] Migration `AuditLog` created and applied

## API

| Method | Route | Query / body | Response |
|---|---|---|---|
| GET | `/api/admin/users` | `search, role, status, page, pageSize (≤ 100)` | `PagedResult<AdminUserListItem>` |
| GET | `/api/admin/users/counts` | — | `{ total, active, blocked, buyers, affiliates, storeOwners, admins }` |
| GET | `/api/admin/audit` | `targetType, targetId, actorId, action, page, pageSize` | `PagedResult<AuditEntryDto>` |

Plus the user actions from 002 (`GET /users/{id}`, grant / revoke / block / unblock), which now write audit entries.
`PagedResult<T> = { items, page, pageSize, total, totalPages }`. All routes: Admin policy.

## Audit actions

| Code | When | Reason field |
|---|---|---|
| `user.role.grant` / `user.role.revoke` | role added / removed | role name |
| `user.block` / `user.unblock` | access changed | block reason / — |

New admin features add their own codes (`store.disable`, `product.delete`, `settings.update`…) through `IAuditLog.Record(...)`.

## Data

`audit_entries`: id, actor_id, actor_email, action, target_type, target_id, target_label, reason, before (jsonb), after (jsonb), created_at, updated_at — indexes on (target_type, target_id, created_at), (actor_id, created_at), created_at. Append-only.

## Out of scope

- Admin dashboard KPIs → 023
- Editing a user's profile data from the admin panel
