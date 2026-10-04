using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Comptoir.Facade;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var legacyUrl = builder.Configuration["Legacy:BaseUrl"] ?? throw new InvalidOperationException("Legacy:BaseUrl is missing.");
var apiUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl is missing.");
builder.Services.Configure<MigrationOptions>(builder.Configuration.GetSection(MigrationOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LegacySession>();
builder.Services.AddSingleton<LegacySignIn>();
builder.Services.AddSingleton<FacadeTokens>();
builder.Services.AddSingleton<ShadowLedger>();
builder.Services.AddSingleton<ShadowWorker>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ShadowWorker>());

builder.Services.AddHttpClient(LegacySession.HttpClientName, client => client.BaseAddress = new Uri(legacyUrl))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHttpClient(ShadowWorker.HttpClientName, client => client.BaseAddress = new Uri(new Uri(apiUrl), "."))
    .AddServiceDiscovery();

var plan = builder.Configuration.GetSection(MigrationOptions.Section).Get<MigrationOptions>() ?? new MigrationOptions();
var routes = plan.Routes.Where(route => !route.ServedByFacade).Select(route => new RouteConfig
{
    RouteId = route.Id,
    Order = route.Order,
    ClusterId = route.Mode == RouteMode.New ? "api" : "legacy",
    Match = new RouteMatch { Path = route.Path, Methods = route.Methods },
    Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { [ProxyPipeline.ModeMetadata] = route.Mode.ToString() },
}).ToList();
var clusters = new List<ClusterConfig>
{
    // The legacy is an App Service: it routes on the Host header, so requests carry its own host name.
    new() { ClusterId = "legacy", Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal) { ["legacy"] = new() { Address = legacyUrl } } },
    new() { ClusterId = "api", Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal) { ["api"] = new() { Address = apiUrl } } },
};

// Development: /app goes to the Angular dev server (live reload) instead of the built files, behind the same origin.
if (builder.Configuration["Web:DevServerUrl"] is { Length: > 0 } devServer)
{
    routes.Add(new RouteConfig { RouteId = "web-dev", Order = 0, ClusterId = "web", Match = new RouteMatch { Path = "/app/{**rest}" } });
    clusters.Add(new ClusterConfig { ClusterId = "web", Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal) { ["web"] = new() { Address = devServer } } });
}

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters).AddServiceDiscoveryDestinationResolver();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitPerMinute", 600), Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse("100.64.0.0/10"));
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseRateLimiter();
// Before routing: the legacy catch-all route matches every path, and a matched endpoint disables static files.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapDefaultEndpoints();

// The migration dashboard's data: the plan, and what the shadow traffic proved for each route.
app.MapGet("/migration/status", (IOptions<MigrationOptions> options, ShadowLedger ledger) => TypedResults.Ok(new
{
    routes = options.Value.Routes.OrderBy(r => r.Order).Select(r => new
    {
        r.Id,
        r.Label,
        r.Path,
        methods = r.Methods ?? ["*"],
        mode = r.Mode.ToString(),
        shadow = ledger.StatsFor(r.Id),
    }),
    recent = ledger.Recent(),
}));

// The new Angular application, served by the facade so that it shares the legacy session cookie (same origin).
// Deep links (/app/devis) get index.html: an explicit route, so it wins over the legacy catch-all.
if (app.Configuration["Web:DevServerUrl"] is not { Length: > 0 })
{
    app.MapGet("/app/{**path}", (IWebHostEnvironment environment) =>
        TypedResults.PhysicalFile(Path.Combine(environment.WebRootPath, "app", "index.html"), "text/html"));
}

// Sign-in, migrated (ADR 0010): the new screen at /app/connexion, the legacy still checking the password.
// The 2014 screen stays one click away (?classic): the migration is shown, not hidden.
var legacyInvoker = new HttpMessageInvoker(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false });
app.MapGet("/Account/Login", async (HttpContext context, IHttpForwarder forwarder) =>
{
    if (context.Request.Query.ContainsKey("classic"))
    {
        await forwarder.SendAsync(context, legacyUrl, legacyInvoker);
        return;
    }

    var returnUrl = context.Request.Query["ReturnUrl"].ToString();
    context.Response.Redirect(returnUrl.Length > 0 ? $"/app/connexion?returnUrl={Uri.EscapeDataString(returnUrl)}" : "/app/connexion");
});
app.MapPost("/session", async (SignInRequest request, HttpContext context, LegacySignIn signIn, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password))
    {
        return Results.Json(new { message = LegacySignIn.WrongCredentials }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await signIn.SignInAsync(request.Login.Trim(), request.Password, cancellationToken);
    if (result is not { } signedIn)
    {
        return Results.Json(new { message = LegacySignIn.WrongCredentials }, statusCode: StatusCodes.Status401Unauthorized);
    }

    context.Response.Cookies.Append(LegacySession.CookieName, signedIn.Cookie, new CookieOptions
    {
        // Always Secure: the demo is HTTPS end to end, and browsers accept Secure cookies on localhost.
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
    });
    return Results.Ok(signedIn.User);
});
app.MapDelete("/session", (HttpContext context) =>
{
    context.Response.Cookies.Delete(LegacySession.CookieName, new CookieOptions { Path = "/", HttpOnly = true, Secure = true });
    return Results.NoContent();
});

app.MapReverseProxy(proxy =>
{
    proxy.Use(ProxyPipeline.BridgeAuthenticationAsync);
    proxy.Use(ProxyPipeline.ShadowAsync);
});

await app.RunAsync();
