using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Graph.Serialisers;
using RackPeek.Domain.Graph.UseCases;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Graph;

public class GraphLogicalCommand(IServiceProvider serviceProvider) : AsyncCommand {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        BuildLogicalGraphUseCase useCase =
            scope.ServiceProvider.GetRequiredService<BuildLogicalGraphUseCase>();

        RackPeek.Domain.Graph.Graph graph = await useCase.ExecuteAsync();
        var mermaid = new MermaidSerialiser().Serialise(graph);

        System.Console.Out.Write(mermaid);
        return 0;
    }
}
