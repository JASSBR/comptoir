using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Comptoir.Api;
using Comptoir.DbMigrator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;

namespace Comptoir.Api.Tests;

/// <summary>The new API on a real SQL Server holding the legacy schema, procedures and demo data.</summary>
public sealed class ApiFactory : WebApplicationFactory<IApiAssembly>, IAsyncLifetime
{
    public const string SigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _sql.StartAsync();
        ConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "Comptoir" }.ConnectionString;
        var result = LegacyDatabase.Upgrade(ConnectionString);
        if (!result.Successful)
        {
            throw new InvalidOperationException("Legacy scripts failed.", result.Error);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:comptoir", ConnectionString);
        builder.UseSetting("Facade:SigningKey", SigningKey);
    }

    /// <summary>A client carrying the token the facade would mint for this legacy user.</summary>
    public HttpClient ClientFor(string login, string role = "commercial")
    {
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            FacadeToken.Issuer,
            FacadeToken.Audience,
            [new Claim("sub", login), new Claim("role", role)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiGroup : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
