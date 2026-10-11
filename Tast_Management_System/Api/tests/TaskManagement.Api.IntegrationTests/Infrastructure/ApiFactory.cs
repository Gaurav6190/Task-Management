using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Infrastructure.Auth;

namespace TaskManagement.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API (real local SQL Server, Development environment) with test-only auth settings:
/// a random JWT signing key and a throwaway user with a random password, generated per test run.
/// No real credentials are needed or stored (task.md D16).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "TaskManagement.Api";
    public const string Audience = "TaskManagement.Ui";

    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string Username { get; } = "it-user@integration.test";
    public string Password { get; } = $"It-{Guid.NewGuid():N}";

    /// <summary>Every log message the app writes, for "never log the password" checks.</summary>
    public CollectingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var hash = new PasswordHasher<ConfiguredUser>().HashPassword(new ConfiguredUser(), Password);

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = SigningKey,
            ["Auth:Users:0:Username"] = Username,       // replaces any developer user-secrets slot 0
            ["Auth:Users:0:PasswordHash"] = hash
        }));
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }

    /// <summary>Creates a client that sends the given access token on every request.</summary>
    public static HttpClient WithToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Builds tokens for negative tests (expired, wrong key/issuer/audience, unsigned).</summary>
    public string CreateToken(
        string? issuer = Issuer, string? audience = Audience, string? signingKey = null,
        DateTime? notBefore = null, DateTime? expires = null, bool signed = true)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = notBefore ?? now,
            NotBefore = notBefore ?? now,
            Expires = expires ?? now.AddMinutes(60),
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, Username)]),
            SigningCredentials = signed
                ? new SigningCredentials(JwtSigningKey.Create(signingKey ?? SigningKey), SecurityAlgorithms.HmacSha256)
                : null
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

public sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(_messages);

    public void Dispose() { }

    private sealed class CollectingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue($"{logLevel}: {formatter(state, exception)} {exception}");
    }
}
