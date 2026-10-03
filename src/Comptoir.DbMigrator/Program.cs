using Comptoir.DbMigrator;

// Applies legacy/Database/*.sql in name order, each exactly once (journal table dbo.SchemaVersions).
// The schema is still owned by those scripts during the migration: EF Core in the new application maps it, never
// migrates it (docs/adr/0003-shared-database-during-transition.md).
// Usage: dotnet run --project src/Comptoir.DbMigrator -- "<connection string>"   (or env ConnectionStrings__comptoir)
var connectionString = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("ConnectionStrings__comptoir");
if (string.IsNullOrWhiteSpace(connectionString))
{
    await Console.Error.WriteLineAsync("Connection string missing: pass it as the first argument or set ConnectionStrings__comptoir.");
    return 2;
}

var upgrade = LegacyDatabase.Upgrade(connectionString, log: true);
if (!upgrade.Successful)
{
    await Console.Error.WriteLineAsync(upgrade.Error.ToString());
    return 1;
}

return 0;
