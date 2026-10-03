# Comptoir — instructions for Claude Code

Portfolio project: migrating a 2014 ASP.NET MVC 5 / Web API 2 / EF6 / AngularJS application (.NET Framework 4.8,
business rules in SQL Server stored procedures) to .NET 10 + Angular 22 with the strangler fig pattern.
Goal: show how to migrate without breaking anything, with proof. Preserving behaviour > new features.

## Commands

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build Comptoir.slnx                       # 0 warnings (legacy excluded from analyzers)
dotnet test --solution Comptoir.slnx             # needs Docker (SQL Server)
dotnet format Comptoir.slnx --verify-no-changes
dotnet build legacy/Comptoir.Web                 # compiles on macOS; runs only on Windows (CI, App Service)
dotnet run --project src/Comptoir.AppHost        # facade + API + web against the integration legacy (user secrets)
cd web && npm run format:check && npm run lint && npm test && npm run build
./deploy/azure.sh                                # Terraform + DbUp + legacy package + images
```

## Hard rules

- **Do not change the legacy** (`legacy/`) beyond what an ADR records. Its scripts own the schema until cut-over:
  EF Core maps, never migrates.
- Any rule ported from a stored procedure gets a **differential test** against the procedure on SQL Server, and the
  port keeps legacy quirks unless an ADR says otherwise.
- New endpoints answering a legacy route reproduce its contract exactly (URLs, JSON, error shapes, messages).
- Writes shared with the legacy reproduce its lock hints and lock order.
- A route goes `Legacy` → `Shadow` (reads) → `New` in `src/Comptoir.Facade/appsettings.json`; never straight to `New`
  for a read without shadow evidence.
- The facade never forwards the legacy cookie to the new side; tokens stay short-lived.
- Conventional commits, subject ≤ 70 chars, **no AI attribution**.
