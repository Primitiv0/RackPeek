using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit.Abstractions;

namespace Tests.E2e;

public class DockerHealthcheckTests(ITestOutputHelper output) {
    private const string _dockerImage = "rackpeek:ci";
    private static readonly TimeSpan _healthDeadline = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Container_Reports_Healthy_Status_Via_Docker_HEALTHCHECK() {
        await using IContainer container = new ContainerBuilder(_dockerImage)
            .WithPortBinding(8080, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilContainerIsHealthy())
            .Build();

        using var cts = new CancellationTokenSource(_healthDeadline);

        try {
            // StartAsync blocks until the wait strategy succeeds. If the image
            // has no HEALTHCHECK instruction Docker reports `health: none` forever,
            // so we bound the wait — the cancellation token forces a fast failure
            // rather than letting the test hang.
            await container.StartAsync(cts.Token);

            Assert.Equal(TestcontainersStates.Running, container.State);
            Assert.Equal(TestcontainersHealthStatus.Healthy, container.Health);
        }
        catch (Exception) {
            output.WriteLine($"Container did not reach 'Healthy' within {_healthDeadline.TotalSeconds:F0}s.");
            output.WriteLine($"Container state:  {container.State}");
            output.WriteLine($"Container health: {container.Health}");
            try {
                (string Stdout, string Stderr) logs = await container.GetLogsAsync();
                output.WriteLine("==== CONTAINER STDOUT ====");
                output.WriteLine(logs.Stdout);
                output.WriteLine("==== CONTAINER STDERR ====");
                output.WriteLine(logs.Stderr);
            }
            catch (Exception logEx) {
                output.WriteLine($"Failed to capture container logs: {logEx.Message}");
            }
            throw;
        }
    }
}
