using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Desktops;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Desktops.Rename;

public class DesktopRenameSettings : DesktopNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class DesktopRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<DesktopRenameSettings> {
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        DesktopRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<Desktop> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<Desktop>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]Desktop '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
