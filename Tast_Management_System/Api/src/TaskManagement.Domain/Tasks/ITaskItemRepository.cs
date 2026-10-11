namespace TaskManagement.Domain.Tasks;

/// <summary>
/// Tasks are never removed from the database; use <see cref="TaskItem.Delete"/> (soft delete).
/// </summary>
public interface ITaskItemRepository
{
    Task<TaskItem?> GetByIdAsync(TaskItemId id, CancellationToken ct = default);
    void Add(TaskItem task);
}
