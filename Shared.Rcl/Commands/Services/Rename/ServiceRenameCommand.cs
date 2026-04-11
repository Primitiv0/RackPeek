using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Services;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Services.Rename;

public class ServiceRenameSettings : ServiceNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class ServiceRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<ServiceRenameSettings> {
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        ServiceRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<Service> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<Service>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]Service '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
