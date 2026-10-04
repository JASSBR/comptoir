# 0010 — The sign-in screen migrates; the legacy keeps checking passwords

- **Status:** Accepted · 2026-10-05

## Context

The demo opened on the 2014 login page: the first screen anyone saw was the oldest one, with no word on why.
Migrating authentication properly (an identity provider, OIDC) is a project of its own (ADR 0005). Rewriting the
password check in .NET 10 would mean reimplementing the legacy's PBKDF2 hashing and, worse, minting its Forms
ticket — encrypted with the legacy's machine key, a format the new side must never depend on.

## Decision

Migrate the **screen**, not the **check**:

- `GET /Account/Login` is answered by the facade: a redirect to the Angular screen `/app/connexion`, the return URL
  kept. The 2014 screen stays reachable with `?classic` (and `POST /Account/Login` still goes to the legacy).
- `POST /session` (facade): the facade plays the browser on the 2014 form — fetches it for its anti-forgery pair,
  posts the credentials with it — and, on the legacy's redirect, hands the Forms cookie to the real browser
  (`HttpOnly`, `Secure`, `SameSite=Lax`). On a re-rendered form it answers `401` with the legacy's message.
- `DELETE /session` expires the cookie. The plan lists the screen as migrated (`ServedByFacade`: no proxy route).

## Consequences

- ✅ The demo opens on a screen of the new application, which explains the migration instead of hiding it.
- ✅ Passwords, hashes and the ticket format stay the legacy's business; the facade only relays (no secret handled
  beyond the request in flight, nothing logged).
- ✅ The return URL is restricted to local paths: no open redirect.
- ⚠️ The facade depends on the shape of the 2014 form (field names, anti-forgery field). A change there is caught by
  the facade tests' fake legacy only if it is mirrored; the end-to-end smoke test signs in on the real one.
- ⚠️ Still one identity provider, the legacy. Replacing it (Entra ID, Keycloak) remains the step after, and stays
  local to the facade.
