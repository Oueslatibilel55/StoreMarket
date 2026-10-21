# 003 — Admin panel shell & user management — Tasks

## Backend
- [x] Domain: `AuditEntry` (append-only)
- [x] Application: `IAuditLog` + `AuditActions` / `AuditTargets`, `IAdminQueries` (read side), `PagedResult` / `Paging`, admin DTOs, `AdminUserService` now takes an `AdminActor` and audits every real change (skips no-ops and failures), `AuditService`
- [x] Infrastructure: `AuditLog` (JSON snapshots, same unit of work), `AdminQueries` (ILIKE search with escaping, role `= ANY`, counts), `AuditEntryConfiguration` (jsonb + indexes)
- [x] Migration `AuditLog` — created and applied; Migrations folder marked `generated_code` in `.editorconfig`
- [x] Api: `GET /api/admin/users`, `/users/counts`, `/audit`; actor (id + email) taken from the token
- [x] Integration tests: search (case-insensitive, phone digits), role / status filters, paging, audit entry with reason + before/after, no-op and failed actions not audited, counts, buyers forbidden

## Frontend
- [x] Admin routes (`roleGuard('Admin')`), `AdminLayout` with side menu + "soon" sections, header link
- [x] `AdminApi`, models; shared `Pagination`, `Modal`, `RoleBadge`
- [x] Users list (URL-driven filters, debounced search), user detail (roles, block / unblock dialogs, history), audit log page
- [x] `changedFields` / `displayValue` (translated statuses & roles, formatted dates)
- [x] FR / AR / EN keys
- [x] Unit tests: admin API params, audit diff and display

## Done
- [x] Acceptance criteria verified (69 backend + 27 frontend tests; manual check in Chrome: list, filters, block dialog, history, Arabic RTL)
- [x] Docs updated

## Notes
- Fixed during the browser check: missing query params arrive as `undefined` through router input binding (search box showed "undefined"); arrows in the change list now flip in RTL.
