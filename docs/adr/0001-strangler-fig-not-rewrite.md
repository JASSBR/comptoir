# 0001 — Strangler fig behind a facade, not a rewrite

- **Status:** Accepted · 2026-10-03

## Context

Comptoir Durand runs its sales on a 2014 application: ASP.NET MVC 5, Web API 2, EF6, AngularJS 1.x, business rules in
SQL Server stored procedures, no tests. .NET Framework 4.8 and AngularJS are frozen, hiring for them is hard, and the
business wants features the design cannot deliver (a quote that does not need a saved order, for instance).
A rewrite would freeze features for months and switch everything on one night — the classic way such projects fail.

## Decision

- A **YARP facade** receives all traffic. Its migration plan (`Migration:Routes` in configuration) says, route by
  route, who answers: `Legacy`, `New`, or `Shadow` (legacy answers, new is checked — [0006](0006-shadow-traffic-before-switching.md)).
- Functions move one at a time, smallest risk first: catalogue reads, then order pricing and confirmation. Shipping,
  invoicing and cancellation still run on the legacy.
- Users keep their screens: the AngularJS UI calls the same URLs, whoever answers them ([0007](0007-same-contract-first.md)).
  New screens (Angular 22) appear next to the old ones, under `/app`, on the same origin.

## Consequences

- ✅ Value from the first week (catalogue, express quote), and every step can be rolled back by changing one line of
  configuration.
- ✅ The migration's progress is measurable and visible: the dashboard shows the plan and the shadow results.
- ⚠️ Two systems run for a while, on one database ([0003](0003-shared-database-during-transition.md)): concurrency
  between them must be designed, not hoped for.
- ⚠️ The facade is now on the critical path; it stays thin (routing, auth bridge, shadow) and has its own tests.
