# 0005 — The legacy session becomes a token at the facade

- **Status:** Accepted · 2026-10-03

## Context

Users sign in on the legacy (Forms authentication cookie, PBKDF2 hashes in the `Users` table). The new API must know
who calls it, without a second login, and without depending on the Forms ticket format or the machine key.

## Decision

- The facade serves the legacy and the new application on **one origin**, so the browser sends the legacy cookie
  everywhere.
- For routes answered by the new side, the facade asks the legacy who the cookie belongs to (`GET /api/session`),
  caches the answer for a minute (keyed by a hash of the cookie), and forwards a **JWT valid five minutes**, signed
  with a key shared by the facade and the API only. The legacy cookie is **removed** from those requests.
- Without a session the facade answers `401 {"message": …}`, the shape Web API 2 used, and the Angular app sends the
  user to the legacy login page, which brings them back.

## Consequences

- ✅ One login for both systems; the new code never parses legacy cookies.
- ✅ Signing out on the legacy ends access to the new side within a minute (cache) and five minutes at most (token).
- ⚠️ The legacy is still the identity provider. Replacing it (OIDC with Entra ID or Keycloak) is a later step, which
  this design makes local to the facade.
