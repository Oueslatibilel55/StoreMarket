# ADR 0006 — Hosting (Netlify + Render + Neon) and images in the database

- **Status:** Accepted
- **Date:** 2026-10-21

## Context

We want the app online for demos, for free, the same way as RichLife: frontend on Netlify, API in Docker on Render, PostgreSQL on Neon, redeployed on every push to `main`.

Images (feature 006) were files on the API server disk. A free Render instance has **no persistent disk**. The file system is reset on every deploy and every restart, and the instance restarts each time it wakes up (it sleeps after ~15 min idle). Uploaded photos would keep disappearing.

## Decision

- **Netlify** builds `frontend/` (`netlify.toml` at the repo root) and **proxies** `/api/*` and `/uploads/*` to Render. The browser only ever talks to one origin, so there is no CORS, and the `SameSite=Strict` refresh cookie works.
- **Render** runs `backend/src/WebApp.Api/Dockerfile` (root directory `backend`). Settings come from environment variables only. The API reads the real client IP from `X-Forwarded-For` (`ForwardLimit = 2`: Netlify + Render), so per-IP rate limits stay per visitor.
- **Neon** PostgreSQL 17. Migrations are applied when the API starts (`Database__MigrateOnStartup=true`). The hosted database started **empty**; the first admin comes from `BootstrapAdmin__Emails__0`.
- **Images:** `Storage:Provider` picks the storage behind `IFileStorage`.
  - `Disk` (default, local dev): files in `uploads/`, served as static files.
  - `Database` (Render): bytes in the `file_blobs` table (path → bytea), served by `GET /uploads/{**path}` with the same headers (`nosniff`, immutable cache). URLs and the domain don't change. `ImageService` keeps its order: store the bytes, save the image row, remove the bytes if that save fails.

- **Test data:** `DemoSeed__Password` makes the API seed a small demo set once at startup (`DemoSeeder`, through the application services — same rules and audit as a user). The password lives only in Render, never in the repository.

## Consequences

- Images survive restarts and live next to the data that points to them; a database backup includes them.
- The Neon free plan has 0.5 GB. Images are capped by the `image.maxSizeMb` setting (5 MB), which is plenty for demos. Move to object storage (S3 / R2 / Cloudinary) with another `IFileStorage` once real traffic arrives.
- Every image request on Render reads the database. The immutable cache headers make browsers and Netlify keep the files, so each image is read once per visitor.
- The free Render instance sleeps: the first request after a pause takes 30–60 s.
