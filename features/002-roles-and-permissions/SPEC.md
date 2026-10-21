# 002 — Roles & permissions

- **Status:** Done
- **Phase:** 1 — Foundation
- **Depends on:** 001
- **Created:** 2026-10-19 · **Done:** 2026-10-19

> **Superseded in part by [027](../027-account-types-role-requests/SPEC.md) (2026-10-20):** the self-service affiliate activation (`POST /api/me/affiliate`, "Become an affiliate" card) was removed. Affiliates now choose it at sign-up, or an admin approves a role request. `user.buyerRoleRequired` became `user.baseRoleRequired`. The rest of this spec still holds.

## Goal

One account can hold several roles: **Buyer** (default), **Affiliate**, **StoreOwner**, **Admin**. Endpoints are protected by policies.

## Decisions

- The **affiliate code is generated here**, when the user activates the affiliate role (not in 012). 012 adds store partnerships and code validation on products.
- **First admin** = any email listed in `BootstrapAdmin:Emails` (user-secrets in dev, `BootstrapAdmin__Emails__0` env var in prod). That user becomes Admin when signing up or signing in. Public sign-up can never ask for Admin.
- **StoreOwner** is normally granted by creating a store (007); admin can also grant it.
- Admin **API** for roles and blocking is done here; the **admin panel screens** (user list, search, buttons, audit log) are feature 003.

## User stories

- As a buyer, I want to activate the affiliate role so that I can start earning coins.
- As an admin, I want to grant/revoke roles and block a user.

## Acceptance criteria

- [x] Roles stored per user (`integer[]`), included as `role` claims in the JWT
- [x] Authorization policies `Admin`, `StoreOwner`, `Affiliate` (+ any signed-in user)
- [x] `POST /api/me/affiliate { acceptTerms: true }` → Affiliate role + unique code like `DRF-45` (3 letters without I/O + 2+ digits); idempotent; re-activation after a revoke gives the **same** code back
- [x] Frontend refreshes the token after activation so the new claim is active immediately
- [x] Admin: grant / revoke role (Buyer can't be revoked; Affiliate only through activation), block (reason required, signs the user out everywhere) / unblock
- [x] Blocked users cannot sign in (`auth.accountBlocked`) and their refresh fails; an Admin can't be blocked; an admin can't change their own account (no lock-out)
- [x] First admin from configuration, never from public sign-up
- [x] Angular: "Become an affiliate" card with terms checkbox → code shown as `#DRF-45` with copy button (FR / AR / EN); `roleGuard(role)` for future routes
- [x] Migration `AffiliateCodeAndBlocking` created and applied

## API

| Method | Route | Policy | Body | Response / errors |
|---|---|---|---|---|
| POST | `/api/me/affiliate` | signed in | `{ acceptTerms }` | `200 UserDto` (with `affiliateCode`) · `400 user.affiliateTermsRequired` · `403 user.blocked` |
| GET | `/api/me/affiliate` | Affiliate | — | `200 { code }` |
| GET | `/api/admin/users/{id}` | Admin | — | `200 AdminUserDto` · `404 auth.userNotFound` |
| POST | `/api/admin/users/{id}/roles/{role}` | Admin | — | `200 AdminUserDto` · `400 user.useAffiliateActivation` |
| DELETE | `/api/admin/users/{id}/roles/{role}` | Admin | — | `200 AdminUserDto` · `400 user.buyerRoleRequired` |
| POST | `/api/admin/users/{id}/block` | Admin | `{ reason }` | `200 AdminUserDto` · `400 user.blockReasonRequired` · `409 user.cannotBlockAdmin` |
| POST | `/api/admin/users/{id}/unblock` | Admin | — | `200 AdminUserDto` |

All admin routes: `409 admin.cannotChangeOwnAccount` when the target is the calling admin.

## Data

`users` + `affiliate_code` (unique, nullable), `affiliate_terms_accepted_at`, `blocked_at`, `blocked_reason`.

## Notes

- A blocked user keeps API access until their current access token expires (≤ 15 min) — see ADR 0004.
- Domain events `AffiliateActivatedEvent`, `UserBlockedEvent` are raised (no handlers yet; notifications come in 022).
