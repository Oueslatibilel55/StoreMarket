# 002 — Roles & permissions — Tasks

## Backend
- [x] Domain: `User.ActivateAffiliate / GrantRole / RevokeRole / Block / Unblock`, `AffiliateCode` (normalize / validate), domain events, new error codes
- [x] Application: `AccountService` (activate affiliate), `AdminUserService` (get, grant, revoke, block + revoke sessions, unblock), `AffiliateCodeGenerator`, `BootstrapAdminOptions` (promotion on sign-up / sign-in)
- [x] Infrastructure: unique index on `affiliate_code`, `GetActiveForUserAsync` for refresh tokens
- [x] Migration `AffiliateCodeAndBlocking` — created and applied
- [x] Api: `AuthPolicies` (Admin, StoreOwner, Affiliate), `AccountEndpoints`, `AdminUserEndpoints`, status mapping for new codes
- [x] Unit tests: `UserRolesTests`, `AffiliateCodeTests`
- [x] Integration tests: activation + role in refreshed token, terms required, same code twice, bootstrap admin, non-admin 403, grant/revoke, block → signed out + can't sign in → unblock, admin can't change own account

## Frontend
- [x] `affiliateCode` on `User`; `AuthService.activateAffiliate()` (then refresh)
- [x] `roleGuard(role)`
- [x] "Become an affiliate" card on My account → code `#XXX-00` + copy button; FR / AR / EN
- [x] Unit tests: activation flow, role guard

## Done
- [x] Acceptance criteria verified (59 backend + 21 frontend tests, manual check in Chrome)
- [x] Docs updated

## Notes
- To make yourself admin locally: `dotnet user-secrets set "BootstrapAdmin:Emails:0" "<your email>" --project src/WebApp.Api`, restart the API, sign in.
