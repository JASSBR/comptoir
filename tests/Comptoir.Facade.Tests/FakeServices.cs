using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Comptoir.Facade.Tests;

/// <summary>Stand-ins for the legacy application and the new API, on real sockets: the facade proxies over HTTP.</summary>
public sealed class FakeServices : IAsyncDisposable
{
    public const string ValidCookie = "valid-forms-ticket";
    public const string AntiForgeryCookie = "__RequestVerificationToken";
    public const string AntiForgeryCookieValue = "cookie-half";
    public const string AntiForgeryFormValue = "form-half";
    private WebApplication? _legacy;
    private WebApplication? _api;

    public string LegacyUrl { get; private set; } = "";
    public string ApiUrl { get; private set; } = "";
    public ConcurrentQueue<(string Path, string? Authorization, string? Cookie)> ApiCalls { get; } = new();
    public int AvailableInNewApi { get; set; } = 238;

    public async Task StartAsync()
    {
        _legacy = Create();
        _legacy.MapGet("/api/session", (HttpRequest request) =>
            request.Cookies[".COMPTOIRAUTH"] == ValidCookie
                ? Results.Ok(new { login = "sophie", role = "commercial", displayName = "Sophie Moreau" })
                : Results.Redirect("/Account/Login?ReturnUrl=%2fapi%2fsession"));
        _legacy.MapGet("/api/orders", () => Results.Ok(new[] { new { id = 1, available = 238, label = "Farine" } }));
        // What IIS does when the browser sends Accept-Encoding: gzip.
        _legacy.MapGet("/api/orders/{id:int}", async (HttpContext context) =>
        {
            context.Response.ContentType = "application/json";
            context.Response.Headers.ContentEncoding = "gzip";
            await using var gzip = new System.IO.Compression.GZipStream(context.Response.Body, System.IO.Compression.CompressionLevel.Fastest);
            await gzip.WriteAsync("{\"id\":7,\"label\":\"Farine\"}"u8.ToArray());
        });
        // The 2014 login, as ASP.NET MVC 5 renders it: an anti-forgery pair (hidden field + cookie), then a Forms cookie.
        _legacy.MapGet("/Account/Login", (HttpContext context) =>
        {
            context.Response.Headers.Append("Set-Cookie", $"{AntiForgeryCookie}={AntiForgeryCookieValue}; path=/; HttpOnly");
            return Results.Text(
                "<title>Connexion - Comptoir Durand</title><form method=\"post\">" +
                $"<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"{AntiForgeryFormValue}\" /></form>",
                "text/html");
        });
        _legacy.MapPost("/Account/Login", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var tokens = form["__RequestVerificationToken"] == AntiForgeryFormValue
                && context.Request.Cookies[AntiForgeryCookie] == AntiForgeryCookieValue;
            if (!tokens)
            {
                return Results.BadRequest();
            }

            if (form["login"] != "sophie" || form["password"] != "comptoir-demo")
            {
                return Results.Text("<div class=\"alert alert-danger\">Identifiant ou mot de passe incorrect.</div>", "text/html");
            }

            context.Response.Headers.Append("Set-Cookie", $".COMPTOIRAUTH={ValidCookie}; path=/; HttpOnly");
            return Results.Redirect("/");
        });
        LegacyUrl = await StartAsync(_legacy);

        _api = Create();
        _api.MapGet("/api/products", (HttpRequest request) =>
        {
            ApiCalls.Enqueue((request.Path, request.Headers.Authorization, request.Headers.Cookie));
            return Results.Ok(new[] { new { id = 1 } });
        });
        _api.MapGet("/api/orders", (HttpRequest request) =>
        {
            ApiCalls.Enqueue((request.Path, request.Headers.Authorization, request.Headers.Cookie));
            return Results.Ok(new[] { new { label = "Farine", available = AvailableInNewApi, id = 1.0m } });
        });
        _api.MapGet("/api/orders/{id:int}", () => Results.Ok(new { label = "Farine", id = 7 }));
        ApiUrl = await StartAsync(_api);
    }

    public async ValueTask DisposeAsync()
    {
        if (_legacy is not null)
        {
            await _legacy.DisposeAsync();
        }

        if (_api is not null)
        {
            await _api.DisposeAsync();
        }
    }

    private static WebApplication Create()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        return builder.Build();
    }

    private static async Task<string> StartAsync(WebApplication app)
    {
        await app.StartAsync();
        return app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
    }
}
