using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Auth.Login;

namespace TaskManagement.Application.UnitTests.Auth;

[TestFixture]
public class LoginTests
{
    private const string User = "tester@example.com";
    private const string Password = "S3cret-for-unit-tests";

    private FakeAuthenticator _authenticator = null!;
    private FakeTokenService _tokens = null!;

    [SetUp]
    public void SetUp()
    {
        _authenticator = new FakeAuthenticator(User, Password);
        _tokens = new FakeTokenService();
    }

    private Task<Result<LoginResult>> Login(string? username, string? password) =>
        new LoginCommandHandler(_authenticator, _tokens, new LoginCommandValidator())
            .Handle(new LoginCommand(username, password), CancellationToken.None);

    // ---------- Handler: positive ----------

    [Test]
    public async Task Valid_credentials_return_a_bearer_token_for_the_user()
    {
        var result = await Login(User, Password);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new LoginResult($"token-for-{User}", "Bearer", FakeTokenService.Expires, User)));
        Assert.That(_tokens.Issued, Is.EqualTo(new[] { new AuthenticatedUser(User) }));
    }

    [Test]
    public async Task Username_is_trimmed_and_matched_case_insensitively()
    {
        var result = await Login("  TESTER@example.com  ", Password);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Username, Is.EqualTo(User));
            Assert.That(_authenticator.Calls.Single().Username, Is.EqualTo("TESTER@example.com"));
        });
    }

    // ---------- Handler: negative ----------

    [Test]
    public async Task Wrong_password_returns_invalid_credentials_and_issues_no_token()
    {
        var result = await Login(User, "wrong");

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.EqualTo(AuthErrors.InvalidCredentials));
            Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Unauthorized));
            Assert.That(_tokens.Issued, Is.Empty);
        });
    }

    [Test]
    public async Task Unknown_user_returns_the_same_error_as_a_wrong_password()
    {
        var unknown = await Login("nobody@example.com", Password);
        var wrongPassword = await Login(User, "wrong");

        Assert.That(unknown.Error, Is.EqualTo(wrongPassword.Error));
    }

    [Test]
    public async Task Invalid_input_is_rejected_before_checking_credentials()
    {
        var result = await Login("not-an-email", "");

        Assert.Multiple(() =>
        {
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
            Assert.That(result.Error.ValidationErrors!.Keys, Is.EquivalentTo(new[] { "Username", "Password" }));
            Assert.That(_authenticator.Calls, Is.Empty);
        });
    }

    [Test]
    public void Command_ToString_never_contains_the_password() =>
        Assert.That(new LoginCommand(User, Password).ToString(), Does.Not.Contain(Password).And.Contain("***"));

    // ---------- Validator ----------

    private static readonly LoginCommandValidator Validator = new();

    [Test]
    public void Validator_accepts_an_email_and_a_password() =>
        Assert.That(Validator.Validate(new LoginCommand(User, "x")).IsValid, Is.True);

    [TestCase(null, "Username is required.")]
    [TestCase("", "Username is required.")]
    [TestCase("   ", "Username is required.")]
    [TestCase("not-an-email", "Username must be a valid email address.")]
    [TestCase("missing-at.example.com", "Username must be a valid email address.")]
    public void Validator_rejects_missing_or_non_email_usernames(string? username, string message)
    {
        var errors = Validator.Validate(new LoginCommand(username, "x")).Errors;

        Assert.That(errors.Select(e => (e.PropertyName, e.ErrorMessage)), Is.EqualTo(new[] { ("Username", message) }));
    }

    [Test]
    public void Validator_rejects_usernames_longer_than_256()
    {
        var username = new string('a', 250) + "@x.com"; // 256 is fine, 257 is not
        Assert.Multiple(() =>
        {
            Assert.That(Validator.Validate(new LoginCommand(username, "x")).IsValid, Is.True);
            Assert.That(Validator.Validate(new LoginCommand("a" + username, "x")).Errors.Single().ErrorMessage,
                Is.EqualTo("Username cannot exceed 256 characters."));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void Validator_requires_a_password(string? password) =>
        Assert.That(Validator.Validate(new LoginCommand(User, password)).Errors.Single().ErrorMessage,
            Is.EqualTo("Password is required."));

    [Test]
    public void Validator_rejects_passwords_longer_than_128_without_echoing_them()
    {
        var longPassword = new string('p', 129);
        var error = Validator.Validate(new LoginCommand(User, longPassword)).Errors.Single();

        Assert.Multiple(() =>
        {
            Assert.That(error.ErrorMessage, Is.EqualTo("Password cannot exceed 128 characters."));
            Assert.That(error.ErrorMessage, Does.Not.Contain(longPassword));
            Assert.That(Validator.Validate(new LoginCommand(User, new string('p', 128))).IsValid, Is.True);
        });
    }
}
