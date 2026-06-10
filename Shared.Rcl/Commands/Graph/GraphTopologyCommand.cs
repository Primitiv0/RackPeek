using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Graph.Serialisers;
using RackPeek.Domain.Graph.UseCases;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Graph;

public class GraphTopologyCommand(IServiceProvider serviceProvider) : AsyncCommand {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        BuildPhysicalTopologyUseCase useCase =
            scope.ServiceProvider.GetRequiredService<BuildPhysicalTopologyUseCase>();

        RackPeek.Domain.Graph.Graph graph = await useCase.ExecuteAsync();
        var mermaid = new MermaidSerialiser().Serialise(graph);

        // Use Console.Out directly rather than AnsiConsole — Spectre soft-wraps
        // long lines based on terminal width, which would corrupt the Mermaid
        // syntax when the user pipes the output to a file or another tool.
        System.Console.Out.Write(mermaid);
        return 0;
    }
}
