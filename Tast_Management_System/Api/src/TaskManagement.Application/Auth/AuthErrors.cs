using TaskManagement.Application.Abstractions;

namespace TaskManagement.Application.Auth;

public static class AuthErrors
{
    /// <summary>Same error for unknown username and wrong password: never reveal which usernames exist.</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized(ErrorCodes.InvalidCredentials, "Invalid username or password.");
}
