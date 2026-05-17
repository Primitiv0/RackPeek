using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.SystemResources;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Systems.Rename;

public class SystemRenameSettings : SystemNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class SystemRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<SystemRenameSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SystemRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<SystemResource> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<SystemResource>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]System '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
