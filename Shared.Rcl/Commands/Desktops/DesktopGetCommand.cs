using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Desktops;
using RackPeek.Domain.UseCases;
using Shared.Rcl.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Desktops;

public class DesktopGetCommand(IServiceProvider provider)
    : AsyncCommand {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken) {
        using IServiceScope scope = provider.CreateScope();
        IGetAllResourcesByKindUseCase<Desktop> useCase =
            scope.ServiceProvider.GetRequiredService<IGetAllResourcesByKindUseCase<Desktop>>();

        IReadOnlyList<Desktop> desktops = await useCase.ExecuteAsync();

        if (desktops.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No desktops found.[/]");
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("Model")
            .AddColumn("CPUs")
            .AddColumn("RAM")
            .AddColumn("Drives")
            .AddColumn("NICs")
            .AddColumn("GPUs");

        foreach (Desktop d in desktops)
            table.AddRow(
                d.Name.EscapeMarkup(),
                (d.Model ?? "Unknown").EscapeMarkup(),
                (d.Cpus?.Count ?? 0).ToString(),
                d.Ram == null ? "None" : $"{d.Ram.Size}GB",
                (d.Drives?.Count ?? 0).ToString(),
                (d.Ports?.Count ?? 0).ToString(),
                (d.Gpus?.Count ?? 0).ToString()
            );

        AnsiConsole.Write(table);
        return 0;
    }
}
