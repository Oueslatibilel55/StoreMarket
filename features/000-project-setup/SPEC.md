# 000 — Project setup

- **Status:** Done
- **Created:** 2026-10-19

## Goal

Provide a runnable skeleton: Angular frontend talking to a .NET 10 API, with tests and docs in place.

## Acceptance criteria

- [x] `dotnet build` and `dotnet test` pass in `backend/`
- [x] `GET /api/ping` returns `200 { status, time }`
- [x] `GET /health` and `GET /alive` return `Healthy`
- [x] PostgreSQL 17 wired through EF Core; Aspire AppHost + docker-compose (ADR 0002)
- [x] Angular 22 app builds; home page calls `/api/ping` through the dev proxy (build + unit tests pass; not yet checked in a browser)
- [x] Tailwind v4 + EN / FR / AR with RTL for Arabic (ADR 0003)
- [x] `CLAUDE.md`, `docs/` and `features/` exist

## API contract

| Method | Route | Response |
|---|---|---|
| GET | `/api/ping` | `200 { "status": "ok", "time": "<ISO date>" }` |
| GET | `/health` | `200 Healthy` |
