using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Routers;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Routers.Rename;

public class RouterRenameSettings : RouterNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class RouterRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<RouterRenameSettings> {
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        RouterRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<Router> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<Router>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]Router '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
