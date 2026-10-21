# ADR 0004 — Authentication: JWT + rotating refresh cookie

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

We need simple sign-up / sign-in now (email verification and password reset come later, feature 026), the same approach as RichLife (JWT + BCrypt + refresh tokens), and a session that survives page reloads in the Angular SPA without exposing long-lived secrets to JavaScript.

## Decision

- **Own accounts**: email + password + full name + Tunisian phone. Email lower-cased and unique; phone normalized to `+216XXXXXXXX`. Password ≥ 8 chars with letters and digits, hashed with **BCrypt** (enhanced, work factor 12).
- **Access token**: JWT HS256, **15 min**, claims `sub`, `email`, `name`, `role` (one per role). Kept **in memory** by the frontend only (never `localStorage`).
- **Refresh token**: 32 random bytes, valid **14 days**, sent as an **httpOnly, SameSite=Strict cookie** (`webapp_refresh`, path `/api/auth`, `Secure` outside Development). Stored server-side as a **SHA-256 hash** only.
- **Rotation + reuse detection**: every refresh revokes the token and issues a new one in the same *family*. Presenting an already-rotated token revokes the whole family (stolen-token signal). Logout revokes the family.
- **Errors** are stable codes (`auth.invalidCredentials`, `auth.emailTaken`…) in ProblemDetails, translated by the client. Login never says whether the email exists.
- **Rate limiting**: 10 requests / minute / IP on register, login and refresh.
- Signing key `Jwt:Secret` (≥ 32 chars) comes from user-secrets in development and the `Jwt__Secret` environment variable elsewhere; the API refuses to start without it.

## Consequences

- XSS can't steal a long-lived credential; CSRF on the refresh endpoint is blocked by SameSite=Strict + path scoping.
- The SPA and API must be same-site (dev: Angular proxy; prod: same domain or reverse proxy).
- A blocked user keeps API access until their current access token expires (≤ 15 min).
- Email verification, password reset and "log out everywhere" are left for later features.
