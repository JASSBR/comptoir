# 0002 — The legacy keeps running as it is (one endpoint added, three defects fixed)

- **Status:** Accepted · 2026-10-03

## Context

Every change to the legacy costs more than it looks: no tests, Windows-only tooling, knowledge gone with the people who
wrote it. Yet the migration needs a little from it (who is signed in) and the team needs to build and deploy it
reliably while it lives.

## Decision

- **One** behavioural change: `GET /api/session` returns the signed-in user, for the facade's auth bridge
  ([0005](0005-auth-bridge-at-the-facade.md)). Nothing else in the legacy code moves.
- Its build is made reproducible without changing what it is: SDK-style project with `MSBuild.SDK.SystemWeb`, so it
  compiles on any machine; CI builds it on **Windows with MSBuild** and **precompiles every Razor view**, so a broken
  view fails the pipeline instead of the first user who opens it.
- Its database scripts become versioned and automated (DbUp, `src/Comptoir.DbMigrator`), replayed identically in
  tests and in deployments.
- Deployment stays what it was in 2014: an xcopy package on IIS (App Service Windows). Dependabot ignores `legacy/`.
- Running it in the cloud surfaced three defects, each fixed with the smallest change and nothing else:
  - Razor files read as Windows-1252 (accents garbled): `<globalization fileEncoding="utf-8">` in `web.config`;
  - Azure SQL serverless pauses when idle and answers error 40613 while resuming: the login page failed with a raw
    IIS error. EF6's `SqlAzureExecutionStrategy` now retries transient errors (`ComptoirDbConfiguration`), and an
    error page replaces the IIS one;
  - EF6 wrapped each stored procedure call in a transaction while the procedures manage their own: their business
    messages came back with "Transaction count after EXECUTE…" appended. The calls now use
    `TransactionalBehavior.DoNotEnsureTransaction`.

## Consequences

- ✅ The legacy is safer to run than before the migration started — a good argument when budgets are discussed.
- ⚠️ Development machines cannot run it (.NET Framework is Windows-only): developers use the integration
  environment's legacy ([0008](0008-hosting-and-local-development.md)).
