using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources.Laptops;
using RackPeek.Domain.UseCases;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Laptops.Rename;

public class LaptopRenameSettings : LaptopNameSettings {
    [CommandArgument(1, "<new-name>")]
    public string NewName { get; set; } = default!;
}

public class LaptopRenameCommand(
    IServiceProvider serviceProvider
) : AsyncCommand<LaptopRenameSettings> {
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        LaptopRenameSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRenameResourceUseCase<Laptop> renameUseCase = scope.ServiceProvider.GetRequiredService<IRenameResourceUseCase<Laptop>>();

        await renameUseCase.ExecuteAsync(settings.Name, settings.NewName);

        AnsiConsole.MarkupLine($"[green]Laptop '{settings.Name}' renamed to '{settings.NewName}'.[/]");
        return 0;
    }
}
