using FluentValidation;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks.ListTasks;

/// <summary>Status/Priority are strings so invalid values become field-level validation errors.</summary>
public sealed record ListTasksQuery(string? Status, string? Priority, int Page = 1, int PageSize = 20);

public sealed class ListTasksQueryValidator : AbstractValidator<ListTasksQuery>
{
    public const int MaxPageSize = 100;

    public ListTasksQueryValidator()
    {
        RuleFor(x => x.Status).MustBeEnumName<ListTasksQuery, TaskItemStatus>();
        RuleFor(x => x.Priority).MustBeEnumName<ListTasksQuery, TaskPriority>();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, MaxPageSize)
            .WithMessage($"PageSize must be between 1 and {MaxPageSize}.");
    }
}

public sealed class ListTasksQueryHandler(ITaskQueries queries, IValidator<ListTasksQuery> validator)
    : IQueryHandler<ListTasksQuery, PagedResult<TaskDto>>
{
    public async Task<Result<PagedResult<TaskDto>>> Handle(ListTasksQuery query, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid)
            return validation.ToError();

        return await queries.ListAsync(
            EnumNames.ParseOptional<TaskItemStatus>(query.Status),
            EnumNames.ParseOptional<TaskPriority>(query.Priority),
            query.Page, query.PageSize, ct);
    }
}
