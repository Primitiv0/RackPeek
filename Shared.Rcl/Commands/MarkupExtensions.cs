using Spectre.Console;

namespace Shared.Rcl.Commands;

public static class MarkupExtensions {
    public static string EscapeMarkup(this string? text) => Markup.Escape(text ?? "");
}
