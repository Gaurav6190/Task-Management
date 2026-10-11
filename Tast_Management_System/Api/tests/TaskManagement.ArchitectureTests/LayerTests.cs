using NetArchTest.Rules;

namespace TaskManagement.ArchitectureTests;

/// <summary>Enforces the dependency rule from the skill: Domain ← Application ← Infrastructure / Api.</summary>
[TestFixture]
public class LayerTests
{
    private const string Application = "TaskManagement.Application";
    private const string Infrastructure = "TaskManagement.Infrastructure";
    private const string Api = "TaskManagement.Api";

    private static readonly System.Reflection.Assembly DomainAssembly = typeof(TaskManagement.Domain.Tasks.TaskItem).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(TaskManagement.Application.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(TaskManagement.Infrastructure.DependencyInjection).Assembly;

    [Test]
    public void Domain_has_no_outward_dependencies()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOnAny(Application, Infrastructure, Api, "Microsoft.EntityFrameworkCore", "FluentValidation")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, Failing(result));
    }

    [Test]
    public void Application_does_not_depend_on_infrastructure_api_or_technology_packages()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOnAny(
                Infrastructure, Api,
                "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient",
                "Microsoft.IdentityModel", "System.IdentityModel", "Microsoft.AspNetCore")
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, Failing(result));
    }

    [Test]
    public void Infrastructure_does_not_depend_on_api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(Api)
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, Failing(result));
    }

    [Test]
    public void Controllers_do_not_use_infrastructure_directly()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That().ResideInNamespace("TaskManagement.Api.Controllers")
            .ShouldNot().HaveDependencyOn(Infrastructure)
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True, Failing(result));
    }

    private static string Failing(TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
