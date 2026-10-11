using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Api.Infrastructure;

/// <summary>
/// The single place that shapes error responses. Every error, whether from a handler Result,
/// model binding, an exception, or an empty 404/405, goes through here, so all of them have:
/// type, title, status, detail, instance, code, traceId (+ errors for validation).
/// </summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";

    public static ProblemDetails Create(
        HttpContext context, int status, string code, string? detail,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = errors is null ? new ProblemDetails() : new ValidationProblemDetails(errors);
        problem.Status = status;
        problem.Title = ReasonPhrases.GetReasonPhrase(status);
        problem.Detail = detail;
        problem.Extensions["code"] = code;
        Enrich(problem, context);
        return problem;
    }

    public static ObjectResult ToResult(ProblemDetails problem) =>
        new(problem) { StatusCode = problem.Status, ContentTypes = { ContentType } };

    /// <summary>Fills in any missing standard fields. Safe to call more than once.</summary>
    public static void Enrich(ProblemDetails problem, HttpContext context)
    {
        var status = problem.Status ?? context.Response.StatusCode;
        problem.Status = status;
        problem.Title ??= ReasonPhrases.GetReasonPhrase(status);
        problem.Type ??= TypeFor(status);
        problem.Instance ??= context.Request.Path;

        if (!problem.Extensions.ContainsKey("code"))
            problem.Extensions["code"] = DefaultCodeFor(status);
        if (problem.Detail is null && Equals(problem.Extensions["code"], ErrorCodes.Unauthorized))
            problem.Detail = "A valid access token is required. Log in via POST /api/auth/login.";
        if (!problem.Extensions.ContainsKey("traceId"))
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
    }

    /// <summary>Codes for errors produced by the framework itself (no handler involved).</summary>
    private static string DefaultCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.RequestInvalid,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.RouteNotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        StatusCodes.Status409Conflict => ErrorCodes.ConcurrencyConflict,
        StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
        >= 500 => ErrorCodes.ServerError,
        _ => ErrorCodes.RequestInvalid
    };

    private static string? TypeFor(int status) => status switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        405 => "https://tools.ietf.org/html/rfc9110#section-15.5.6",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        415 => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
        500 => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        _ => null
    };
}
