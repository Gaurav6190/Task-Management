using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Api.Infrastructure;

public static class ResultExtensions
{
    /// <summary>Maps a failed Result to the standard error response.</summary>
    public static ActionResult ToProblem(this Result result, ControllerBase controller)
    {
        var error = result.Error;
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound   => StatusCodes.Status404NotFound,
            ErrorType.Conflict   => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _                    => StatusCodes.Status500InternalServerError
        };

        var errors = error.ValidationErrors?.ToDictionary(e => e.Key, e => e.Value);
        return ApiProblems.ToResult(
            ApiProblems.Create(controller.HttpContext, status, error.Code, error.Message, errors));
    }
}
