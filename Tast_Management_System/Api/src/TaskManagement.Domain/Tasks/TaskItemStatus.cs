namespace TaskManagement.Domain.Tasks;

/// <summary>Workflow state of a task. Sent and received by name ("ToDo"), never by number.</summary>
public enum TaskItemStatus
{
    /// <summary>Not started yet (default for new tasks).</summary>
    ToDo = 0,
    /// <summary>Someone is working on it.</summary>
    InProgress = 1,
    /// <summary>Finished; CompletedAtUtc is set.</summary>
    Done = 2
}
