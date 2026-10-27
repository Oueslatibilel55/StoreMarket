# 028 — Visual refresh "Souk moderne"

- **Status:** Done
- **Owner:** Oueslati Bilel
- **Created:** 2026-10-27

## Goal

Give StoreMarket its own identity instead of a generic SaaS look, consistently on every screen, by changing the
design system (ADR 0005) rather than each page.

## Acceptance criteria

- [x] New tokens in `frontend/src/styles.css`: limestone surfaces, Sidi Bou Said blue, olive, saffron, harissa red, navy ink.
- [x] Bricolage Grotesque for titles, figures and prices; IBM Plex Sans / Plex Sans Arabic for text (Arabic titles stay in Plex Arabic).
- [x] Sentence-case labels (`eyebrow` no longer upper-cases).
- [x] Round buttons, nav links and language switcher; `rounded-xl` cards; `rounded-lg` fields.
- [x] `arch` motif: logo mark (header, footer), store logos, home hero doorway framing the newest product photos.
- [x] Every screen re-laid out with shared page patterns (page header, list cards, detail facts, figures, underlined tabs, admin side menu, split sign-in / sign-up).
- [x] Header with a brand top rule, saffron cart count; navy footer.
- [x] Motion limited to feedback + the home arch opening; all cut under `prefers-reduced-motion`.
- [x] FR / AR (RTL) / EN; build and unit tests green.
- [ ] Manual check of every role's screens at 320 / 375 px.

## Backend

None.

## Frontend

- `src/styles.css`, `src/index.html` (fonts, title, theme colour)
- `shared/layout/header.html`, `shared/layout/footer.ts`, `shared/language-switcher`, `shared/ui/modal.ts`
- `shared/catalog/product-card.ts`, `shared/catalog/store-card.ts`
- `features/home/*`

## Out of scope

- Dark mode.
- Renaming the `app.name` i18n value.
