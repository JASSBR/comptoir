using System.Reflection;
using DbUp;
using DbUp.Engine;

namespace Comptoir.DbMigrator;

/// <summary>Replays legacy/Database/*.sql. Used by the deployment and by every test that needs the real legacy schema.</summary>
public static class LegacyDatabase
{
    public static DatabaseUpgradeResult Upgrade(string connectionString, bool log = false)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);
        var builder = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .WithTransactionPerScript();
        builder = log ? builder.LogToConsole() : builder.LogToNowhere();
        return builder.Build().PerformUpgrade();
    }
}
