using FluentValidation;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Application.Auth.Login;

/// <summary>Credentials sent by the UI. The password is never logged, echoed, or stored.</summary>
public sealed record LoginCommand(string? Username, string? Password)
{
    // Keep the password out of logs / debugger output if the command is ever printed.
    public override string ToString() => $"LoginCommand {{ Username = {Username}, Password = *** }}";
}

public sealed record LoginResult(string AccessToken, string TokenType, DateTime ExpiresAtUtc, string Username);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public const int UsernameMaxLength = 256;
    public const int PasswordMaxLength = 128;

    public LoginCommandValidator()
    {
        RuleFor(x => x.Username)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(UsernameMaxLength).WithMessage($"Username cannot exceed {UsernameMaxLength} characters.")
            .EmailAddress().WithMessage("Username must be a valid email address.");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(PasswordMaxLength).WithMessage($"Password cannot exceed {PasswordMaxLength} characters.");
    }
}

public sealed class LoginCommandHandler(
    IUserAuthenticator authenticator,
    IAccessTokenService tokens,
    IValidator<LoginCommand> validator) : ICommandHandler<LoginCommand, LoginResult>
{
    public const string BearerTokenType = "Bearer";

    public async Task<Result<LoginResult>> Handle(LoginCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return validation.ToError();

        var user = await authenticator.AuthenticateAsync(command.Username!.Trim(), command.Password!, ct);
        if (user is null)
            return AuthErrors.InvalidCredentials;

        var token = tokens.Issue(user);
        return new LoginResult(token.Token, BearerTokenType, token.ExpiresAtUtc, user.Username);
    }
}
