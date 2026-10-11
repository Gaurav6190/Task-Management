using FluentValidation;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.Tasks;

/// <summary>Field rules shared by the task commands, so Create and Update stay identical.</summary>
internal static class TaskValidationRules
{
    public static void Title<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(TaskItem.TitleMaxLength)
            .WithMessage($"Title cannot exceed {TaskItem.TitleMaxLength} characters.");

    public static void Description<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(TaskItem.DescriptionMaxLength)
            .WithMessage($"Description cannot exceed {TaskItem.DescriptionMaxLength} characters.");

    public static void AssignedTo<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(TaskItem.AssignedToMaxLength)
            .WithMessage($"AssignedTo cannot exceed {TaskItem.AssignedToMaxLength} characters.");
}
