# 027 — Tasks

## Backend
- [x] Domain: `User.RegisterAffiliate`, `User.IsBaseRole`, `RevokeRole` keeps one base role; `RoleRequest` aggregate + `RoleRequestStatus`; error codes `user.baseRoleRequired`, `user.accountTypeInvalid`, `roleRequest.*`
- [x] Application: `RegisterRequest.AccountType / AcceptAffiliateTerms`, `AuthService` creates client or affiliate accounts; `RoleRequestService` (create, list mine, cancel, admin list, approve → grant / activate affiliate, refuse); audit actions `roleRequest.approve / refuse`; `AccountService` + `ActivateAffiliateRequest` removed
- [x] Infrastructure: `RoleRequestConfiguration`, `RoleRequestRepository`, `AdminQueries.ListRoleRequestsAsync`; migration `RoleRequests` created **and applied** to the local Aspire DB
- [x] Api: `RoleRequestEndpoints` (`/api/me/role-requests`, `/api/admin/role-requests`); `POST /api/me/affiliate` removed; `ClaimsPrincipal.GetAdminActor()` shared; error → status mapping
- [x] Unit tests: `RoleRequestTests`, `UserRolesTests` (affiliate sign-up, base role rule)
- [x] Integration tests: `RoleRequestEndpointsTests` (account types, self-activation gone, buyer → affiliate approved + audited, affiliate → store owner, refuse with reason then ask again, rules, cancel, admin-only)

## Frontend
- [x] `RegisterRequest.accountType / acceptAffiliateTerms`; `AuthService.activateAffiliate()` removed
- [x] Sign-up: account type cards + affiliate terms checkbox
- [x] *My account*: affiliate card shows the code only; new "Add a role" card (choose role, message, terms for affiliate, self-affiliation note, my requests, withdraw); refresh once when a request got approved
- [x] Admin: "Role requests" menu entry + screen (status tabs in the URL, approve / refuse modal, user's current roles); user detail can grant / remove Buyer; audit shows `roleRequest.*` and the requested role
- [x] i18n FR / AR / EN (same 181 keys in the three files)
- [x] Unit test: affiliate sign-up in `AuthService` spec

## Done
- [x] Acceptance criteria verified (tests green; manual check in Chrome FR + AR)
- [x] Seed script: affiliates sign up as affiliates, store owners via approved requests, one pending and one refused request
- [x] Docs updated: `docs/CONTEXT.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`, `features/README.md`, 002 / 012 specs

## Notes
- Existing local demo data created before 027 (Sana, Youssef, Amine) still holds Buyer + Affiliate: allowed by the new rules (combination), just not how a new affiliate starts.
