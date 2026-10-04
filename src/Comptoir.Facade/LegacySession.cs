using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Comptoir.Facade;

public sealed record LegacyUser(string Login, string Role, string DisplayName);

/// <summary>
/// Users still sign in on the legacy application (Forms authentication cookie). To call the new API, the facade asks
/// the legacy who the cookie belongs to (GET /api/session), caches the answer for a minute, and mints a token.
/// The new code never parses the legacy cookie: its format and machine key stay the legacy's business (ADR 0005).
/// </summary>
public sealed class LegacySession(IHttpClientFactory clients, IMemoryCache cache)
{
    public const string CookieName = ".COMPTOIRAUTH";
    public const string HttpClientName = "legacy-session";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public Task<LegacyUser?> ResolveAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cookie = request.Cookies[CookieName];
        return string.IsNullOrEmpty(cookie) ? Task.FromResult<LegacyUser?>(null) : ResolveCookieAsync(cookie, cancellationToken);
    }

    public async Task<LegacyUser?> ResolveCookieAsync(string cookie, CancellationToken cancellationToken)
    {
        var key = "session:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie)));
        if (cache.TryGetValue(key, out LegacyUser? cached))
        {
            return cached;
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, "api/session");
        message.Headers.Add("Cookie", $"{CookieName}={cookie}");
        using var response = await clients.CreateClient(HttpClientName).SendAsync(message, cancellationToken);
        // Without a valid cookie, Forms authentication answers 302 to the login page (redirects are not followed).
        LegacyUser? user = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<LegacyUser>(cancellationToken)
            : null;
        if (user is not null)
        {
            cache.Set(key, user, CacheDuration);
        }

        return user;
    }
}
