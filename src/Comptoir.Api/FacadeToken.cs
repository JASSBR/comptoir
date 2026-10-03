namespace Comptoir.Api;

/// <summary>What the facade puts in the tokens it mints. Shared by contract, not by reference.</summary>
public static class FacadeToken
{
    public const string Issuer = "comptoir-facade";
    public const string Audience = "comptoir-api";
}

/// <summary>Names the API assembly for WebApplicationFactory (top-level Program is internal).</summary>
public interface IApiAssembly;
