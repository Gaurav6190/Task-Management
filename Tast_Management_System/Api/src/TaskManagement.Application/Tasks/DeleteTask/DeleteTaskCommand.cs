using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks.DeleteTask;

/// <summary>Soft delete: marks the task as deleted; the row stays in the database.</summary>
public sealed record DeleteTaskCommand(Guid Id);

public sealed class DeleteTaskCommandHandler(
    ITaskItemRepository repository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<DeleteTaskCommand>
{
    public async Task<Result> Handle(DeleteTaskCommand command, CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(new TaskItemId(command.Id), ct);
        if (task is null)
            return TaskErrors.NotFound(command.Id);

        task.Delete(timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
