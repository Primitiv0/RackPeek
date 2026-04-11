using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.SystemResources.UseCases;
using Shared.Rcl.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Systems;

public class SystemDescribeCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<SystemNameSettings> {
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        SystemNameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        DescribeSystemUseCase useCase = scope.ServiceProvider.GetRequiredService<DescribeSystemUseCase>();

        SystemDescription system = await useCase.ExecuteAsync(settings.Name);

        Grid grid = new Grid()
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn(new GridColumn().NoWrap());

        grid.AddRow("Name:", system.Name.EscapeMarkup());
        grid.AddRow("Type:", (system.Type ?? "Unknown").EscapeMarkup());
        grid.AddRow("OS:", (system.Os ?? "Unknown").EscapeMarkup());
        grid.AddRow("Cores:", system.Cores.ToString());
        grid.AddRow("RAM (GB):", system.RamGb.ToString());
        grid.AddRow("Total Storage (GB):", system.TotalStorageGb.ToString());
        grid.AddRow("Runs On:", (string.Join(", ", system.RunsOn) ?? "Unknown").EscapeMarkup());


        if (system.Labels.Count > 0)
            grid.AddRow("Labels:", string.Join(", ", system.Labels.Select(kvp => $"{kvp.Key.EscapeMarkup()}: {kvp.Value.EscapeMarkup()}")));

        AnsiConsole.Write(
            new Panel(grid)
                .Header("System")
                .Border(BoxBorder.Rounded));

        return 0;
    }
}
