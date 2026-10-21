# ADR 0001 — Tech stack

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

We need a maintainable web application with a typed SPA frontend and a robust API backend.

## Decision

- Frontend: **Angular 22** (standalone, signals, zoneless)
- Backend: **.NET 10** with ASP.NET Core **Minimal APIs**
- Backend structure: **Clean Architecture** (Domain / Application / Infrastructure / Api)
- Tests: xUnit (backend), Vitest (frontend)

## Consequences

- Clear layer boundaries; Domain stays framework-free and easy to unit test.
- More projects/boilerplate than a single-project API; acceptable for a long-lived app.
- Frontend requires Node 24 on every dev machine and in CI.
