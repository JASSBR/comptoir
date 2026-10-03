# 0006 — Shadow traffic before switching a route

- **Status:** Accepted · 2026-10-03

## Context

Tests cover what we thought of. Production data covers what we did not: the odd customer code, the order with fifty
lines, the product with an empty label.

## Decision

- A route in `Shadow` mode is answered by the legacy, as before. The facade buffers that response, then a background
  worker replays the same **read** on the new API and compares the two JSON documents semantically (numbers by value,
  keys in any order). Differences are recorded with their JSON path.
- The replay never delays the user: the queue is bounded and drops work when full (the shadow is sampled under load).
- Only `GET` routes are shadowed. Writes are migrated through tests and the differential suite
  ([0004](0004-pricing-parity-by-differential-testing.md)), never executed twice.
- The dashboard (`/app/migration`) shows, per route, how many comparisons ran and how many were identical; a route
  moves to `New` when its shadow is clean.

## Consequences

- ✅ Real evidence before each switch, visible to the business, not only to developers.
- ⚠️ A difference can come from timing (stock changed between the two reads), not from a defect: the recorded JSON
  paths make that easy to tell.
- ⚠️ Comparisons are kept in memory, so the facade runs as a single replica; persisting them is the change needed to
  scale it out.
