using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks.GetTaskById;

public sealed record GetTaskByIdQuery(Guid Id);

public sealed class GetTaskByIdQueryHandler(ITaskItemRepository repository)
    : IQueryHandler<GetTaskByIdQuery, TaskDto>
{
    public async Task<Result<TaskDto>> Handle(GetTaskByIdQuery query, CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(new TaskItemId(query.Id), ct);
        return task is null
            ? TaskErrors.NotFound(query.Id)
            : TaskDto.FromDomain(task);
    }
}
