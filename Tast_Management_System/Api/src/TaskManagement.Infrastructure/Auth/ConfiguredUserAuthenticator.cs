using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Auth;

namespace TaskManagement.Infrastructure.Auth;

/// <summary>
/// Verifies credentials against the users in <see cref="AuthOptions"/> using ASP.NET Core Identity's
/// PBKDF2 <see cref="PasswordHasher{TUser}"/> (constant-time comparison).
/// Usernames are compared case-insensitively. Unknown usernames still run a hash verification,
/// so response time doesn't reveal whether a username exists.
/// </summary>
internal sealed class ConfiguredUserAuthenticator(
    IOptionsMonitor<AuthOptions> options, ILogger<ConfiguredUserAuthenticator> logger) : IUserAuthenticator
{
    private static readonly PasswordHasher<ConfiguredUser> Hasher = new();

    // Hash of a random value, verified when the username is unknown to equalise timing.
    private static readonly string DummyHash = Hasher.HashPassword(new ConfiguredUser(), Guid.NewGuid().ToString());

    public Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        var user = options.CurrentValue.Users.FirstOrDefault(
            u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            Verify(new ConfiguredUser(), DummyHash, password);
            logger.LogWarning("Login failed for {Username}: unknown user", username);
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        if (!Verify(user, user.PasswordHash, password))
        {
            logger.LogWarning("Login failed for {Username}: wrong password", username);
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        logger.LogInformation("Login succeeded for {Username}", user.Username);
        return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser(user.Username));
    }

    private bool Verify(ConfiguredUser user, string hash, string password)
    {
        try
        {
            return Hasher.VerifyHashedPassword(user, hash, password) is
                PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // A broken hash in config is a configuration problem, not a server error for the caller.
            logger.LogError("The configured password hash for {Username} is not a valid Identity hash", user.Username);
            return false;
        }
    }
}
