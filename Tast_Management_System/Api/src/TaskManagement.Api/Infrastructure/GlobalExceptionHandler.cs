using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Common;

namespace TaskManagement.Api.Infrastructure;

/// <summary>
/// Turns any unhandled exception into the standard error response.
/// 4xx: the message is safe to show. 5xx: a generic message only; details go to the log with the traceId.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string ServerErrorMessage =
        "An unexpected error occurred. Please contact support with the traceId if the problem persists.";

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {Method} {Path} was cancelled by the client",
                context.Request.Method, context.Request.Path);
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499; // client closed request; nobody is listening for a body
            return true;
        }

        var (status, code, detail) = exception switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, e.Code, e.Message),
            BadHttpRequestException e => (e.StatusCode, ErrorCodes.RequestInvalid, e.Message),
            // Must come before DbUpdateException (it derives from it).
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict,
                "The task was changed by another request. Reload it and try again."),
            DbUpdateException => (StatusCodes.Status500InternalServerError, ErrorCodes.DatabaseError,
                "The change could not be saved. Please try again later."),
            // Raw ADO.NET queries (e.g. the task summary) surface SqlException directly, not via EF.
            SqlException => (StatusCodes.Status500InternalServerError, ErrorCodes.DatabaseError,
                "The database could not be reached or the query failed. Please try again later."),
            _ => (StatusCodes.Status500InternalServerError, ErrorCodes.ServerError, ServerErrorMessage)
        };

        var problem = ApiProblems.Create(context, status, code, detail);

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception {Code} on {Method} {Path} (traceId {TraceId})",
                code, context.Request.Method, context.Request.Path, problem.Extensions["traceId"]);
        else
            logger.LogWarning("Request failed with {Status} {Code} on {Method} {Path}: {Message} (traceId {TraceId})",
                status, code, context.Request.Method, context.Request.Path, exception.Message,
                problem.Extensions["traceId"]);

        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}
