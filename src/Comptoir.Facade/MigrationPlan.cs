namespace Comptoir.Facade;

/// <summary>Who answers a route: the 2014 application, the new one, or the legacy with the new one checked in its shadow.</summary>
public enum RouteMode
{
    Legacy,
    Shadow,
    New,
}

/// <summary>One line of the migration plan (appsettings "Migration:Routes"). Changing a mode is a configuration change.</summary>
public sealed record MigrationRoute
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public IReadOnlyList<string>? Methods { get; init; }
    public RouteMode Mode { get; init; } = RouteMode.Legacy;
    public string Label { get; init; } = "";
    public int Order { get; init; }

    /// <summary>Answered by the facade's own endpoints rather than proxied (the sign-in screen, ADR 0010).</summary>
    public bool ServedByFacade { get; init; }
}

public sealed class MigrationOptions
{
    public const string Section = "Migration";

    public IReadOnlyList<MigrationRoute> Routes { get; init; } = [];
}
