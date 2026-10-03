using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Comptoir.Facade;

/// <summary>Short-lived tokens for the new API, signed with a key the two services share (never the browser).</summary>
public sealed class FacadeTokens(IConfiguration configuration, TimeProvider time)
{
    public const string Issuer = "comptoir-facade";
    public const string Audience = "comptoir-api";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Facade:SigningKey"] ?? throw new InvalidOperationException("Facade:SigningKey is missing."))),
        SecurityAlgorithms.HmacSha256);

    public string For(LegacyUser user)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Issuer,
            Audience,
            [new Claim("sub", user.Login), new Claim("role", user.Role), new Claim("name", user.DisplayName)],
            notBefore: now,
            expires: now.Add(Lifetime),
            signingCredentials: _credentials));
    }
}
