namespace TaskManagement.Infrastructure.Auth;

/// <summary>
/// Users allowed to log in, from configuration section "Auth".
/// TEMPORARY test users only. They are set per developer with dotnet user-secrets
/// (scripts/Set-DevAuthUser.ps1) and never committed. Outside Development the list is cleared
/// at startup (see AuthSetup), so a test login can't work on a deployed server - unless
/// <see cref="AllowConfiguredUsersOutsideDevelopment"/> is set (localhost-only Docker deployment).
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public List<ConfiguredUser> Users { get; set; } = [];

    /// <summary>
    /// Explicit opt-in to keep <see cref="Users"/> outside Development. Off by default; only for a
    /// deployment that is not reachable from other machines (e.g. docker compose on localhost).
    /// </summary>
    public bool AllowConfiguredUsersOutsideDevelopment { get; set; }
}

public sealed class ConfiguredUser
{
    public string Username { get; set; } = string.Empty;

    /// <summary>ASP.NET Core Identity v3 hash (PBKDF2). Never a plaintext password.</summary>
    public string PasswordHash { get; set; } = string.Empty;
}

/// <summary>
/// JWT settings, from configuration section "Jwt" (validated at startup in AddInfrastructure).
/// The signing key comes from user-secrets / env vars.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32; // HMAC-SHA256 needs at least 256 bits

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>Secret, at least 32 bytes (UTF-8). Never in appsettings*.json.</summary>
    public string SigningKey { get; set; } = string.Empty;
}
