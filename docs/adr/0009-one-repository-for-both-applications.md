# 0009 — One repository for the legacy and the new application

- **Status:** Accepted · 2026-10-05

## Context

In a real engagement the 2014 application already has its repository, with its own history and release process, and
the new application would usually get a repository of its own. During a strangler-fig migration, though, the two are
not independent: they share a SQL Server schema (ADR 0003), the facade decides route by route which one answers
(ADR 0001), and the main safety net — differential tests — runs the legacy stored procedures and the new code side by
side (ADR 0004).

## Decision

One repository holds both: `legacy/` (ASP.NET MVC 5, Web API 2, EF6, AngularJS, the SQL scripts that own the schema),
`src/` and `web/` (.NET 10, Angular 22), and `tests/` (including the differential tests that need both).

- A migration step is one commit: the ported rule, its differential test and the facade's route mode change together,
  so the history reads as the migration itself.
- A schema change made by the legacy scripts is seen at once by the EF Core mapping and the tests on the new side.
- CI builds both: the legacy on a Windows runner (views precompiled), the new side and its images on Linux.
- `legacy/` stays read-only by convention (ADR 0002): what changes there is listed in that record.

## Consequences

- ✅ A reviewer follows the whole migration — before, after and the bridge — in one place.
- ✅ No version skew between the schema, the mapping and the tests that compare the two systems.
- ⚠️ Not how most companies start: the legacy usually has a repository already. There, the same setup is reached by
  bringing the legacy in as a git submodule (or subtree) and keeping the differential tests next to the new code.
- ⚠️ The CI needs two runners (Windows for .NET Framework 4.8, Linux for the rest).
