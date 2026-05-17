using Microsoft.Playwright;

namespace Tests.E2e.PageObjectModels;

/// <summary>
/// POM for the header-level global search component.
///
/// Selectors anchor on stable test IDs:
///   global-search-input             — the textbox
///   global-search-results           — the dropdown container (visible when query is non-empty)
///   global-search-no-results        — empty-state inside the dropdown
///   global-search-result-{kind}-{n} — one row per match
///   global-search-result-match      — secondary label inside a row, e.g. "via ip: 10.0.0.5"
/// </summary>
public class GlobalSearchPom(IPage page) {
    public ILocator Input => page.GetByTestId("global-search-input");
    public ILocator Results => page.GetByTestId("global-search-results");
    public ILocator NoResults => page.GetByTestId("global-search-no-results");

    public ILocator Result(string kind, string name) =>
        page.GetByTestId($"global-search-result-{kind.ToLowerInvariant()}-{Sanitize(name)}");

    public ILocator ResultMatchLabel(string kind, string name) =>
        Result(kind, name).GetByTestId("global-search-result-match");

    public async Task SearchAsync(string query) {
        await Input.FillAsync(query);
        await Assertions.Expect(Results).ToBeVisibleAsync();
    }

    public async Task ClearAsync() => await Input.FillAsync("");

    public async Task ClickResultAsync(string kind, string name) =>
        await Result(kind, name).ClickAsync();

    public async Task AssertResultExistsAsync(string kind, string name) =>
        await Assertions.Expect(Result(kind, name)).ToBeVisibleAsync();

    public async Task AssertResultDoesNotExistAsync(string kind, string name) =>
        await Assertions.Expect(Result(kind, name)).Not.ToBeVisibleAsync();

    private static string Sanitize(string value) =>
        value.Replace(" ", "-").ToLowerInvariant();
}
