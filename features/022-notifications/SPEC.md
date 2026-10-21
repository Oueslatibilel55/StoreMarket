# 022 — Notifications (email + in-app)

- **Status:** Draft
- **Phase:** 6 — Admin & operations
- **Depends on:** 016
- **Created:** 2026-10-19

## Goal

Keep everyone informed.

## Acceptance criteria

- [ ] Email + in-app notification for: new order (store), order placed with my code (affiliate), status changes (client), coins earned on delivery (affiliate), store/product disabled by admin (owner), **commission invoice issued / due soon / **store closes in 3-2-1 days** / payment approved or rejected (store owner)**, payout status (affiliate)
- [ ] Sent from domain event handlers, never inline in the request
- [ ] User can see notification list and mark as read
