using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Resources;
using RackPeek.Domain.UseCases.Tags;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Tags;

public class TagRemoveCommand<T>(IServiceProvider serviceProvider)
    : AsyncCommand<ResourceTagSettings>
    where T : Resource {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        ResourceTagSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IRemoveTagUseCase<T> useCase = scope.ServiceProvider.GetRequiredService<IRemoveTagUseCase<T>>();

        await useCase.ExecuteAsync(settings.Name, settings.Tag);

        AnsiConsole.MarkupLine($"[green]Tag '{settings.Tag}' removed from '{settings.Name}'.[/]");
        return 0;
    }
}
