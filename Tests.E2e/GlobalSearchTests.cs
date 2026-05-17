using Microsoft.Playwright;
using Tests.E2e.Infra;
using Tests.E2e.PageObjectModels;
using Xunit.Abstractions;

namespace Tests.E2e;

public class GlobalSearchTests(
    PlaywrightFixture fixture,
    ITestOutputHelper output) : E2ETestBase(fixture, output) {
    private readonly PlaywrightFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task User_Can_Search_For_A_Service_And_Navigate_To_It() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();
        var serviceName = $"e2e-search-{Guid.NewGuid():N}"[..20];

        try {
            // Seed: create a service via the existing services list flow
            await page.GotoAsync(_fixture.BaseUrl);

            var layout = new MainLayoutPom(page);
            await layout.AssertLoadedAsync();
            await layout.GotoServicesAsync();

            var servicesList = new ServicesListPom(page);
            await servicesList.AssertLoadedAsync();
            await servicesList.AddServiceAsync(serviceName);

            // Use global search from the header — partial prefix of the unique name
            var search = new GlobalSearchPom(page);
            await search.SearchAsync(serviceName[..12]);
            await search.AssertResultExistsAsync("service", serviceName);

            // Click the result and assert navigation to the service detail page
            await search.ClickResultAsync("service", serviceName);
            await page.WaitForURLAsync($"**/resources/services/{serviceName}");
        }
        catch (Exception) {
            _output.WriteLine("TEST FAILED — Capturing diagnostics");
            _output.WriteLine($"Current URL: {page.Url}");

            var html = await page.ContentAsync();
            _output.WriteLine("==== DOM SNAPSHOT START ====");
            _output.WriteLine(html);
            _output.WriteLine("==== DOM SNAPSHOT END ====");

            throw;
        }
        finally {
            await context.CloseAsync();
        }
    }
}
