using System.Net;
using System.Text.RegularExpressions;

namespace Comptoir.Facade;

public sealed record SignInRequest(string? Login, string? Password);

/// <summary>
/// The new sign-in screen, with the legacy still the identity provider (ADR 0010): the facade plays the browser on
/// the 2014 login form — fetches it for its anti-forgery pair, posts the credentials — and hands the resulting Forms
/// cookie to the real browser. Passwords are still checked by the legacy, against its PBKDF2 hashes; the Forms ticket
/// is still encrypted with its machine key. Nothing of either is reimplemented here.
/// </summary>
public sealed partial class LegacySignIn(IHttpClientFactory clients, LegacySession session)
{
    public const string WrongCredentials = "Identifiant ou mot de passe incorrect.";

    public async Task<(LegacyUser User, string Cookie)?> SignInAsync(string login, string password, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(LegacySession.HttpClientName);

        using var form = await client.GetAsync("Account/Login", cancellationToken);
        var antiForgeryCookies = CookiesFrom(form).ToList();
        var token = AntiForgeryField().Match(await form.Content.ReadAsStringAsync(cancellationToken)).Groups["token"].Value;
        if (token.Length == 0 || antiForgeryCookies.Count == 0)
        {
            throw new InvalidOperationException("The legacy login form no longer carries its anti-forgery token.");
        }

        using var post = new HttpRequestMessage(HttpMethod.Post, "Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["__RequestVerificationToken"] = token,
                ["login"] = login,
                ["password"] = password,
            }),
        };
        post.Headers.Add("Cookie", string.Join("; ", antiForgeryCookies.Select(cookie => $"{cookie.Name}={cookie.Value}")));
        using var response = await client.SendAsync(post, cancellationToken);

        // Success is a redirect carrying the Forms cookie; a failure re-renders the form (200) with its message.
        var formsCookie = CookiesFrom(response)
            .Where(cookie => string.Equals(cookie.Name, LegacySession.CookieName, StringComparison.Ordinal))
            .Select(cookie => cookie.Value)
            .FirstOrDefault();
        if (response.StatusCode != HttpStatusCode.Redirect || string.IsNullOrEmpty(formsCookie))
        {
            return null;
        }

        var user = await session.ResolveCookieAsync(formsCookie, cancellationToken);
        return user is null ? null : (user, formsCookie);
    }

    private static IEnumerable<(string Name, string Value)> CookiesFrom(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var headers)
            ? headers.Select(header => header.Split(';', 2)[0].Split('=', 2))
                .Where(pair => pair.Length == 2)
                .Select(pair => (pair[0].Trim(), pair[1].Trim()))
            : [];

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="(?<token>[^"]+)""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AntiForgeryField();
}
