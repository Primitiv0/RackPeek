using LibGit2Sharp;
using RackPeek.Domain.Git;
using RackPeek.Domain.Git.UseCases;

namespace Tests.Git;

public sealed class GitRepositoryAvailabilityTests : IDisposable {
    private readonly string _tempDir;
    private readonly IGitCredentialsProvider _creds =
        new TokenCredentialsProvider("test-user", "test-token");

    public GitRepositoryAvailabilityTests() {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "rackpeek-git-tests",
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "config.yaml"), "");
    }

    public void Dispose() {
        try {
            if (Directory.Exists(_tempDir))
                ForceDelete(_tempDir);
        }
        catch {
            // ignore cleanup issues
        }
    }

    private static void ForceDelete(string path) {
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, true);
    }

    [Fact]
    public void Fresh_Config_Directory_Becomes_Available_After_Construction() {
        // Reproduces the user's bug: GIT_TOKEN is set, container starts on a
        // fresh volume with only config.yaml. Without auto-init, IsAvailable
        // stays false and every git action returns "Git is not available."
        Assert.False(Repository.IsValid(_tempDir),
            "precondition: fresh dir should not be a git repo yet");

        var repo = new LibGit2GitRepository(_tempDir, _creds);

        Assert.True(repo.IsAvailable,
            "after construction with GIT_TOKEN configured, the repo should be initialised " +
            "so the user can immediately add a remote without an explicit Enable Git click");
    }

    [Fact]
    public async Task Add_Remote_Succeeds_On_Fresh_Config_Directory() {
        // End-to-end: simulates the user clicking Add Remote on a brand-new
        // install. Before the fix this returns "Git is not available." even
        // though GIT_TOKEN was set on the container.
        var repo = new LibGit2GitRepository(_tempDir, _creds);
        var useCase = new AddRemoteUseCase(repo);

        var error = await useCase.ExecuteAsync("https://example.com/user/repo.git");

        // The AddRemote fetch will fail (no real remote) but the failure
        // should be a network error, not "Git is not available."
        Assert.DoesNotContain("Git is not available", error ?? string.Empty);
    }

    [Fact]
    public void Existing_Repo_Is_Not_Reinitialised() {
        // Auto-init must be idempotent: pre-existing repos must keep their
        // history. Initialising twice would discard the user's commits.
        Repository.Init(_tempDir);

        using (var seed = new Repository(_tempDir)) {
            File.WriteAllText(Path.Combine(_tempDir, "seed.txt"), "seed");
            Commands.Stage(seed, "seed.txt");
            var sig = new Signature("seed", "seed@test", DateTimeOffset.UtcNow);
            seed.Commit("seed commit", sig, sig);
        }

        var repo = new LibGit2GitRepository(_tempDir, _creds);

        Assert.True(repo.IsAvailable);

        using var verify = new Repository(_tempDir);
        Assert.NotNull(verify.Head.Tip);
        Assert.Equal("seed commit", verify.Head.Tip.MessageShort);
    }

    [Fact]
    public void Missing_Directory_Stays_Unavailable() {
        // Edge case: directory doesn't exist (e.g. misconfigured volume).
        // We must not throw out of the constructor and we must not pretend
        // git is available.
        var missing = Path.Combine(_tempDir, "definitely-not-there");

        var repo = new LibGit2GitRepository(missing, _creds);

        Assert.False(repo.IsAvailable);
    }

    [Fact]
    public async Task Add_Remote_Persists_The_Configured_Origin() {
        // The full happy path that was broken before the fix: construct a repo
        // against a fresh dir, add a remote, confirm it lives on disk so a
        // subsequent push/pull would find it.
        var repo = new LibGit2GitRepository(_tempDir, _creds);
        var useCase = new AddRemoteUseCase(repo);

        var url = "https://example.com/user/repo.git";
        await useCase.ExecuteAsync(url);

        using var verify = new Repository(_tempDir);
        Remote? origin = verify.Network.Remotes["origin"];

        Assert.NotNull(origin);
        Assert.Equal(url, origin!.Url);
    }

    [Fact]
    public void Existing_Repo_With_Remote_Is_Detected_As_Available() {
        // After a container restart we should pick up the previously
        // initialised repo (including its remote) without re-initialising
        // and without losing state.
        Repository.Init(_tempDir);
        using (var seed = new Repository(_tempDir)) {
            seed.Network.Remotes.Add("origin", "https://example.com/user/repo.git");
        }

        var repo = new LibGit2GitRepository(_tempDir, _creds);

        Assert.True(repo.IsAvailable);
        Assert.True(repo.HasRemote());
    }
}
