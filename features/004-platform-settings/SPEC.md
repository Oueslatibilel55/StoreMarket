# 004 — Platform settings (dynamic config)

- **Status:** Done
- **Phase:** 1 — Foundation
- **Depends on:** 003
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

All tunable business values live in the database and are edited by the admin — no redeploy to change them.

## Decisions (2026-10-20)

- Default store subscription: **30 TND / month** (each store can still get its own price in 025).
- Commission invoice due: the **10th** of the next month (closes the open question in CONTEXT).
- Affiliate code format **is a setting** (letters count, starting digits) — it only shapes **new** codes; codes already given stay valid. Every generated code is **checked unique in the database before it is saved** (unique index as a backstop).
- Currency stays **TND**, shown but not editable in v1.

## Settings

| Key | Type | Default | Allowed | Used by |
|---|---|---|---|---|
| `currency` | text | TND | read-only | everywhere |
| `coin.valueInTnd` | number | 1 | 0.001 – 1000 | 019 coin calculation, 021 payouts |
| `payout.minimumCoins` | number | **80** | 0 – 1 000 000 | 021 |
| `affiliatePercent.min` / `.max` | whole | 0 / 50 | 0 – 100, min ≤ max | 007, 009 (limits for store owners) |
| `affiliateCode.letters` | whole | 3 | 2 – 5 | 002 / 027 code generation |
| `affiliateCode.minDigits` | whole | 2 | 2 – 6 | 002 / 027 code generation (grows on collisions) |
| `subscription.defaultMonthlyPriceTnd` | number | **30** | 0 – 100 000 | 025 |
| `commissionInvoice.dueDayOfMonth` | whole | **10** | 1 – 28 | 025 |
| `billing.graceDays` | whole | **3** | 0 – 60 | 025 — days after the due date before the store is suspended |
| `ranking.minReviewsPrior` | whole | 10 | 0 – 1000 | 018 |

Numbers keep at most 3 decimals (millimes). Values are stored as invariant strings (`0.5`, `80`).

## Acceptance criteria

- [x] Typed settings: a catalog in the domain (type, default, limits, unit, group); a row in `platform_settings` only once the admin changes a value — no row = default
- [x] Cached (IMemoryCache) for the whole API and invalidated on change (10-min expiry as a safety net for several instances)
- [x] Admin screen to view / edit settings with validation (same rules client- and server-side), grouped, with default, allowed range and last change
- [x] Every change is audited (`setting.update`: old value, new value, admin, date, optional reason); saving the same value is a no-op (not audited)
- [x] `affiliatePercent.min` can never go above `.max` (`setting.percentMinAboveMax`)
- [x] Affiliate code shape follows the settings for new codes; old codes keep working; every candidate checked unique before use
- [x] Changing a value never rewrites history: consumers copy the value they use onto their own record (order line %, ledger rate, invoice) — rule documented in the catalog and on the screen

## API contract

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/settings` | — | — | `200 { key: value }` (every setting) |
| GET | `/api/admin/settings` | Admin | — | `200 AdminSettingDto[]` |
| PUT | `/api/admin/settings/{key}` | Admin | `{ value, reason? }` | `200 AdminSettingDto` · `400 setting.*` · `404 setting.unknown` |

## Persistence

Migration `PlatformSettings`: table `platform_settings` (`key` unique, `value`, `updated_by_id` → users).

## Frontend

- **Route:** `/admin/settings` (menu entry enabled)
- **Components:** `features/admin/settings` (+ `setting-input.ts` validation shared with its spec)
- **States:** loading / error / per-row invalid / saved; confirm modal with old → new and optional reason
