namespace TaskManagement.Domain.Common;

/// <summary>
/// Thrown when a business rule is violated. Returned to clients as 400 with <see cref="Code"/>.
/// Pass a specific code (e.g. "Task.AlreadyDeleted") when clients need to tell rules apart.
/// </summary>
public sealed class DomainException(string message, string code = DomainException.DefaultCode) : Exception(message)
{
    public const string DefaultCode = "Domain.RuleViolated";

    public string Code { get; } = code;
}
