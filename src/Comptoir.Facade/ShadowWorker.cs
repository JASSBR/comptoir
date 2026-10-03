using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace Comptoir.Facade;

public sealed record ShadowJob(string RouteId, string Method, string PathAndQuery, string Token, byte[] LegacyBody, double LegacyMs);

/// <summary>
/// Replays shadowed requests on the new API, off the request path: the user always gets the legacy answer at legacy
/// speed. The queue is bounded and drops when full: under load, the shadow is sampled, never a bottleneck.
/// </summary>
public sealed partial class ShadowWorker(IHttpClientFactory clients, ShadowLedger ledger, TimeProvider time, ILogger<ShadowWorker> logger) : BackgroundService
{
    public const string HttpClientName = "new-api";
    private readonly Channel<ShadowJob> _jobs = Channel.CreateBounded<ShadowJob>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropWrite });

    public bool TryEnqueue(ShadowJob job) => _jobs.Writer.TryWrite(job);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _jobs.Reader.ReadAllAsync(stoppingToken))
        {
            ledger.Record(await CompareAsync(job, stoppingToken));
        }
    }

    private async Task<ShadowComparison> CompareAsync(ShadowJob job, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(job.Method), job.PathAndQuery.TrimStart('/'));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", job.Token);
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var newMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (!response.IsSuccessStatusCode)
            {
                return Result(job, ShadowOutcome.Error, [$"new API answered {(int)response.StatusCode}"], newMs);
            }

            var differences = JsonDiff.Compare(JsonNode.Parse(job.LegacyBody), JsonNode.Parse(body));
            if (differences.Count > 0)
            {
                LogMismatch(logger, job.RouteId, job.PathAndQuery, differences[0]);
            }

            return Result(job, differences.Count == 0 ? ShadowOutcome.Match : ShadowOutcome.Mismatch, differences, newMs);
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
        {
            return Result(job, ShadowOutcome.Error, [exception.Message], Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private ShadowComparison Result(ShadowJob job, ShadowOutcome outcome, IReadOnlyList<string> differences, double newMs) =>
        new(job.RouteId, job.Method, job.PathAndQuery, time.GetUtcNow(), outcome, differences, job.LegacyMs, newMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shadow mismatch on {RouteId} {Path}: {FirstDifference}")]
    private static partial void LogMismatch(ILogger logger, string routeId, string path, string firstDifference);
}
