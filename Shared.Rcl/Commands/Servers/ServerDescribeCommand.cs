using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Servers;
using RackPeek.Domain.Resources.SubResources;
using RackPeek.Domain.UseCases;
using Shared.Rcl.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Servers;

public class ServerDescribeCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<ServerNameSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        ServerNameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IGetResourceByNameUseCase<Server> useCase =
            scope.ServiceProvider.GetRequiredService<IGetResourceByNameUseCase<Server>>();

        Server server = await useCase.ExecuteAsync(settings.Name);

        Grid grid = new Grid()
            .AddColumn()
            .AddColumn();

        grid.AddRow("Name", server.Name.EscapeMarkup());
        grid.AddRow("IPMI", server.Ipmi == true ? "yes" : "no");
        grid.AddRow("RAM", $"{server.Ram?.Size ?? 0} GB");

        if (server.Cpus != null)
            foreach (Cpu cpu in server.Cpus)
                grid.AddRow("CPU", $"{cpu.Model.EscapeMarkup()} ({cpu.Cores}/{cpu.Threads})");

        if (server.Labels.Count > 0)
            grid.AddRow("Labels", string.Join(", ", server.Labels.Select(kvp => $"{kvp.Key.EscapeMarkup()}: {kvp.Value.EscapeMarkup()}")));

        AnsiConsole.Write(
            new Panel(grid)
                .Header("Server")
                .Border(BoxBorder.Rounded));

        return 0;
    }
}
