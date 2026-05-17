using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Switches;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Switches.Rename;

public class SwitchRenameSettings : SwitchNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class SwitchRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<SwitchRenameSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SwitchRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<Switch> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<Switch>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]Switch '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
