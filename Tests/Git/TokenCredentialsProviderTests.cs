using LibGit2Sharp;
using RackPeek.Domain.Git;

namespace Tests.Git;

public sealed class TokenCredentialsProviderTests {
    [Fact]
    public void Produces_HTTP_Basic_Credentials_With_Configured_Username_And_Token() {
        // Doc: GIT_TOKEN is "used as the password for HTTP Basic auth";
        // GIT_USERNAME is the username the token belongs to. The handler must
        // wire those two into UsernamePasswordCredentials, which libgit2 sends
        // as HTTP Basic over HTTPS.
        var provider = new TokenCredentialsProvider("octocat", "ghp_secret");

        LibGit2Sharp.Handlers.CredentialsHandler handler = provider.GetHandler();
        var credentials = (UsernamePasswordCredentials)handler(
            "https://github.com/foo/bar.git", null!, SupportedCredentialTypes.UsernamePassword);

        Assert.Equal("octocat", credentials.Username);
        Assert.Equal("ghp_secret", credentials.Password);
    }

    [Fact]
    public void Rejects_Null_Username() =>
        Assert.Throws<ArgumentNullException>(() => new TokenCredentialsProvider(null!, "token"));

    [Fact]
    public void Rejects_Null_Token() =>
        Assert.Throws<ArgumentNullException>(() => new TokenCredentialsProvider("user", null!));
}
