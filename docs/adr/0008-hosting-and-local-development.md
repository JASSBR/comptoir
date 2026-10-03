# 0008 — Hosting: IIS for the legacy, containers for the new side

- **Status:** Accepted · 2026-10-03

## Decision

- **Legacy**: Azure App Service on **Windows**, IIS, .NET Framework 4.8 (F1 tier: free, no always-on, 32-bit worker),
  deployed as a zip of the CI package. Sticky-session cookies are off: only the facade calls it.
- **Database**: Azure SQL Database, serverless, on the **free offer** (pauses instead of billing when the monthly
  allowance is spent). Connection pools are capped per application.
- **New side**: the facade (public, serves the Angular build under `/app`) and the API (internal ingress: only the
  facade reaches it) on Azure Container Apps, images pulled with a managed identity.
- Everything is Terraform (`deploy/terraform`); `deploy/azure.sh` builds the images, applies, runs the database
  scripts and deploys the legacy package.
- **Local development**: the legacy cannot run on a Mac or Linux machine. As in most migrations, developers use the
  integration environment's legacy and database; Aspire (`src/Comptoir.AppHost`) runs the facade, the API and the
  Angular dev server locally, and the facade proxies `/app` to the dev server so the browser sees one origin, as in
  production.

## Consequences

- ✅ The demo costs nothing at rest; every part runs on the platform it would run on in a real company.
- ⚠️ The free tiers sleep: the first request after a quiet period wakes the legacy (IIS) and the containers, which can
  take tens of seconds.
