using Microsoft.Extensions.DependencyInjection;
using RackPeek.Domain.Persistence;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Tags;

public class TagsListCommand(IServiceProvider serviceProvider) : AsyncCommand {
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken) {
        using IServiceScope scope = serviceProvider.CreateScope();
        IResourceCollection repo = scope.ServiceProvider.GetRequiredService<IResourceCollection>();

        Dictionary<string, int> tags = await repo.GetTagsAsync();

        if (tags.Count == 0) {
            AnsiConsole.MarkupLine("[grey]No tags in use.[/]");
            return 0;
        }

        Table table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Tags[/]")
            .AddColumn("Tag")
            .AddColumn(new TableColumn("Count").RightAligned());

        foreach ((var tag, var count) in tags.OrderByDescending(t => t.Value).ThenBy(t => t.Key))
            table.AddRow(tag.EscapeMarkup(), count.ToString());

        AnsiConsole.Write(table);
        return 0;
    }
}
