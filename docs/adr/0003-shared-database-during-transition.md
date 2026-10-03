# 0003 — One database for both applications during the transition

- **Status:** Accepted · 2026-10-03

## Context

Orders confirmed by the new code are shipped and invoiced by the legacy procedures, and the other way round. Two
databases would need synchronisation in both directions — more risk than the migration itself.

## Decision

- Both applications use **the same SQL Server database**. Its schema stays owned by `legacy/Database` scripts until
  the cut-over: EF Core in the new API **maps** the tables and never migrates them.
- Where both write, the new code reproduces the legacy's locking exactly: `UPDLOCK` on the order and on each product
  (in id order), the same `UPDLOCK, HOLDLOCK` statement on the numbering table. The status codes (TINYINT), number
  formats and stored amounts are the legacy's.
- The new confirmation runs inside EF Core's retrying execution strategy: if SQL Server picks it as a deadlock victim
  (error 1205), the whole transaction is replayed.

## Consequences

- ✅ Tested end to end: an order confirmed by the new endpoint is shipped and invoiced by the legacy procedures, and
  legacy and new confirmations running together never share a number.
- ⚠️ The legacy design can deadlock under concurrent confirmations (it always could): the new side retries, a legacy
  user still gets an error. Moving confirmation fully to the new side removes it.
- ⚠️ The schema cannot evolve freely until the legacy stops writing to a table. After the cut-over, ownership moves to
  EF Core migrations, one table at a time.
