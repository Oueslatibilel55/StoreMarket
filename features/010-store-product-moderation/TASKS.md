# 010 — Store & product moderation (admin) — Tasks

## Backend
- [x] Domain: `Store.Disable / Enable / Delete` (reason, `ModerationReason`, `DisabledAt`, `DeletedAt`; deleted store refuses `Update`), `Product.Disable / Enable / DeleteByAdmin` (`DisabledAt`, `ModerationReason`), `DomainErrors.Moderation`
- [x] Application: `ModerationService` (audited, no-ops skipped, bulk disable), `AdminUserService.RevokeRoleAsync` disables the store on StoreOwner revoke, `ProductService` refuses writes on a deleted store, DTOs in `ModerationDtos.cs`
- [x] Infrastructure: `AdminQueries.Moderation.cs` (store list / detail, product list), `VisibleProducts()` + product page + store card count skip disabled products, repositories (`GetByIdAsync`, `ListByIdsAsync`), migration `StoreProductModeration` (applied locally)
- [x] Api: `Endpoints/ModerationEndpoints.cs`, 409 mapping for the new codes
- [x] Unit tests (store / product moderation rules)
- [x] Integration tests (`ModerationEndpointsTests`: hide / show, reason required, owner view, filters & sort, bulk, soft delete, revoked owner, admin only)

## Frontend
- [x] Routes `/admin/stores`, `/admin/stores/:id`, `/admin/products` (nav entries enabled)
- [x] `ModerationApi` + spec
- [x] Components: store list (URL filters), store detail (config, owner, counts, actions, history), product list (filters, sort, selection + bulk disable), `ModerationDialog`, `StateBadge`, shared `ModerationReason`
- [x] Owner side: banner on My store (disabled / deleted + reason), "disabled by admin" badge in the product list and banner in the editor
- [x] Audit log: store / product targets link to their screen, automatic reason translated
- [x] i18n FR / AR / EN

## Done
- [x] Acceptance criteria verified (suspicious view skipped by decision)
- [x] Docs updated (`features/README.md`, CONTEXT decisions, CLAUDE.md, ARCHITECTURE)
- [x] Manual check in Chrome (FR + AR): admin lists, disable / enable a product, owner banners

## Notes
- Re-enabling a store sets it **Active** (not back to Closed): the owner can close it again.
- 025 (billing) will add `Suspended`; a disable overrides it and enable returns to Active — revisit when 025 lands.
