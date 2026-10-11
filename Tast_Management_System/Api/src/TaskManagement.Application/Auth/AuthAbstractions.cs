namespace TaskManagement.Application.Auth;

/// <summary>A user whose credentials have been verified.</summary>
public sealed record AuthenticatedUser(string Username);

/// <summary>An issued access token (JWT) and when it stops being valid.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Checks a username/password pair. Returns null for an unknown user AND for a wrong password,
/// so callers can't tell the two apart. The current implementation reads users from
/// configuration (user-secrets, Development only); a real user store can replace it later.
/// </summary>
public interface IUserAuthenticator
{
    Task<AuthenticatedUser?> AuthenticateAsync(string username, string password, CancellationToken ct);
}

/// <summary>Issues access tokens for authenticated users.</summary>
public interface IAccessTokenService
{
    AccessToken Issue(AuthenticatedUser user);
}
