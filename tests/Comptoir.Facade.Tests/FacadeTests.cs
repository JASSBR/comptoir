using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Comptoir.Facade.Tests;

public sealed class FacadeTests : IAsyncLifetime
{
    private readonly FakeServices _fakes = new();
    private WebApplicationFactory<ShadowLedger> _facade = null!;

    public async ValueTask InitializeAsync()
    {
        await _fakes.StartAsync();
        _facade = new WebApplicationFactory<ShadowLedger>().WithWebHostBuilder(builder => builder
            .UseSetting("Legacy:BaseUrl", _fakes.LegacyUrl)
            .UseSetting("Api:BaseUrl", _fakes.ApiUrl)
            .UseSetting("Facade:SigningKey", "test-signing-key-that-is-long-enough-for-hmac-sha256"));
    }

    public async ValueTask DisposeAsync()
    {
        await _facade.DisposeAsync();
        await _fakes.DisposeAsync();
    }

    [Fact]
    public async Task LegacyRoutes_AreProxiedToTheLegacyApplication()
    {
        var page = await _facade.CreateClient().GetStringAsync("/Account/Login", TestContext.Current.CancellationToken);
        page.ShouldContain("Connexion - Comptoir Durand");
    }

    [Fact]
    public async Task NewRoutes_WithoutALegacySession_AnswerLikeWebApi()
    {
        var response = await _facade.CreateClient().GetAsync("/api/products", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("message").GetString().ShouldNotBeNullOrEmpty();
        _fakes.ApiCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewRoutes_TurnTheLegacySessionIntoAToken_AndDropTheCookie()
    {
        var response = await SignedIn().GetAsync("/api/products", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var call = _fakes.ApiCalls.ShouldHaveSingleItem();
        call.Cookie.ShouldBeNullOrEmpty();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(call.Authorization!["Bearer ".Length..]);
        token.Issuer.ShouldBe("comptoir-facade");
        token.Audiences.ShouldBe(["comptoir-api"]);
        token.Claims.Single(c => c.Type == "sub").Value.ShouldBe("sophie");
        token.Claims.Single(c => c.Type == "role").Value.ShouldBe("commercial");
        token.ValidTo.ShouldBeLessThan(DateTime.UtcNow.AddMinutes(6));
    }

    [Fact]
    public async Task ShadowRoutes_AnswerFromTheLegacy_AndRecordTheComparison()
    {
        var client = SignedIn();

        var body = await client.GetStringAsync("/api/orders", TestContext.Current.CancellationToken);
        body.ShouldContain("\"available\":238");
        var status = await WaitForShadowAsync(client, "orders-list", total: 1);
        status.GetProperty("matches").GetInt32().ShouldBe(1);

        _fakes.AvailableInNewApi = 237;
        await client.GetStringAsync("/api/orders", TestContext.Current.CancellationToken);
        status = await WaitForShadowAsync(client, "orders-list", total: 2);
        status.GetProperty("mismatches").GetInt32().ShouldBe(1);

        var recent = (await client.GetFromJsonAsync<JsonElement>("/migration/status", TestContext.Current.CancellationToken)).GetProperty("recent")[0];
        recent.GetProperty("outcome").GetString().ShouldBe("Mismatch");
        recent.GetProperty("differences")[0].GetString().ShouldBe("$[0].available: legacy 238, new 237");
    }

    [Fact]
    public async Task ShadowRoutes_CompareCompressedLegacyAnswers_AndPassThemOnUntouched()
    {
        var client = SignedIn();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/orders/7");
        request.Headers.AcceptEncoding.ParseAdd("gzip");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Content.Headers.ContentEncoding.ShouldBe(["gzip"]);
        var status = await WaitForShadowAsync(client, "orders-detail", total: 1);
        status.GetProperty("matches").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Status_ListsThePlanInOrder()
    {
        var status = await _facade.CreateClient().GetFromJsonAsync<JsonElement>("/migration/status", TestContext.Current.CancellationToken);
        var routes = status.GetProperty("routes").EnumerateArray().ToList();

        routes[^1].GetProperty("id").GetString().ShouldBe("legacy");
        routes.Select(r => r.GetProperty("mode").GetString()).Distinct().Order().ShouldBe(["Legacy", "New", "Shadow"]);
    }

    private HttpClient SignedIn()
    {
        var client = _facade.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $".COMPTOIRAUTH={FakeServices.ValidCookie}");
        return client;
    }

    private static async Task<JsonElement> WaitForShadowAsync(HttpClient client, string routeId, int total)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var status = await client.GetFromJsonAsync<JsonElement>("/migration/status", TestContext.Current.CancellationToken);
            var shadow = status.GetProperty("routes").EnumerateArray().Single(r => r.GetProperty("id").GetString() == routeId).GetProperty("shadow");
            if (shadow.GetProperty("total").GetInt32() >= total)
            {
                return shadow;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No shadow comparison recorded for {routeId}.");
    }
}
