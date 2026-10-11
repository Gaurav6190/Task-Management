namespace TaskManagement.Api.Contracts.Tasks;

// All fields are nullable and enums are strings on purpose: a missing or invalid value must
// reach the validator and come back as a field-level "Validation.Failed" error,
// instead of failing during JSON binding with a framework message.

/// <param name="Priority">One of: Low, Medium, High, Critical.</param>
public sealed record CreateTaskRequest(
    string? Title, string? Description, string? Priority, string? AssignedTo, DateTime? DueDateUtc);

/// <param name="Status">One of: ToDo, InProgress, Done.</param>
/// <param name="Priority">One of: Low, Medium, High, Critical.</param>
public sealed record UpdateTaskRequest(
    string? Title, string? Description, string? Status, string? Priority,
    string? AssignedTo, DateTime? DueDateUtc);

public sealed record CreatedResponse(Guid Id);
