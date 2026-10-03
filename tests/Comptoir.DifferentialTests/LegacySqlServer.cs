using Comptoir.DbMigrator;
using Testcontainers.MsSql;

namespace Comptoir.DifferentialTests;

/// <summary>A real SQL Server with the legacy schema and stored procedures, exactly as production runs them.</summary>
public sealed class LegacySqlServer : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = "Comptoir" };
        ConnectionString = builder.ConnectionString;
        var result = LegacyDatabase.Upgrade(ConnectionString);
        if (!result.Successful)
        {
            throw new InvalidOperationException("Legacy scripts failed.", result.Error);
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class LegacySqlServerGroup : ICollectionFixture<LegacySqlServer>
{
    public const string Name = "legacy-sql-server";
}
