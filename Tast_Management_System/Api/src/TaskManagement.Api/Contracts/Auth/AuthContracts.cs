namespace TaskManagement.Api.Contracts.Auth;

/// <summary>Credentials sent by the UI. Nullable so missing values become field-level validation errors.</summary>
/// <param name="Username">Email address.</param>
public sealed record LoginRequest(string? Username, string? Password)
{
    public override string ToString() => $"LoginRequest {{ Username = {Username}, Password = *** }}";
}

/// <param name="AccessToken">JWT to send as "Authorization: Bearer {AccessToken}" on every API call.</param>
/// <param name="TokenType">Always "Bearer".</param>
/// <param name="ExpiresAtUtc">After this time the API returns 401 and the UI must log in again.</param>
public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc, string Username);
