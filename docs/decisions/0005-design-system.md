# ADR 0005 — Design system (Tailwind utilities)

- **Status:** Accepted
- **Date:** 2026-10-21

## Context

The first screens used raw Tailwind classes repeated in every template (inputs, buttons, cards). They looked basic and drifted apart. We want a premium, consistent look across all roles (clients, store owners, affiliates, admins), in the spirit of Stripe / Lemon Squeezy / Gumroad, without adding a component library (ADR 0003).

## Decision

Design tokens and a small set of **component utilities** live in `frontend/src/styles.css` (Tailwind v4 `@theme` + `@utility`). Templates compose them with ordinary Tailwind utilities.

**Look — "Souk moderne" (revised 2026-10-27, feature 028):** a Tunisian marketplace, not a generic SaaS kit.

- **Color:** whitewashed limestone surfaces (`surface` #f3f5f6, `subtle`), white cards, Sidi Bou Said blue `brand` (#0b5cad), olive `success` (money earned, coins), saffron `accent` (attention, cart count, warnings), harissa red `danger`, medina-night navy `ink` (#10233f) for text and the footer.
- **Type:** Bricolage Grotesque (`font-display`) for every `h1`–`h3`, `page-title`, `section-title`, `stat-value` and prices; IBM Plex Sans for text; IBM Plex Sans Arabic for Arabic (titles included — `--font-display` is remapped under `html[lang='ar']`). Labels are sentence case: no tracked all-caps eyebrows.
- **Shape hierarchy:** buttons, nav links, pills and the language switcher are fully round; cards and dialogs `rounded-xl`; fields `rounded-lg`. No `rounded-2xl`.
- **The arch** (`arch` utility, a Sidi Bou Said doorway) is the one signature motif and means "a door into a store": the logo mark, store logos on store cards, and the home hero, where it frames the newest product photos. Don't use it as decoration elsewhere.
- **Depth:** a hairline navy-tinted `shadow-card` at rest, `shadow-card-hover` + a faint brand border on hover, `shadow-pop` for dialogs.
- **Motion:** feedback only (button press scale, hover lift, focus rings, dialog pop) plus one load moment — the home arch opening (`animate-arch-open`). All of it is cut under `prefers-reduced-motion`.

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
| `stat-tile` + `stat-label` / `stat-value` | Dashboard KPI tiles (023): a figure with its label; a linked tile adds `card-interactive` hover |
| `nav-link`, `nav-link-active` | Header, sidebar and tab navigation |
| `arch` | The Sidi Bou Said doorway shape — only for "a door into a store" (logo mark, store logos, home hero) |
| `page-header`, `back-link` | Top of every page: title + subtitle at the start, actions at the end, hairline below; "← back" above a detail title |
| `panel-toolbar`, `list-row`, `data-table` | List cards: the filter bar, one row (item at the start, meta / actions at the end, stacks on phones), plain tables |
| `detail-grid` | Key / value facts (`<dl>` of `<div><dt/><dd/></div>`) |
| `figure` | Prices, totals, coins and counts that must stand out (display font, tabular numbers) |
| `tabs` + `tab` / `tab-active` | Underlined section tabs (store owner pages, inner tabs); scroll sideways on phones |
| `side-link` / `side-link-active` | Vertical menus (admin sidebar): a brand rule at the start marks the current entry |
| `app-auth-shell` (`shared/layout`) | Sign-in / sign-up frame: navy panel with the doorway arch beside the form from `lg` |

`empty-state` draws an empty dashed arch above its message — don't add another icon.

Tokens: `brand`, `brand-hover`, `brand-soft`, `brand-ring`, `accent`, `accent-ink`, `surface`, `subtle`, `card`, `ink`, `ink-soft`, `muted`, `line`, `line-strong`, `success`, `danger`; fonts `font-sans`, `font-display`, `font-arabic`; shadows `card`, `card-hover`, `pop`, `brand`; animations `animate-fade-in`, `animate-pop-in`, `animate-arch-open` (home hero only).

## Consequences

- New screens use these utilities first; a raw class string repeated in three places is a sign a utility is missing — add it to `styles.css` and this table.
- Still no raw hex in templates, still RTL-safe logical utilities, still every string through Transloco.
- Motion respects `prefers-reduced-motion`.
- **Every width** (checked at 19 widths from 320 to 1440 px, incl. the awkward 640–1279 range, for every role): the header keeps only logo / language / cart / account on the top row, shop links join it from `md`, role links (orders, affiliate, store, admin) from `xl` — below that they sit on a second, scrollable row; labels shorten below `xl`. Side-by-side layouts start where both sides really fit (product page from `md`, cart / checkout summary from `lg`); filter bars keep the search field on its own row until `lg`; never give a button a min width wider than its column.
- **Mobile first** (checked at 320 and 375 px, FR and AR): no horizontal page scroll; rows that hold a person or item plus actions stack (`flex-col sm:flex-row`); long menus become a `<select>` below `lg` (admin); chip rows scroll sideways on phones; the header keeps the language switcher in the top row with short labels (FR / ع / EN) and logout as an icon.
