using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Api.Infrastructure;

/// <summary>
/// Replaces ASP.NET's default model-binding 400 so it uses the same error format as validation
/// failures, with field names in C# style ("Title") and no framework or parser internals.
/// </summary>
public static class ModelStateProblems
{
    private const string InvalidJsonValue = "The value is not valid JSON or has the wrong type.";

    public static IActionResult Create(ActionContext context)
    {
        var bodyParameters = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entries = context.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 })
            .ToList();

        var hasJsonError = entries.Any(e => e.Key.StartsWith('$'));
        var errors = new Dictionary<string, string[]>();

        foreach (var (key, entry) in entries)
        {
            if (key.StartsWith('$'))
            {
                var field = ToFieldName(key.TrimStart('$').TrimStart('.'));
                errors[field.Length == 0 ? "Body" : field] = [InvalidJsonValue];
            }
            else if (bodyParameters.Contains(key))
            {
                // "The request field is required." is noise when the JSON itself was broken.
                if (!hasJsonError)
                    errors["Body"] = ["A request body is required."];
            }
            else
            {
                errors[ToFieldName(key)] = entry!.Errors
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "The value is not valid." : e.ErrorMessage)
                    .ToArray();
            }
        }

        var (code, detail) = hasJsonError || errors.ContainsKey("Body")
            ? (ErrorCodes.RequestInvalid, "The request body is not valid JSON or a field has the wrong type.")
            : (ErrorCodes.ValidationFailed, ValidationExtensions.ValidationMessage);

        return ApiProblems.ToResult(ApiProblems.Create(
            context.HttpContext, StatusCodes.Status400BadRequest, code, detail, errors));
    }

    /// <summary>"title" -> "Title", "items[0].name" -> "Items[0].Name".</summary>
    private static string ToFieldName(string key) =>
        string.Join('.', key.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
