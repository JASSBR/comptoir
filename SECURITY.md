# Security policy

Comptoir is a portfolio project on fictitious data. If you find a vulnerability anyway, please report it privately
through GitHub's **Report a vulnerability** button rather than in a public issue.

| Concern | Implementation |
|---|---|
| Authentication | Legacy Forms authentication (PBKDF2 hashes), unchanged; the facade turns the session into a 5-minute JWT for the new API — [ADR 0005](docs/adr/0005-auth-bridge-at-the-facade.md) |
| Exposure | The new API has internal ingress only: the facade is its only client |
| Token handling | HMAC key shared by the facade and the API only (Container Apps secret); the legacy cookie is stripped from requests to the new side |
| Input validation | Quantities, customers and products validated before any write; parameterized SQL only (EF Core, `FromSql` interpolation, ADO parameters) |
| Concurrency | Lock hints identical to the legacy procedures; deadlock victims replayed by the execution strategy |
| Abuse | Per-IP rate limiting at the facade |
| Secrets | None in the repository: Terraform-generated, stored as App Service / Container Apps secrets; local development uses user secrets |
| Supply chain | Central package management for the new side, Dependabot, CodeQL, vulnerable-package audit in CI |

Demo-only: the three demo accounts share a published password.
