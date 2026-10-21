# 006 — Image upload & storage — Tasks

## Backend
- [x] Domain: `ImageSignature` (magic bytes JPEG / PNG / WebP), `StoredImage` (path built from the id), `ImagePurpose`, `image.*` error codes; `StoreCategory.ImageId`; setting `upload.maxImageSizeMb`
- [x] Application: `IFileStorage`, `IImageRepository`, `ImageService` (upload: role per purpose, size, format; delete: owner / admin, not in use; `RequireAsync` purpose check), `ImageUrls`, store category DTOs / service with image
- [x] Infrastructure: `LocalFileStorage` (`Storage:RootPath`, refuses paths outside the root, never overwrites), `StoredImageConfiguration`, `ImageRepository`; migration `Images` created **and applied** to the local Aspire DB
- [x] Api: `ImageEndpoints` (multipart upload, delete), static files under `/uploads` (nosniff, immutable cache)
- [x] Unit tests: `ImageTests` (signatures, SVG / HTML refused, path never from the file name)
- [x] Integration tests: `ImageEndpointsTests` (upload + served file, fake image refused, 413 from the setting, roles per purpose, category image shown + in use + delete removes the file, wrong purpose, only the uploader deletes)
- [ ] Re-encode + thumbnails (library choice)

## Frontend
- [x] `ImagesApi`, `PlatformSettings` (public settings)
- [x] `app-image-upload` component (progress, client checks, replace / remove)
- [x] Store category form + list + home use the image
- [x] Proxy `/uploads`
- [x] i18n FR / AR / EN (303 keys)
- [x] Unit test: `image-upload.spec.ts`

## Done
- [x] Acceptance criteria verified except re-encoding / thumbnails (backend 166 tests, frontend 36 tests; home checked in Chrome)
- [x] Seed: an image (generated colour tile) for every store category
- [x] Docs updated

## Notes
- Unlinking an image (remove in a form) keeps the file; a cleanup job for orphans comes later.
