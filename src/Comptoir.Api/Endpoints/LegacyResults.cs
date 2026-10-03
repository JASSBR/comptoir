namespace Comptoir.Api.Endpoints;

/// <summary>
/// Errors shaped exactly as ASP.NET Web API 2 produced them, because the AngularJS screens parse them:
/// BadRequest(string) gave {"message": "…"}, Content(Conflict, string) gave a bare JSON string.
/// </summary>
internal static class LegacyResults
{
    public static IResult BadRequest(string message) => TypedResults.Json(new { message }, statusCode: StatusCodes.Status400BadRequest);

    public static IResult Conflict(string message) => TypedResults.Json(message, statusCode: StatusCodes.Status409Conflict);
}
