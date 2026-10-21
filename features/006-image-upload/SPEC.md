# 006 — Image upload & storage

- **Status:** Done (v1) — re-encoding / thumbnails pending a library choice (see open questions)
- **Phase:** 2 — Stores & catalog
- **Depends on:** 001
- **Created:** 2026-10-19 · **Done:** 2026-10-20

## Goal

Upload and serve images for stores, products and store categories.

## Decisions (2026-10-20)

- **Files on the API server's disk, served by ASP.NET as static files** under `/uploads/...`; the database keeps the **relative path** of each file (`images.path`), entities keep the image **id**.
- Storage behind `IFileStorage` (`LocalFileStorage`, folder `Storage:RootPath`, default `uploads/` next to the API, git-ignored) — a cloud bucket later is one new class + config.
- Accepted: **JPEG, PNG, WebP**, recognised by their **magic bytes** (the file name and the browser's Content-Type are ignored). No SVG (can carry scripts), no GIF.
- Max size = setting **`upload.maxImageSizeMb`** (default **5 MB**, 1–20).
- The path is built by the server from the image id (`product-photos/2026/10/{id}.webp`) — nothing the uploader sends ends up in it; paths that would leave the storage root are refused.
- Served with `X-Content-Type-Options: nosniff` and a 1-year immutable cache (a file never changes: new upload = new id).
- **Who uploads what:** `CategoryImage` → Admin; `StoreLogo` / `StoreBanner` / `ProductPhoto` → StoreOwner (or Admin).
- First consumer: **store categories** get an optional image (shown instead of the emoji).

## Acceptance criteria

- [x] Upload endpoint: type check (jpg/png/webp by content), max size from the settings
- [x] Storage behind an `IFileStorage` interface (local disk; cloud blob later)
- [x] Served as static files; database keeps the path
- [x] Only the uploader (or an admin) can delete an image, and only when nothing uses it (`409 image.inUse`, FK as backstop)
- [x] An entity only accepts an image of its purpose (`image.wrongPurpose`)
- [ ] Image re-encoded (strip metadata such as GPS) — **needs an image library**
- [ ] Thumbnails (e.g. 200 / 600 / 1200 px) — **needs an image library**
- [ ] Orphans (uploaded, never attached, or unlinked) cleaned up by a background job — later
- [ ] Attach rules for stores / products (owner only) → **007 / 009** (add their references to `ImageRepository.IsInUseAsync`)

## API contract

| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| POST | `/api/images` | signed in (role per purpose) | multipart: `file`, `purpose` | `201 ImageDto { id, url, purpose, contentType, sizeBytes, originalName }` · `400 image.required / unsupportedType` · `403 image.purposeNotAllowed` · `413 image.tooLarge` |
| DELETE | `/api/images/{id}` | uploader or Admin | — | `204` · `404` · `409 image.inUse` |
| GET | `/uploads/{path}` | — | — | the file (static) |

## Persistence

Migration `Images`: table `images` (`uploaded_by_id`, `purpose`, `path` unique, `content_type`, `size_bytes`, `original_name`); `store_categories.image_id` → `images` (restrict).

## Frontend

- `core/images/images-api.ts` (upload with progress), `core/settings/platform-settings.ts` (public settings, read once)
- `shared/ui/image-upload` — pick → uploads at once → emits `{ id, url }` (or null on remove); same type / size checks as the server
- Store category form: image field; list and home show the image instead of the emoji
- Dev proxy forwards `/uploads` to the API

## Open questions

- [ ] **Image library** for re-encoding + thumbnails: SkiaSharp (MIT, free) or ImageSharp (Six Labors Split License — free under USD 1M yearly revenue, paid above)? Recommendation: SkiaSharp.
- [ ] Production storage: keep server disk (needs a persistent volume + backups) or move to S3-compatible / Azure Blob?
