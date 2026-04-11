using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Servers;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Servers.Rename;

public class ServerRenameSettings : ServerNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class ServerRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<ServerRenameSettings> {
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        ServerRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<Server> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<Server>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]Server '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
