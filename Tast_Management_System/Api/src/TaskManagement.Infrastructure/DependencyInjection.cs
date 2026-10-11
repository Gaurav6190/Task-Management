using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Auth;
using TaskManagement.Infrastructure.Auth;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.GetTaskSummary;
using TaskManagement.Domain.Tasks;
using TaskManagement.Infrastructure.DomainEvents;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Persistence.Queries;
using TaskManagement.Infrastructure.Persistence.Repositories;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<ITaskQueries, TaskQueries>();

        // Raw SQL (ADO.NET) read side, bypassing EF Core.
        services.AddSingleton(new SqlConnectionFactory(connectionString));
        services.AddScoped<ITaskSummaryQuery, TaskSummaryQuery>();

        AddAuth(services, configuration);

        return services;
    }

    private static void AddAuth(IServiceCollection services, IConfiguration configuration)
    {
        // Options are bound lazily (not read here) so tests and environments can override them.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 1440, "Jwt:AccessTokenMinutes must be between 1 and 1440.")
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey ?? string.Empty) >= JwtOptions.MinimumKeyBytes,
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumKeyBytes} bytes. Set it with dotnet user-secrets " +
                "(scripts/Set-DevAuthUser.ps1) or the Jwt__SigningKey environment variable.")
            .ValidateOnStart();

        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName));

        services.AddSingleton<IUserAuthenticator, ConfiguredUserAuthenticator>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
    }
}
