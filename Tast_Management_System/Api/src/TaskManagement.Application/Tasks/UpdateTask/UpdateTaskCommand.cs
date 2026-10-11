using FluentValidation;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks.UpdateTask;

/// <summary>Nullable string fields on purpose; see <see cref="CreateTask.CreateTaskCommand"/>.</summary>
public sealed record UpdateTaskCommand(
    Guid Id, string? Title, string? Description, string? Status, string? Priority,
    string? AssignedTo, DateTime? DueDateUtc);

public sealed class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");
        RuleFor(x => x.Title).Title();
        RuleFor(x => x.Description).Description();
        RuleFor(x => x.AssignedTo).AssignedTo();

        RuleFor(x => x.Status)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Status is required.")
            .MustBeEnumName<UpdateTaskCommand, TaskItemStatus>();

        RuleFor(x => x.Priority)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Priority is required.")
            .MustBeEnumName<UpdateTaskCommand, TaskPriority>();

        // No "due date in the past" rule here: an overdue task must stay editable without
        // moving its due date. The domain rejects only a *changed* due date in the past.
    }
}

public sealed class UpdateTaskCommandHandler(
    ITaskItemRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<UpdateTaskCommand> validator,
    TimeProvider timeProvider) : ICommandHandler<UpdateTaskCommand>
{
    public async Task<Result> Handle(UpdateTaskCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return validation.ToError();

        var task = await repository.GetByIdAsync(new TaskItemId(command.Id), ct);
        if (task is null)
            return TaskErrors.NotFound(command.Id);

        task.Update(
            command.Title!, command.Description,
            EnumNames.Parse<TaskItemStatus>(command.Status!), EnumNames.Parse<TaskPriority>(command.Priority!),
            command.AssignedTo, command.DueDateUtc, timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
