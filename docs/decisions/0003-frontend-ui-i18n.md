# ADR 0003 — Angular 22, Tailwind CSS v4, EN / FR / AR

- **Status:** Accepted
- **Date:** 2026-10-19

## Context

The UI must look modern and be available in **French, Arabic and English**, with Arabic displayed right-to-left. The project was scaffolded on Angular 20, whose support ends in November 2026, before any real frontend code existed.

## Decision

- **Angular 22** (current release): zoneless by default, Vitest test runner (`ng test`), TypeScript 6. Requires **Node 24** (`frontend/.nvmrc`).
- **Tailwind CSS v4** through `@tailwindcss/postcss`; design tokens (colors, fonts) in `src/styles.css` `@theme`. No component library — UI pieces are built in `shared/`.
- **Transloco** for runtime language switching (one build, no reload): `public/i18n/{fr,ar,en}.json`. Default **FR**, then the browser language, then the user's saved choice.
- **RTL**: `LanguageService` sets `<html lang dir>`; templates use Tailwind logical utilities (`ms-*`, `pe-*`, `start-*`) so layouts flip automatically.
- Fonts: Inter (Latin) + Noto Sans Arabic.

## Consequences

- Every UI string must exist in three files — missing keys fall back to English.
- No ready-made component library: forms, tables, dialogs are our own Tailwind components (more control, more initial work).
- Shared styling (cards, buttons, fields, pills) is the design system of ADR 0005.
- Developers need Node 24 for the frontend (`nvm use 24.21.0`).
