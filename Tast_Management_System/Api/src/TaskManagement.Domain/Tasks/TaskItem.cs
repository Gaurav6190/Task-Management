using TaskManagement.Domain.Common;
using TaskManagement.Domain.Tasks.Events;

namespace TaskManagement.Domain.Tasks;

public sealed class TaskItem : AggregateRoot<TaskItemId>
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int AssignedToMaxLength = 100;

    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public string? AssignedTo { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime CreatedDate { get; private set; }
    public DateTime? ModifiedDate { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedDate { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private TaskItem() { } // EF Core

    public static TaskItem Create(
        string title, string? description, TaskPriority priority, string? assignedTo,
        DateTime? dueDateUtc, DateTime nowUtc)
    {
        var task = new TaskItem
        {
            Id = TaskItemId.New(),
            Status = TaskItemStatus.ToDo,
            CreatedDate = nowUtc
        };
        task.SetDetails(title, description, priority, assignedTo, dueDateUtc, nowUtc);
        task.Raise(new TaskItemCreated(task.Id, nowUtc));
        return task;
    }

    public void Update(
        string title, string? description, TaskItemStatus status, TaskPriority priority,
        string? assignedTo, DateTime? dueDateUtc, DateTime nowUtc)
    {
        EnsureNotDeleted();
        SetDetails(title, description, priority, assignedTo, dueDateUtc, nowUtc);
        SetStatus(status, nowUtc);
        ModifiedDate = nowUtc;
    }

    public void Delete(DateTime nowUtc)
    {
        EnsureNotDeleted();
        IsDeleted = true;
        DeletedDate = nowUtc;
        ModifiedDate = nowUtc;
        Raise(new TaskItemDeleted(Id, nowUtc));
    }

    private void SetStatus(TaskItemStatus status, DateTime nowUtc)
    {
        if (!Enum.IsDefined(status))
            throw new DomainException($"'{status}' is not a valid status.");
        if (status == Status)
            return;

        Status = status;
        if (status == TaskItemStatus.Done)
        {
            CompletedAtUtc = nowUtc;
            Raise(new TaskItemCompleted(Id, nowUtc));
        }
        else
        {
            CompletedAtUtc = null;
        }
    }

    private void SetDetails(
        string title, string? description, TaskPriority priority, string? assignedTo,
        DateTime? dueDateUtc, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Title is required.");
        if (title.Length > TitleMaxLength)
            throw new DomainException($"Title cannot exceed {TitleMaxLength} characters.");
        if (description?.Length > DescriptionMaxLength)
            throw new DomainException($"Description cannot exceed {DescriptionMaxLength} characters.");
        if (assignedTo?.Length > AssignedToMaxLength)
            throw new DomainException($"AssignedTo cannot exceed {AssignedToMaxLength} characters.");
        if (!Enum.IsDefined(priority))
            throw new DomainException($"'{priority}' is not a valid priority.");
        // Only validate a due date that is being set or changed, so an existing
        // overdue task can still be edited without moving its due date.
        if (dueDateUtc is not null && dueDateUtc != DueDateUtc && dueDateUtc < nowUtc.Date)
            throw new DomainException("Due date cannot be in the past.");

        Title = title.Trim();
        Description = description?.Trim();
        Priority = priority;
        AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo.Trim();
        DueDateUtc = dueDateUtc;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("A deleted task cannot be modified.");
    }
}
