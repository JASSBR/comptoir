# 0007 — Same contract first, new endpoints second

- **Status:** Accepted · 2026-10-03

## Context

The AngularJS screens are not being rewritten in the same step. They call `/api/products`, `/api/orders/{id}/confirm`…
and parse what Web API 2 returned, errors included.

## Decision

- The new API reproduces the legacy contract **field for field**: same URLs, camelCase JSON, numeric status with the
  French label, and the error shapes the screens parse — `{"message": "…"}` for a 400, a bare JSON string for a 409,
  with the procedures' messages word for word.
- One deliberate difference: an unknown product is a `400 Article inconnu.` where the legacy failed with a
  foreign-key error (HTTP 500).
- New capabilities get new, versioned endpoints (`/api/v2/quotes`) with standard problem details.

## Consequences

- ✅ Switching a route is invisible to users of the old screens.
- ✅ The shadow comparison ([0006](0006-shadow-traffic-before-switching.md)) can compare responses directly.
- ⚠️ The new API carries the legacy's API style on those routes until the old screens are retired; then the contract
  can be modernised behind the same facade.
