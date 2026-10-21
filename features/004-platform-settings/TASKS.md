# 004 — Platform settings (dynamic config) — Tasks

## Backend
- [x] Domain: `SettingDefinition` (type, default, limits, `Validate` → canonical value), `PlatformSettingCatalog` + `SettingKeys`, `PlatformSetting` entity, `setting.*` error codes; `AffiliateCode` accepts 2–5 letters / 2–6 digits
- [x] Application: `IPlatformSettings` (cached read), `IPlatformSettingRepository`, `PlatformSettingsService` (list, update + min ≤ max check, audit `setting.update`, no-op on same value); `AffiliateCodeGenerator` reads letters / digits from the settings
- [x] Infrastructure: `PlatformSettingConfiguration`, `PlatformSettingRepository`, `PlatformSettingsStore` (IMemoryCache), `AdminQueries.ListStoredSettingsAsync`; migration `PlatformSettings` created **and applied** to the local Aspire DB
- [x] Api: `SettingsEndpoints` (`GET /api/settings`, `GET /api/admin/settings`, `PUT /api/admin/settings/{key}`)
- [x] Unit tests: `PlatformSettingTests`, `AffiliateCodeTests` (bounds, configured shape), `AffiliateCodeGeneratorTests` (checks the DB for every candidate, retries when taken, widens the number)
- [x] Integration tests: `SettingsEndpointsTests` (defaults, change served at once + audited, same value not audited, validation errors, unknown key, code shape for new codes / old ones still valid, admin only)

## Frontend
- [x] `/admin/settings` lazy route, menu entry enabled
- [x] `AdminApi.settings()` / `updateSetting()`
- [x] Settings screen: groups, units, default + range + last change, inline validation, confirm modal (old → new, reason)
- [x] Audit log: `setting.update` entries link to the settings screen (user links only for user targets)
- [x] i18n FR / AR / EN (236 keys, same in the three files)
- [x] Unit test: `setting-input.spec.ts`

## Done
- [x] Acceptance criteria verified (backend 128 tests, frontend 30 tests; manual check in Chrome FR + AR)
- [x] Docs updated (`features/README.md`, `docs/CONTEXT.md`, `docs/ARCHITECTURE.md`, `CLAUDE.md`)

## Notes
- Integration tests share one database: a test that changes a setting puts the default back, and only touches values no other test depends on.
- `TestApi.SignInAdmin` now survives parallel tests racing to register the admin (it used to get a 500 from the unique email index).
