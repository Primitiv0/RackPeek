using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RackPeek.Domain.Resources.Desktops;
using Shared.Rcl.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Desktops;

public class DesktopReportCommand(
    ILogger<DesktopReportCommand> logger,
    IServiceProvider serviceProvider
) : AsyncCommand {
    private readonly ILogger<DesktopReportCommand> _logger = logger;

    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        DesktopHardwareReportUseCase useCase = scope.ServiceProvider.GetRequiredService<DesktopHardwareReportUseCase>();

        DesktopHardwareReport report = await useCase.ExecuteAsync();

        if (report.Desktops.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No desktops found.[/]");
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
            .AddColumn("GPU");

        foreach (DesktopHardwareRow d in report.Desktops)
            table.AddRow(
                d.Name.EscapeMarkup(),
                d.CpuSummary.EscapeMarkup(),
                $"{d.TotalCores}/{d.TotalThreads}",
                $"{d.RamGb} GB",
                $"{d.TotalStorageGb} GB (SSD {d.SsdStorageGb} / HDD {d.HddStorageGb})",
                d.NicSummary.EscapeMarkup(),
                d.GpuSummary.EscapeMarkup()
            );

        AnsiConsole.Write(table);
        return 0;
    }
}
