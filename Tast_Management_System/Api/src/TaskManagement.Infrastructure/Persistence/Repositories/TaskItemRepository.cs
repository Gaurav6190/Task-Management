using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Infrastructure.Persistence.Repositories;

internal sealed class TaskItemRepository(AppDbContext db) : ITaskItemRepository
{
    public Task<TaskItem?> GetByIdAsync(TaskItemId id, CancellationToken ct = default) =>
        db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public void Add(TaskItem task) => db.Tasks.Add(task);
}
