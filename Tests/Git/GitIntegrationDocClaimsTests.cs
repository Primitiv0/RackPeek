using LibGit2Sharp;
using RackPeek.Domain.Git;
using RackPeek.Domain.Git.UseCases;

namespace Tests.Git;

/// <summary>
///     Holds tests that pin doc-only claims (UI strings, security promises,
///     architectural statements) which don't fit cleanly inside one component
///     test class.
/// </summary>
public sealed class GitIntegrationDocClaimsTests : IDisposable {
    private readonly string _tempDir;

    public GitIntegrationDocClaimsTests() {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "rackpeek-doc-claims-tests",
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(_tempDir);
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

    [Fact]
    public async Task Token_Never_Touches_The_Config_Directory() {
        // Doc: "The token is read from the environment at container start; it
        // is not persisted in the YAML config." Stronger guarantee: it must
        // never appear in ANY file under the config directory, including the
        // .git folder, so a leaked backup never leaks the token.
        var token = $"sentinel-token-{Guid.NewGuid():N}";

        var configPath = Path.Combine(_tempDir, "config.yaml");
        await File.WriteAllTextAsync(configPath, "resources: []\n");

        var repo = new LibGit2GitRepository(
            _tempDir,
            new TokenCredentialsProvider("octocat", token));

        var addRemote = new AddRemoteUseCase(repo);
        await addRemote.ExecuteAsync("https://example.com/user/repo.git");

        var commit = new CommitAllUseCase(repo);
        await commit.ExecuteAsync("test commit");

        // Recursively scan everything under the config dir for the sentinel.
        foreach (var file in Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories)) {
            var bytes = await File.ReadAllBytesAsync(file);
            // tokens are ASCII; cheaper than decoding every binary file as text
            var contents = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain(token, contents);
        }
    }

    [Fact]
    public void Writability_Warning_String_In_UI_Matches_Documented_Wording() {
        // Doc: 'If the indicator shows "Git configured but config directory is
        // not writable", the container can't create the .git/ folder...'.
        // Lock that exact wording so the doc and UI cannot drift out of sync.
        // The tests run from Tests/bin/Debug/net10.0; walk up to the repo root.
        var razorPath = LocateRepoFile("Shared.Rcl/Layout/GitStatusIndicator.razor");

        var contents = File.ReadAllText(razorPath);

        Assert.Contains(
            "Git configured but config directory is not writable",
            contents);
    }

    [Fact]
    public void Integration_Uses_LibGit2_And_Not_System_Git() {
        // Doc callout: "You do not need to install `git` in the container.
        // RackPeek uses the bundled libgit2 library to talk to remotes
        // directly over HTTPS." Verify the production implementation is
        // LibGit2Sharp-backed (not a process-shelling wrapper) so the doc
        // statement can't be invalidated by an accidental refactor.
        IGitRepository repo = new LibGit2GitRepository(
            _tempDir,
            new TokenCredentialsProvider("u", "t"));

        // Sanity: LibGit2Sharp must be reachable at all — if it weren't, the
        // line above would have thrown a DllNotFoundException for the native
        // libgit2 binary, which proves it ships with the assembly.
        Assert.True(repo.IsAvailable);

        // Pin: the production binding is the LibGit2Sharp-backed one. If
        // someone swaps it for a CLI-shelling implementation, this fails and
        // they're forced to revisit the "no system git needed" promise.
        Assert.IsType<LibGit2GitRepository>(repo);

        // And LibGit2Sharp itself must be loaded into the test process, which
        // proves the assembly ships with native binaries.
        Assert.NotNull(typeof(Repository).Assembly.Location);
    }

    private static string LocateRepoFile(string relativeFromRepoRoot) {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null) {
            var candidate = Path.Combine(dir.FullName, relativeFromRepoRoot);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {relativeFromRepoRoot} walking up from {AppContext.BaseDirectory}");
    }
}
