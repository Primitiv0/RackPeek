using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Desktops;
using Shared.Rcl.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Desktops;

public class DesktopDescribeCommand(IServiceProvider provider)
    : AsyncCommand<DesktopNameSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        DesktopNameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = provider.CreateScope();
        DescribeDesktopUseCase useCase = scope.ServiceProvider.GetRequiredService<DescribeDesktopUseCase>();

        DesktopDescription result = await useCase.ExecuteAsync(settings.Name);

        Grid grid = new Grid().AddColumn().AddColumn();

        grid.AddRow("Name:", result.Name.EscapeMarkup());
        grid.AddRow("Model:", (result.Model ?? "Unknown").EscapeMarkup());
        grid.AddRow("CPUs:", result.CpuCount.ToString());
        grid.AddRow("RAM:", (result.RamSummary ?? "None").EscapeMarkup());
        grid.AddRow("Drives:", result.DriveCount.ToString());
        grid.AddRow("NICs:", result.NicCount.ToString());
        grid.AddRow("GPUs:", result.GpuCount.ToString());

        if (result.Labels.Count > 0)
            grid.AddRow("Labels:", string.Join(", ", result.Labels.Select(kvp => $"{kvp.Key.EscapeMarkup()}: {kvp.Value.EscapeMarkup()}")));

        AnsiConsole.Write(new Panel(grid).Header("Desktop").Border(BoxBorder.Rounded));

        return 0;
    }
}
