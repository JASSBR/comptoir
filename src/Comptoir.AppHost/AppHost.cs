// `dotnet run --project src/Comptoir.AppHost` starts the new side of the migration: the facade, the new API and the
// Angular dev server, with traces in the Aspire dashboard.
// The legacy cannot run here (.NET Framework 4.8 needs Windows): like most legacy migrations, development uses the
// integration environment's legacy application and its database (user secrets, see README).
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddConnectionString("comptoir");
var legacyUrl = builder.AddParameter("legacy-url");
var signingKey = builder.AddParameter("facade-signing-key", new GenerateParameterDefault { MinLength = 48, Special = false }, secret: true, persist: true);

var api = builder.AddProject<Projects.Comptoir_Api>("api")
    .WithReference(database)
    .WithEnvironment("Facade__SigningKey", signingKey)
    .WithHttpHealthCheck("/health");

var web = builder.AddJavaScriptApp("web", "../../web", "start")
    .WithHttpEndpoint(port: 4200, env: "PORT");

// Open the facade, not the dev server: like production, one origin serves the legacy screens and /app.
builder.AddProject<Projects.Comptoir_Facade>("facade")
    .WithReference(api).WaitFor(api)
    .WithEnvironment("Legacy__BaseUrl", legacyUrl)
    .WithEnvironment("Api__BaseUrl", "https+http://api")
    .WithEnvironment("Facade__SigningKey", signingKey)
    .WithEnvironment("Web__DevServerUrl", web.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
