using TaskManagement.Application.Abstractions;

namespace TaskManagement.Application.Tasks;

public static class TaskErrors
{
    public static Error NotFound(Guid id) => Error.NotFound(ErrorCodes.TaskNotFound, $"Task '{id}' was not found.");
}
