# ADR 0002 — PostgreSQL + .NET Aspire (same setup as RichLife)

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

We need a relational database and a repeatable local environment. The RichLife backend already uses a setup that works well, and reusing it keeps the two projects consistent.

## Decision

- **PostgreSQL 17** with **Npgsql EF Core 10** (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- Local orchestration with **.NET Aspire 9.5.2**: `WebApp.AppHost` starts a persistent Postgres container (fixed host port 62750, named volume `webapp-pgdata`) and the API on a fixed port 5185.
- `docker-compose.yml` as the non-Aspire alternative (Postgres on 5433 + pgAdmin on 5051; the API container is behind the `full` profile).
- `WebApp.ServiceDefaults` provides OpenTelemetry, health checks (`/health`, `/alive`) and HTTP resilience.
- Domain patterns copied from RichLife: `BaseEntity`, `AggregateRoot` with domain events, `Result`, `IUnitOfWork` that dispatches events after `SaveChanges`.

## Consequences

- Docker is required for local development.
- No licence cost; same stack as RichLife, so code and knowledge carry over.
- `Microsoft.EntityFrameworkCore.Relational` is pinned to match EF Core (Npgsql 10.0.2 otherwise pulls an older one and the API fails to start).
