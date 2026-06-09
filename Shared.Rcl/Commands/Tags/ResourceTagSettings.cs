using Spectre.Console.Cli;

namespace Shared.Rcl.Commands.Tags;

public class ResourceTagSettings : CommandSettings {
    [CommandArgument(0, "<name>")] public string Name { get; set; } = default!;

    [CommandArgument(1, "<tag>")] public string Tag { get; set; } = default!;
}
