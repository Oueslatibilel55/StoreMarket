# 001 — Authentication & accounts

- **Status:** Done
- **Phase:** 1 — Foundation
- **Depends on:** 000
- **Created:** 2026-10-19 · **Done:** 2026-10-19
- **Decision:** [ADR 0004](../../docs/decisions/0004-authentication.md)

## Goal

People can sign up, sign in and stay signed in securely. Base for every other feature.
**Simple sign-up / sign-in for now** — email verification and password reset come later (026).

## User stories

- As a visitor, I want to sign up with email, password, full name and phone so that I can buy, sell or become an affiliate.
- As a user, I want to sign in and stay signed in (also after reloading the page).
- As a user, I want to sign out.

## Acceptance criteria

- [x] Sign up: email (unique, case-insensitive), password (≥ 8 chars, letters + digits), full name, Tunisian phone (8 digits, `+216` optional) → account with role **Buyer**, signed in immediately
- [x] Password hashed with BCrypt; never returned or logged
- [x] Sign in returns a 15-min JWT access token + sets a 14-day **httpOnly refresh cookie**
- [x] Refresh rotates the cookie; reusing an old refresh token revokes the whole session family
- [x] Sign out revokes the session and clears the cookie
- [x] `GET /api/auth/me` returns the current user with roles (requires a token)
- [x] Same error for unknown email and wrong password (`auth.invalidCredentials`)
- [x] Rate limiting on register / login / refresh (10 / min / IP)
- [x] Errors returned as ProblemDetails with a translatable `code`
- [x] Angular: sign-in and sign-up pages (FR / AR / EN, RTL), header with user menu and sign-out, "My account" page, auth interceptor (bearer + silent refresh on 401), `authGuard` / `guestGuard`, session restored on page reload
- [x] Migration `InitialAuth` (tables `users`, `refresh_tokens`) created and applied

## API contract

| Method | Route | Request | Response | Errors (`code`) |
|---|---|---|---|---|
| POST | `/api/auth/register` | `{ email, password, fullName, phone }` | `200 AuthResponse` + refresh cookie | `400 user.emailInvalid / user.passwordTooWeak / user.fullNameRequired / user.phoneInvalid`, `409 auth.emailTaken`, `429` |
| POST | `/api/auth/login` | `{ email, password }` | `200 AuthResponse` + refresh cookie | `401 auth.invalidCredentials`, `403 auth.accountBlocked`, `429` |
| POST | `/api/auth/refresh` | refresh cookie | `200 AuthResponse` + new cookie | `401 auth.invalidRefreshToken`, `403 auth.accountBlocked` |
| POST | `/api/auth/logout` | refresh cookie | `204` (cookie cleared) | — |
| GET | `/api/auth/me` | `Authorization: Bearer` | `200 UserDto` | `401` |

`AuthResponse = { accessToken, accessTokenExpiresAt, user: UserDto }` ·
`UserDto = { id, email, fullName, phone, roles: ["Buyer" | "Affiliate" | "StoreOwner" | "Admin"], createdAt }`

## Data

| Table | Columns |
|---|---|
| `users` | id, email (unique), password_hash, full_name, phone (indexed, `+216…`), status (1 Active, 2 Blocked), roles `integer[]`, last_login_at, created_at, updated_at |
| `refresh_tokens` | id, user_id → users (cascade), family_id, token_hash (unique, SHA-256), expires_at, revoked_at, replaced_by_token_id, created_at, updated_at |

## Out of scope (later)

- Email verification, password reset → **026**
- Grant/revoke roles, block users → 002 / 003
- "Log out from all devices", social login
