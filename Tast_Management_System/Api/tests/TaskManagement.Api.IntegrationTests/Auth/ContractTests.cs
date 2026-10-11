using TaskManagement.Api.Contracts.Auth;

namespace TaskManagement.Api.IntegrationTests.Auth;

[TestFixture]
public class ContractTests
{
    [Test]
    public void LoginRequest_ToString_never_contains_the_password()
    {
        var text = new LoginRequest("someone@example.com", "SuperSecret!").ToString();

        Assert.That(text, Does.Contain("someone@example.com").And.Contain("***").And.Not.Contain("SuperSecret!"));
    }
}
