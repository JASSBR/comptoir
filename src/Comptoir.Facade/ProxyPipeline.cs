using System.Diagnostics;
using Yarp.ReverseProxy.Model;

namespace Comptoir.Facade;

/// <summary>What the facade does around YARP, depending on the mode of the matched route.</summary>
public static class ProxyPipeline
{
    public const string ModeMetadata = "mode";

    public static RouteMode ModeOf(HttpContext context) =>
        context.GetReverseProxyFeature().Route.Config.Metadata?.TryGetValue(ModeMetadata, out var mode) == true
            && Enum.TryParse<RouteMode>(mode, out var parsed) ? parsed : RouteMode.Legacy;

    /// <summary>New routes: the legacy session becomes a token, and the legacy cookie never reaches the new code.</summary>
    public static async Task BridgeAuthenticationAsync(HttpContext context, Func<Task> next)
    {
        if (ModeOf(context) != RouteMode.New)
        {
            await next();
            return;
        }

        var user = await context.RequestServices.GetRequiredService<LegacySession>().ResolveAsync(context.Request, context.RequestAborted);
        if (user is null)
        {
            // Same shape as a Web API 2 error: the AngularJS screens already handle it.
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Session expirée : reconnectez-vous." }, context.RequestAborted);
            return;
        }

        context.Request.Headers.Authorization = "Bearer " + context.RequestServices.GetRequiredService<FacadeTokens>().For(user);
        context.Request.Headers.Remove("Cookie");
        await next();
    }

    /// <summary>Shadow routes: the legacy answers the user; the same read is replayed on the new API and compared.</summary>
    public static async Task ShadowAsync(HttpContext context, Func<Task> next)
    {
        if (ModeOf(context) != RouteMode.Shadow || !HttpMethods.IsGet(context.Request.Method))
        {
            await next();
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next();
        }
        finally
        {
            context.Response.Body = original;
        }

        var legacyMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        buffer.Position = 0;
        await buffer.CopyToAsync(original, context.RequestAborted);

        if (context.Response.StatusCode != StatusCodes.Status200OK)
        {
            return;
        }

        var user = await context.RequestServices.GetRequiredService<LegacySession>().ResolveAsync(context.Request, context.RequestAborted);
        if (user is null)
        {
            return;
        }

        var route = context.GetReverseProxyFeature().Route.Config.RouteId;
        var token = context.RequestServices.GetRequiredService<FacadeTokens>().For(user);
        context.RequestServices.GetRequiredService<ShadowWorker>().TryEnqueue(new ShadowJob(
            route, context.Request.Method, context.Request.Path + context.Request.QueryString, token, buffer.ToArray(), legacyMs));
    }
}
