using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.AccessPoints;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.AccessPoints.Rename;

public class AccessPointRenameSettings : AccessPointNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class AccessPointRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<AccessPointRenameSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        AccessPointRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<AccessPoint> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<AccessPoint>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]AccessPoint '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
