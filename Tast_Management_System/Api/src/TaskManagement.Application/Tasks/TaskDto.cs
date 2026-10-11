using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks;

public sealed record TaskDto(
    Guid Id, string Title, string? Description, string Status, string Priority, string? AssignedTo,
    DateTime? DueDateUtc, DateTime CreatedDate, DateTime? ModifiedDate, DateTime? CompletedAtUtc)
{
    public static TaskDto FromDomain(TaskItem t) => new(
        t.Id.Value, t.Title, t.Description, t.Status.ToString(), t.Priority.ToString(), t.AssignedTo,
        t.DueDateUtc, t.CreatedDate, t.ModifiedDate, t.CompletedAtUtc);
}
