# 027 — Account types & role requests

- **Status:** Done
- **Phase:** 1 — Foundation (amends 001 / 002 / 003, built before 004)
- **Depends on:** 002, 003
- **Created:** 2026-10-20 · **Done:** 2026-10-20

## Goal

Becoming an affiliate was a self-service button on *My account*, available to every buyer and store owner: anyone could turn it on in one click (that is how the demo store owner Karim ended up an affiliate). The product decision (2026-10-20):

- An **affiliate is a person who works with many stores**. You choose it **when you sign up**: *client account* (Buyer) **or** *affiliate account* (Affiliate + code, **no** Buyer role).
- **Combining roles is allowed** (Buyer + StoreOwner + Affiliate) but only **through a request the admin approves**: a buyer or store owner who wants to be an affiliate asks; an affiliate who wants to open a store asks.
- **No self-affiliation** (rule 9): an affiliate's code never earns on products of a store they own, nor on their own purchases ("I'm a store owner and an affiliate: buying my own products, the affiliate feature doesn't apply").

## User stories

- As a **visitor**, I choose *client account* or *affiliate account* when I sign up.
- As a **buyer or store owner**, I ask to become an affiliate (I accept the affiliate terms in my request).
- As an **affiliate or buyer**, I ask to become a store owner (with a short message about my store).
- As an **affiliate**, I can ask for the Buyer role.
- As a **user**, I see my requests (pending / approved / refused with the reason) and withdraw a pending one.
- As an **admin**, I see the pending requests (oldest first), approve or refuse with a reason; every decision is in the audit log and in the user's admin history.

## Acceptance criteria

- [x] `POST /api/auth/register` takes `accountType` = `Buyer` (default) or `Affiliate`; an affiliate account needs `acceptAffiliateTerms`, gets the Affiliate role + code (`DRF-45`) and **no** Buyer role; `StoreOwner` / `Admin` → `400 user.accountTypeInvalid`
- [x] `POST /api/me/affiliate` (self-activation) is **removed**; `GET /api/me/affiliate` stays
- [x] A user asks for `Buyer`, `Affiliate` or `StoreOwner`; never `Admin` (`roleRequest.roleNotRequestable`); not a role they hold (`409 roleRequest.alreadyHasRole`); one pending request per role (`409 roleRequest.alreadyPending`, unique partial index as backstop); `Affiliate` needs `acceptTerms` (`user.affiliateTermsRequired`); optional message ≤ 500
- [x] User lists their requests and withdraws a pending one; someone else's request → `404`
- [x] Admin lists requests by status (default **Pending**, oldest first) / role, paged, with the user's current roles
- [x] Approve → role granted; an affiliate gets their code (the same one back if they had one), with the terms date of the request; refuse needs a reason (shown to the user); a decided request can't be decided again (`409 roleRequest.notPending`); an admin can't decide on their own request; blocked user → `403 user.blocked`
- [x] Decisions are audited on the **user** (`roleRequest.approve` with before/after roles, `roleRequest.refuse` with the reason) → visible in the audit log and the user's admin history
- [x] An account always keeps one **base role** (Buyer or Affiliate): revoking the last one → `400 user.baseRoleRequired` (replaces `user.buyerRoleRequired`); an admin can now grant / remove Buyer like StoreOwner
- [x] The approved role reaches the client at the next token refresh — *My account* refreshes once by itself when it sees an approved request whose role isn't in the token yet
- [x] Sign-up: two account-type cards + affiliate terms checkbox; *My account*: affiliate code card (affiliates only) + "Add a role" card with request list; Admin → **Role requests** screen with approve / refuse modal (FR / AR / EN, RTL-safe)

## Backend

### Domain
- `User.Register` (Buyer) / `User.RegisterAffiliate` (Affiliate + code); `User.IsBaseRole`; `RevokeRole` keeps one base role.
- `RoleRequest` aggregate: `Create` (requestable role, terms for affiliate, message length), `Approve`, `Refuse(reason)`, `Cancel`; only from `Pending`.
- `RoleRequestStatus` enum: `Pending=1, Approved=2, Refused=3, Cancelled=4` (stored as int, append only).

### API contract

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| POST | `/api/auth/register` | — | `{ email, password, fullName, phone, accountType?, acceptAffiliateTerms? }` | `200 AuthResponse` · `400 user.accountTypeInvalid / user.affiliateTermsRequired` |
| GET | `/api/me/role-requests` | signed in | — | `200 RoleRequestDto[]` (newest first) |
| POST | `/api/me/role-requests` | signed in | `{ role, message?, acceptTerms? }` | `201 RoleRequestDto` · `400` · `409 alreadyHasRole / alreadyPending` |
| POST | `/api/me/role-requests/{id}/cancel` | signed in | — | `200` · `404` · `409 notPending` |
| GET | `/api/admin/role-requests?status=&role=&page=&pageSize=` | Admin | — | `200 PagedResult<AdminRoleRequestItem>` |
| POST | `/api/admin/role-requests/{id}/approve` | Admin | — | `200 RoleRequestDto` · `404` · `409 notPending / cannotChangeOwnAccount` · `403 user.blocked` |
| POST | `/api/admin/role-requests/{id}/refuse` | Admin | `{ reason }` | `200 RoleRequestDto` · `400 reasonRequired` · `409` |

### Persistence
- Migration `RoleRequests`: table `role_requests` (FK `user_id`, `decided_by_id` → `users`, restrict), indexes `(status, created_at)`, `(user_id, created_at)`, unique `(user_id, role) WHERE status = 1`.

## Frontend

- **Routes:** `/register` (account type), `/account` (role requests card), `/admin/role-requests` (`?status=Approved|Refused|Cancelled|all`, none = Pending)
- **Components:** `features/account/role-requests`, `features/account/account-api.ts`, `features/admin/role-requests/role-request-list`
- **States:** loading / empty / error / success; modal confirmation; refuse reason required

## Out of scope

- Notifying the user of a decision (022) — they see it on *My account*.
- Opening the store itself (007): StoreOwner only unlocks it.
- Checking self-affiliation on orders (013 / 014 / 024) — the rule is recorded, enforced at checkout.

## Open questions

- [ ] Should the admin see a pending-requests counter in the menu? (nice to have, later)
