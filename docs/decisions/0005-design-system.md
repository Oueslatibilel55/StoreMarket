# ADR 0005 — Design system (Tailwind utilities)

- **Status:** Accepted
- **Date:** 2026-10-21

## Context

The first screens used raw Tailwind classes repeated in every template (inputs, buttons, cards). They looked basic and drifted apart. We want a premium, consistent look across all roles (clients, store owners, affiliates, admins), in the spirit of Stripe / Lemon Squeezy / Gumroad, without adding a component library (ADR 0003).

## Decision

Design tokens and a small set of **component utilities** live in `frontend/src/styles.css` (Tailwind v4 `@theme` + `@utility`). Templates compose them with ordinary Tailwind utilities.

**Look:** slate neutrals on a near-white background (`bg-surface`), white cards with micro-borders (`border-line`), soft multi-layer shadows (`shadow-card` → `hover:shadow-card-hover`, `shadow-pop` for dialogs), `rounded-xl` everywhere, generous spacing (`gap-6`, `p-5 sm:p-6`), 200 ms transitions, indigo brand.

| Utility | Use |
|---|---|
| `page`, `page-narrow` | Page container (max width + responsive padding) |
| `page-title`, `page-subtitle`, `section-title`, `eyebrow` | Typography hierarchy |
| `card`, `card-interactive`, `card-header`, `card-body`, `row-hover` | Surfaces; `card-interactive` lifts on hover (`-translate-y-0.5` + `shadow-card-hover`) |
| `btn` + `btn-primary` / `btn-secondary` / `btn-ghost` / `btn-danger` / `btn-danger-soft` / `btn-success`, sizes `btn-sm` / `btn-lg` | Buttons and button-like links |
| `link` | Inline text links |
| `field-label`, `field`, `field-hint`, `field-error`, `checkbox` | Forms (`field` works on input, select, textarea) |
| `alert-danger` / `alert-success` / `alert-info`, `empty-state` | Feedback |
| `pill` + `pill-neutral` / `pill-brand` / `pill-success` / `pill-warning` / `pill-danger` / `pill-dark`, `code-chip` | Compact status, role and commission badges; codes like `#DRF-45` |
| `nav-link`, `nav-link-active` | Header, sidebar and tab navigation |

Tokens: `brand`, `brand-hover`, `brand-soft`, `brand-ring`, `accent`, `accent-ink`, `surface`, `subtle`, `card`, `ink`, `ink-soft`, `muted`, `line`, `line-strong`, `success`, `danger`; shadows `card`, `card-hover`, `pop`, `brand`; animations `animate-fade-in`, `animate-pop-in`.

## Consequences

- New screens use these utilities first; a raw class string repeated in three places is a sign a utility is missing — add it to `styles.css` and this table.
- Still no raw hex in templates, still RTL-safe logical utilities, still every string through Transloco.
- Motion respects `prefers-reduced-motion`.
- **Every width** (checked at 19 widths from 320 to 1440 px, incl. the awkward 640–1279 range, for every role): the header keeps only logo / language / cart / account on the top row, shop links join it from `md`, role links (orders, affiliate, store, admin) from `xl` — below that they sit on a second, scrollable row; labels shorten below `xl`. Side-by-side layouts start where both sides really fit (product page from `md`, cart / checkout summary from `lg`); filter bars keep the search field on its own row until `lg`; never give a button a min width wider than its column.
- **Mobile first** (checked at 320 and 375 px, FR and AR): no horizontal page scroll; rows that hold a person or item plus actions stack (`flex-col sm:flex-row`); long menus become a `<select>` below `lg` (admin); chip rows scroll sideways on phones; the header keeps the language switcher in the top row with short labels (FR / ع / EN) and logout as an icon.
