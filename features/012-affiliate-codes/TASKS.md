# 012 — Affiliate codes & store partnerships — Tasks

## Backend
- [x] Domain: `Partnership` aggregate (`Request`, `Accept`, `Refuse`, `Cancel`, `End`) + `PartnershipStatus`; `User.AffiliateCodeDisabledAt / Reason`, `DisableAffiliateCode`, `EnableAffiliateCode`, `CanEarnAsAffiliate`; error codes `partnership.*`, `user.affiliateCodeReasonRequired`, `user.notAffiliate`
- [x] Application: `PartnershipService` (affiliate: request / list / cancel; owner: list / accept / refuse / end; admin: list / end audited), `AffiliateCodeService.ValidateAsync` + `ShortName`; `AdminUserService.Disable / EnableAffiliateCodeAsync`; audit actions `partnership.end`, `user.affiliateCode.disable / enable`; `PartnershipErrors`, `AffiliateCodeErrors`
- [x] Infrastructure: `PartnershipConfiguration` (unique open partnership per affiliate + store, filtered index), `PartnershipRepository`, `PartnershipQueries`, `AdminQueries.Partnerships`, `CatalogQueries.GetVisibleProductStoreAsync / IsStoreVisibleAsync` (through `VisibleProducts()`), `UserRepository.GetByAffiliateCodeAsync`; migration `Partnerships` created **and applied** to the local Aspire DB
- [x] Api: `PartnershipEndpoints` (`/api/me/partnerships`, `/api/me/store/partnerships`, `/api/admin/partnerships`, `POST /api/affiliate-codes/check`), `/api/admin/users/{id}/affiliate-code/disable|enable`, rate limit policy `affiliate-code`, error → status mapping
- [x] Unit tests: `PartnershipTests` (lifecycle, reasons, short name), `UserRolesTests` (code disable / enable, `CanEarnAsAffiliate`)
- [x] Integration tests: `PartnershipEndpointsTests` (request → accept → code valid → end → refused → ask again; refuse with reason; withdraw; rules; no self-affiliation; only the owner decides; every code-check error; code deactivation audited; admin list / search / end audited)

## Frontend
- [x] `core/partnerships/partnership-api.ts` (models, API, `partnershipTone`, `shareLink`) + spec
- [x] `shared/partnerships/partnership-cta` on store pages and product pages: ask (with message), waiting (withdraw), partner (share link + copy), last refusal / end reason
- [x] `/affiliate` "My stores" (affiliate code card, partner stores with share links, waiting, history) + header link for affiliates
- [x] `/my-store/affiliates` tab "My affiliates": counters, requests (accept / refuse), partners (end), history, confirmation modal with optional reason
- [x] Admin: "Partnerships" menu entry + screen (search, status filter in the URL, end with required reason); user detail: affiliate code card (deactivate / re-enable, link to their partnerships)
- [x] i18n FR / AR / EN (same 754 keys in the three files), audit action labels, error messages

## Done
- [x] Acceptance criteria verified (tests green: 150 unit, 107 integration, 49 frontend; manual check in Chrome FR + AR)
- [x] Seed script: Sana partner of Parfumerie Karim and Nour Mode, waiting on Slim Électronique; Youssef waiting / ended; Walid refused
- [x] Docs updated: `docs/CONTEXT.md`, `CLAUDE.md`, `docs/ARCHITECTURE.md`, `features/README.md`, this spec

## Notes
- Notifications (new request, decision) come with 022.
- The `ref` query parameter of share links is read by the cart (013).
- 2026-10-21: admin screens show who owns each store — owner (name, e-mail, link) on every partnership row, the owned store on the user list (search by store name) and a "Boutique" row on the user page.
