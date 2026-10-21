# 001 — Authentication & accounts — Tasks

## Backend
- [x] Domain: `User` (Register factory, email/phone normalization, password rule, roles), `RefreshToken` (rotation family, revoke), `Role`, `UserStatus`, `DomainErrors`
- [x] Application: `AuthService` (register, login, refresh with reuse detection, logout, me), DTOs, `AuthErrors`, interfaces (`IUserRepository`, `IRefreshTokenRepository`, `IPasswordHasher`, `ITokenService`)
- [x] Infrastructure: EF configurations (snake_case via EFCore.NamingConventions), repositories, `BCryptPasswordHasher`, `JwtTokenService` + `JwtOptions` (validated on start), design-time DbContext factory
- [x] Migration `InitialAuth` — created and applied to the Aspire database
- [x] Api: `AuthEndpoints`, refresh cookie helper, JWT bearer setup, per-IP rate limit, Result → ProblemDetails mapping
- [x] Unit tests: `UserTests`, `RefreshTokenTests`
- [x] Integration tests (Testcontainers PostgreSQL): register + cookie flags, duplicate email, invalid input, wrong password, me with/without token, refresh rotation + reuse detection, logout

## Frontend
- [x] `AuthService` (signals), `authInterceptor` (bearer + single shared refresh on 401), `authGuard` / `guestGuard`, session restore at startup
- [x] Pages: `/login`, `/register`, `/account`; header with user menu + sign-out + language switcher
- [x] Error codes → `errors.*` translations; FR / AR / EN keys; `fr-TN` / `ar-TN` locale data for pipes
- [x] Unit tests: auth service, interceptor, safe return URL, locale data, app shell

## Done
- [x] Acceptance criteria verified (automated tests + manual check in Chrome, FR and AR/RTL)
- [x] Docs updated (`features/README.md`, `CLAUDE.md`, `README.md`, ARCHITECTURE, CONVENTIONS, ADR 0004)

## Notes
- Dev JWT key lives in user-secrets of `WebApp.Api` (`Jwt:Secret`). Integration tests set their own.
- Found while testing: Angular needs `registerLocaleData` for `fr-TN` / `ar-TN`, otherwise date pipes throw — now covered by a test.
