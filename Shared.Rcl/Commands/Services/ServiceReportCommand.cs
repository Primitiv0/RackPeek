using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Services.UseCases;
using Shared.Rcl.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Services;

public class ServiceReportCommand(
    IServiceProvider serviceProvider
) : AsyncCommand {
    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        ServiceReportUseCase useCase = scope.ServiceProvider.GetRequiredService<ServiceReportUseCase>();

        ServiceReport report = await useCase.ExecuteAsync();

        if (report.Services.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No Services found.[/]");
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Name")
            .AddColumn("Ip")
            .AddColumn("Port")
            .AddColumn("Protocol")
            .AddColumn("Url")
            .AddColumn("Runs On");

        foreach (ServiceReportRow s in report.Services) {
            string? sys = null;
            string? phys = null;

            if (s.RunsOnSystemHost?.Count > 0) sys = string.Join(", ", s.RunsOnSystemHost);
            if (s.RunsOnPhysicalHost?.Count > 0) phys = string.Join(", ", s.RunsOnPhysicalHost);

            table.AddRow(
                s.Name.EscapeMarkup(),
                (s.Ip ?? "").EscapeMarkup(),
                (s.Port.ToString() ?? "").EscapeMarkup(),
                (s.Protocol ?? "").EscapeMarkup(),
                (s.Url ?? "").EscapeMarkup(),
                ServicesFormatExtensions.FormatRunsOn(sys, phys)
            );
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
