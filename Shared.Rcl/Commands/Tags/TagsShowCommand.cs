using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Persistence;
using RackPeek.Domain.Resources;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Tags;

public class TagsShowSettings : CommandSettings {
    [CommandArgument(0, "<tag>")] public string Tag { get; set; } = default!;
}

public class TagsShowCommand(IServiceProvider serviceProvider) : AsyncCommand<TagsShowSettings> {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        TagsShowSettings settings,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IResourceCollection repo = scope.ServiceProvider.GetRequiredService<IResourceCollection>();

        IReadOnlyList<Resource> resources = await repo.GetByTagAsync(settings.Tag);

        if (resources.Count == 0) {
            AnsiConsole.MarkupLine($"[grey]No resources tagged '{settings.Tag.EscapeMarkup()}'.[/]");
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]Resources tagged '{settings.Tag.EscapeMarkup()}'[/]")
            .AddColumn("Name")
            .AddColumn("Kind");

        foreach (Resource resource in resources.OrderBy(r => r.Kind).ThenBy(r => r.Name))
            table.AddRow(resource.Name.EscapeMarkup(), resource.Kind.EscapeMarkup());

        AnsiConsole.Write(table);
        return 0;
    }
}
