using System.Collections.Concurrent;

namespace Comptoir.Facade;

public enum ShadowOutcome
{
    Match,
    Mismatch,
    Error,
}

public sealed record ShadowComparison(
    string RouteId,
    string Method,
    string Path,
    DateTimeOffset At,
    ShadowOutcome Outcome,
    IReadOnlyList<string> Differences,
    double LegacyMs,
    double NewMs);

public sealed record ShadowStats(int Total, int Matches, int Mismatches, int Errors);

/// <summary>What the shadow traffic has shown so far: the evidence for switching a route to the new application.</summary>
public sealed class ShadowLedger
{
    private const int RecentCapacity = 30;
    private readonly ConcurrentDictionary<string, int[]> _counts = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ShadowComparison> _recent = new();

    public void Record(ShadowComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var counts = _counts.GetOrAdd(comparison.RouteId, _ => new int[3]);
        Interlocked.Increment(ref counts[(int)comparison.Outcome]);
        _recent.Enqueue(comparison);
        var excess = _recent.Count - RecentCapacity;
        for (var i = 0; i < excess; i++)
        {
            _recent.TryDequeue(out _);
        }
    }

    public ShadowStats StatsFor(string routeId)
    {
        if (!_counts.TryGetValue(routeId, out var counts))
        {
            return new ShadowStats(0, 0, 0, 0);
        }

        var (matches, mismatches, errors) = (Volatile.Read(ref counts[0]), Volatile.Read(ref counts[1]), Volatile.Read(ref counts[2]));
        return new ShadowStats(matches + mismatches + errors, matches, mismatches, errors);
    }

    public IReadOnlyList<ShadowComparison> Recent() => [.. _recent.Reverse()];
}
