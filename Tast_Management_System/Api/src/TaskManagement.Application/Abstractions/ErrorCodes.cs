using TaskManagement.Domain.Common;

namespace TaskManagement.Application.Abstractions;

/// <summary>
/// Catalogue of every error code the API returns in the "code" field of an error response.
/// Format: "Area.Reason". Clients switch on these, so never rename an existing code.
/// </summary>
public static class ErrorCodes
{
    // 400
    public const string ValidationFailed = "Validation.Failed";
    public const string RequestInvalid = "Request.Invalid";
    public const string DomainRuleViolated = DomainException.DefaultCode;

    // 401 / 403
    public const string InvalidCredentials = "Auth.InvalidCredentials";
    public const string Unauthorized = "Auth.Unauthorized";
    public const string Forbidden = "Auth.Forbidden";

    // 404 / 405 / 415
    public const string TaskNotFound = "Task.NotFound";
    public const string RouteNotFound = "Route.NotFound";
    public const string MethodNotAllowed = "Method.NotAllowed";
    public const string UnsupportedMediaType = "Request.UnsupportedMediaType";

    // 409
    public const string ConcurrencyConflict = "Concurrency.Conflict";

    // 500
    public const string DatabaseError = "Database.Error";
    public const string ServerError = "Server.Error";
}
