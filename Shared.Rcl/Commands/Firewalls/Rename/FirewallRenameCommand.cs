using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Firewalls;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Firewalls.Rename;

public class FirewallRenameSettings : FirewallNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class FirewallRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<FirewallRenameSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        FirewallRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<Firewall> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<Firewall>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]Firewall '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
