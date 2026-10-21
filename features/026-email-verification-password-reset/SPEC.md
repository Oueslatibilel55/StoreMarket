# 026 — Email verification & password reset

- **Status:** Draft — later (decided 2026-10-19: v1 starts with simple sign-up / sign-in)
- **Phase:** Later
- **Depends on:** 001, 022
- **Created:** 2026-10-19

## Goal

Make sure every account owns its email, and let users recover a forgotten password.

## Acceptance criteria

- [ ] After sign-up, a verification email with a single-use link (expires in 24 h); account flagged `EmailVerified`
- [ ] Resend verification (rate limited)
- [ ] Decide which actions require a verified email (e.g. becoming an affiliate, opening a store, requesting a payout)
- [ ] "Forgot password": email with a single-use reset link (expires in 1 h); response never reveals whether the email exists
- [ ] Resetting the password revokes all refresh tokens (signs out every device)
- [ ] Emails in FR / AR / EN

## Open questions

- Email provider (SMTP, SendGrid, Brevo…)?
- Also verify the phone number (SMS) for store owners / affiliates?
