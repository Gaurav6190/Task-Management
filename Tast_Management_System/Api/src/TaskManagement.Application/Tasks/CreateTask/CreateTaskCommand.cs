using FluentValidation;
using TaskManagement.Application.Abstractions;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks.CreateTask;

/// <summary>
/// Fields are nullable strings on purpose: missing or invalid values must reach the validator
/// (and come back as a field-level validation error) instead of failing during JSON binding.
/// </summary>
public sealed record CreateTaskCommand(
    string? Title, string? Description, string? Priority, string? AssignedTo, DateTime? DueDateUtc);

public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Title).Title();
        RuleFor(x => x.Description).Description();
        RuleFor(x => x.AssignedTo).AssignedTo();

        RuleFor(x => x.Priority)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Priority is required.")
            .MustBeEnumName<CreateTaskCommand, TaskPriority>();

        RuleFor(x => x.DueDateUtc)
            .Must(d => d is null || d.Value >= timeProvider.GetUtcNow().UtcDateTime.Date)
            .WithMessage("DueDateUtc cannot be in the past.");
    }
}

public sealed class CreateTaskCommandHandler(
    ITaskItemRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<CreateTaskCommand> validator,
    TimeProvider timeProvider) : ICommandHandler<CreateTaskCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTaskCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return validation.ToError();

        var task = TaskItem.Create(
            command.Title!, command.Description, EnumNames.Parse<TaskPriority>(command.Priority!),
            command.AssignedTo, command.DueDateUtc, timeProvider.GetUtcNow().UtcDateTime);

        repository.Add(task);
        await unitOfWork.SaveChangesAsync(ct);

        return task.Id.Value;
    }
}
