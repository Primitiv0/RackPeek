using RackPeek.Domain.Git;
using RackPeek.Domain.Git.UseCases;

namespace Tests.Git;

public sealed class AddRemoteUseCaseTests : IDisposable {
    private readonly string _tempDir;
    private readonly IGitRepository _repo;
    private readonly AddRemoteUseCase _useCase;

    public AddRemoteUseCaseTests() {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "rackpeek-add-remote-tests",
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(_tempDir);

        _repo = new LibGit2GitRepository(
            _tempDir,
            new TokenCredentialsProvider("test", "test-token"));

        _useCase = new AddRemoteUseCase(_repo);
    }

    public void Dispose() {
        try {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch {
            // ignore cleanup issues
        }
    }

    [Theory]
    [InlineData("https://github.com/youruser/rackpeek-config.git")]
    [InlineData("https://gitea.example.com/youruser/rackpeek-config.git")]
    [InlineData("https://gitlab.example.com/youruser/rackpeek-config.git")]
    public async Task Accepts_Documented_HTTPS_Examples(string url) {
        // The doc lists these three URL shapes as supported. Use case must
        // not reject them as malformed — a fetch failure (no real remote) is
        // acceptable but "Only HTTPS URLs are supported" is not.
        var error = await _useCase.ExecuteAsync(url);

        Assert.DoesNotContain("Only HTTPS URLs are supported", error ?? string.Empty);
        Assert.DoesNotContain("Git is not available", error ?? string.Empty);
    }

    [Theory]
    [InlineData("http://github.com/u/r.git")]
    [InlineData("ssh://git@github.com/u/r.git")]
    [InlineData("git@github.com:u/r.git")]
    [InlineData("file:///tmp/repo")]
    public async Task Rejects_Non_HTTPS_URLs(string url) {
        // Doc: "RackPeek does not use any host-specific APIs; the integration
        // is plain git over HTTPS." Only HTTPS is supported on purpose — SSH
        // would need a different credentials flow we don't offer.
        var error = await _useCase.ExecuteAsync(url);

        Assert.Equal("Only HTTPS URLs are supported.", error);
    }

    [Fact]
    public async Task Rejects_Empty_URL() {
        var error = await _useCase.ExecuteAsync("   ");
        Assert.Equal("URL is required.", error);
    }

    [Fact]
    public async Task Rejects_When_Remote_Already_Configured() {
        await _useCase.ExecuteAsync("https://example.com/first.git");

        var error = await _useCase.ExecuteAsync("https://example.com/second.git");

        Assert.Equal("Remote already configured.", error);
    }
}
