# 007 — Store creation & configuration

- **Status:** Done
- **Phase:** 2 — Stores & catalog
- **Depends on:** 002, 005, 006
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

A business owner creates their store and configures it. The store is **live immediately** (no admin approval).

## Decisions (2026-10-20)

- Only a **StoreOwner** creates a store (the role comes from an admin-approved request, 027); **one store per owner** in v1 (unique index).
- Fields: name (2–80), URL name (made from the name, unique, editable), **one category** (active; a category hidden later stays on its stores), description (≤ 2000), phone (Tunisian), **city = one of the 24 governorates**, address, **logo / banner** (the owner's own uploads, 006), Facebook / Instagram / website links (checked to point to the right site).
- Selling: **default affiliate %** within the settings `affiliatePercent.min` / `.max` (a % the admin's new limits no longer allow can be kept, not changed to another out-of-range value), **delivery fee** (0–1000 TND, 3 decimals), **delivery zones** (≥ 1 governorate), **delivery delay** min–max days (0–30).
- **Subscription price** copied from the setting `subscription.defaultMonthlyPriceTnd` into the store at creation — the admin will change it per store (025); a later change of the default doesn't touch existing stores.
- Status: `Active` on creation; the owner **closes / reopens** (`Closed` stays visible as closed); `Disabled` (admin, 010) and `Suspended` (unpaid, 025) can't be lifted by the owner.
- Owner actions are not admin actions → not in the audit log.

## Acceptance criteria

- [x] Only a `StoreOwner` can create a store (creating a store never grants the role); one owner → one store (`409 store.alreadyExists`)
- [x] Store picks one **active** store category; a category used by stores can't be deleted (`409 storeCategory.inUse`) — the admin hides it instead
- [x] Store has a unique public slug: `/stores/{slug}` (`409 store.slugTaken`); public `GET /api/stores/{slug}` shows active and closed stores only
- [x] Store is `Active` on creation; owner closes / reopens; `Disabled` / `Suspended` reserved for 010 / 025
- [x] **Default affiliate %** within the settings limits (`400 store.percentOutOfRange`); orders will copy it (014) so changing it only affects future orders
- [x] Delivery settings: delivery fee (TND), governorates served, delivery delay
- [x] Owner page "My store" (`/my-store`, header link for store owners): create form the first time, then settings form + open / close
- [x] Logo / banner use the image upload from 006; only the owner's own images of the right purpose (`image.notYours`, `image.wrongPurpose`); an image used by a store can't be deleted
- [x] All prices in TND

## API contract

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| GET | `/api/me/store` | StoreOwner | — | `200 MyStoreDto` · `404 store.notFound` |
| POST | `/api/me/store` | StoreOwner | `SaveStoreRequest` | `201` · `400 store.*` · `409 alreadyExists / slugTaken` |
| PUT | `/api/me/store` | StoreOwner | `SaveStoreRequest` | `200` · `400` · `404` · `409` |
| POST | `/api/me/store/close` · `/reopen` | StoreOwner | — | `200` · `409 store.notOwnerControlled` |
| GET | `/api/stores/{slug}` | — | — | `200 PublicStoreDto` · `404` |
| GET | `/api/cities` | — | — | `200 string[]` (24 governorate codes) |

## Persistence

Migration `Stores`: table `stores` (owner unique, slug unique, category / logo / banner FKs restrict, `delivery_cities text[]`, `delivery_fee` / `subscription_monthly_price numeric(10,3)`), index `(status, category_id)`.

## Frontend

- **Route:** `/my-store` (`roleGuard('StoreOwner')`), header link "Ma boutique"
- **Components:** `features/my-store` (one form for create / edit, image uploads, governorate checkboxes, close / reopen modal), `core/stores/store-api.ts`, `core/stores/cities.ts`
- **Public store page** comes with 011 (the API is ready)

## Out of scope

- Admin store list / disable / delete → 010
- Per-store subscription price editing → 025
- What happens to a store when the admin revokes its owner's StoreOwner role → 010 (today: the store stays, the owner can't manage it)
