using FluentValidation;
using FluentValidation.Results;

namespace TaskManagement.Application.Abstractions;

public static class ValidationExtensions
{
    public const string ValidationMessage = "One or more fields are invalid.";

    /// <summary>Converts FluentValidation failures into an Error with per-field messages.</summary>
    public static Error ToError(this ValidationResult result) =>
        Error.Validation(ValidationMessage, result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));

    /// <summary>
    /// Enum values arrive as strings and must match a member name (case-insensitive).
    /// Numbers ("2") and flag combinations ("Low, High") are rejected on purpose.
    /// A blank value counts as "not provided" (matching <see cref="EnumNames.ParseOptional{TEnum}"/>);
    /// combine with NotEmpty() when the field is required.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MustBeEnumName<T, TEnum>(this IRuleBuilder<T, string?> rule)
        where TEnum : struct, Enum =>
        rule.Must(value => string.IsNullOrWhiteSpace(value) || EnumNames.IsValid<TEnum>(value))
            .WithMessage($"{{PropertyName}} must be one of: {EnumNames.List<TEnum>()}.");
}

public static class EnumNames
{
    public static bool IsValid<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.GetNames<TEnum>().Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string List<TEnum>() where TEnum : struct, Enum => string.Join(", ", Enum.GetNames<TEnum>());

    /// <summary>Parses a value that has already passed <see cref="IsValid{TEnum}"/>.</summary>
    public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value.Trim(), ignoreCase: true);

    public static TEnum? ParseOptional<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Parse<TEnum>(value);
}
