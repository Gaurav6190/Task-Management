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

namespace TaskManagement.Application.UnitTests;

/// <summary>Every use case must be registered, otherwise its endpoint fails at runtime.</summary>
[TestFixture]
public class DependencyInjectionTests
{
    private static readonly IServiceCollection Services = new ServiceCollection().AddApplication();

    [TestCase(typeof(ICommandHandler<CreateTaskCommand, Guid>), typeof(CreateTaskCommandHandler))]
    [TestCase(typeof(ICommandHandler<UpdateTaskCommand>), typeof(UpdateTaskCommandHandler))]
    [TestCase(typeof(ICommandHandler<DeleteTaskCommand>), typeof(DeleteTaskCommandHandler))]
    [TestCase(typeof(IQueryHandler<GetTaskByIdQuery, TaskDto>), typeof(GetTaskByIdQueryHandler))]
    [TestCase(typeof(IQueryHandler<ListTasksQuery, PagedResult<TaskDto>>), typeof(ListTasksQueryHandler))]
    [TestCase(typeof(IQueryHandler<GetTaskSummaryQuery, TaskSummaryDto>), typeof(GetTaskSummaryQueryHandler))]
    [TestCase(typeof(ICommandHandler<LoginCommand, LoginResult>), typeof(LoginCommandHandler))]
    public void Handler_is_registered_as_scoped(Type service, Type implementation)
    {
        var descriptor = Services.Single(d => d.ServiceType == service);

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.ImplementationType, Is.EqualTo(implementation));
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        });
    }

    [TestCase(typeof(IValidator<CreateTaskCommand>))]
    [TestCase(typeof(IValidator<UpdateTaskCommand>))]
    [TestCase(typeof(IValidator<ListTasksQuery>))]
    [TestCase(typeof(IValidator<LoginCommand>))]
    public void Validator_is_registered(Type validator) =>
        Assert.That(Services.Any(d => d.ServiceType == validator), Is.True);

    [Test]
    public void TimeProvider_is_the_system_clock() =>
        Assert.That(Services.Single(d => d.ServiceType == typeof(TimeProvider)).ImplementationInstance, Is.SameAs(TimeProvider.System));
}
