using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.UpsUnits;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;
using UpsResource = RackPeek.Domain.Resources.UpsUnits.Ups;

namespace Shared.Rcl.Commands.Ups.Rename;

public class UpsRenameSettings : UpsNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class UpsRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<UpsRenameSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        UpsRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<UpsResource> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<UpsResource>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]UPS '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
