using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Servers;
using Shared.Rcl.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Servers;

public class ServerGetCommand(
    IServiceProvider serviceProvider
) : AsyncCommand {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        ServerHardwareReportUseCase useCase = scope.ServiceProvider.GetRequiredService<ServerHardwareReportUseCase>();

        ServerHardwareReport report = await useCase.ExecuteAsync();

        if (report.Servers.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No servers found.[/]");
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("CPU")
            .AddColumn("C/T")
            .AddColumn("RAM")
            .AddColumn("Storage")
            .AddColumn("NICs")
            .AddColumn("IPMI");

        foreach (ServerHardwareRow s in report.Servers)
            table.AddRow(
                s.Name.EscapeMarkup(),
                s.CpuSummary.EscapeMarkup(),
                $"{s.TotalCores}/{s.TotalThreads}",
                $"{s.RamGb} GB",
                $"{s.TotalStorageGb} GB",
                $"{s.TotalNicPorts}×{s.MaxNicSpeedGb}G",
                s.Ipmi ? "[green]yes[/]" : "[red]no[/]"
            );

        AnsiConsole.Write(table);
        return 0;
    }
}
