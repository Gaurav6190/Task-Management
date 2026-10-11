using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Auth.Login;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.CreateTask;
using TaskManagement.Application.Tasks.DeleteTask;
using TaskManagement.Application.Tasks.GetTaskById;
using TaskManagement.Application.Tasks.GetTaskSummary;
using TaskManagement.Application.Tasks.ListTasks;
using TaskManagement.Application.Tasks.UpdateTask;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton(TimeProvider.System);

        // Register every handler explicitly; add new use cases here.
        services.AddScoped<ICommandHandler<CreateTaskCommand, Guid>, CreateTaskCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateTaskCommand>, UpdateTaskCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteTaskCommand>, DeleteTaskCommandHandler>();
        services.AddScoped<IQueryHandler<GetTaskByIdQuery, TaskDto>, GetTaskByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListTasksQuery, PagedResult<TaskDto>>, ListTasksQueryHandler>();
        services.AddScoped<IQueryHandler<GetTaskSummaryQuery, TaskSummaryDto>, GetTaskSummaryQueryHandler>();
        services.AddScoped<ICommandHandler<LoginCommand, LoginResult>, LoginCommandHandler>();

        return services;
    }
}
