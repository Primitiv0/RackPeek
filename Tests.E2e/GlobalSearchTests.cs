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
    public async Task User_Can_Search_For_Resources_Of_Every_Kind_And_Navigate_To_One() {
        (IBrowserContext context, IPage page) = await CreatePageAsync();

        // Shared random token in every seeded name so a single fragment hits
        // every kind, while a kind-prefixed query isolates one.
        var token = Guid.NewGuid().ToString("N")[..6];

        var seed = new (string Kind, string Name)[] {
            ("server",      $"srv-{token}"),
            ("switch",      $"sw-{token}"),
            ("firewall",    $"fw-{token}"),
            ("router",      $"rt-{token}"),
            ("accesspoint", $"ap-{token}"),
            ("ups",         $"ups-{token}"),
            ("desktop",     $"dt-{token}"),
            ("laptop",      $"lt-{token}"),
            ("system",      $"sys-{token}"),
            ("service",     $"svc-{token}"),
        };

        try {
            await page.GotoAsync(_fixture.BaseUrl);
            await new MainLayoutPom(page).AssertLoadedAsync();

            // ───── Seed: one resource of every kind ─────
            await page.GotoAsync($"{_fixture.BaseUrl}/servers/list");
            await new ServersListPom(page).AddServerAsync($"srv-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/switches/list");
            await new SwitchListPom(page).AddSwitchAsync($"sw-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/firewalls/list");
            await new FirewallsListPom(page).AddFirewallAsync($"fw-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/routers/list");
            await new RouterListPom(page).AddRouterAsync($"rt-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/accesspoints/list");
            await new AccessPointsListPom(page).AddAccessPointAsync($"ap-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/ups/list");
            await new UpsListPom(page).AddUpsAsync($"ups-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/desktops/list");
            await new DesktopsListPom(page).AddDesktopAsync($"dt-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/laptops/list");
            await new LaptopListPom(page).AddLaptopAsync($"lt-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/systems/list");
            await new SystemsListPom(page).AddSystemAsync($"sys-{token}");

            await page.GotoAsync($"{_fixture.BaseUrl}/services/list");
            await new ServicesListPom(page).AddServiceAsync($"svc-{token}");

            var search = new GlobalSearchPom(page);

            // ───── Per-kind search: each unique name surfaces its own kind ─────
            foreach ((string Kind, string Name) entry in seed) {
                await search.SearchAsync(entry.Name);
                await search.AssertResultExistsAsync(entry.Kind, entry.Name);
            }

            // ───── Cross-kind search: shared token surfaces results from
            //       multiple kinds in the same dropdown. Top N is capped at 8;
            //       within an equal-score tier ties break alphabetically by name,
            //       so the kinds asserted here are those guaranteed to land
            //       inside the cap given the seeded name prefixes.
            await search.SearchAsync(token);
            await search.AssertResultExistsAsync("service", $"svc-{token}");
            await search.AssertResultExistsAsync("server",  $"srv-{token}");
            await search.AssertResultExistsAsync("switch",  $"sw-{token}");

            // ───── Click navigates to the resource page ─────
            await search.SearchAsync($"svc-{token}");
            await search.ClickResultAsync("service", $"svc-{token}");
            await page.WaitForURLAsync($"**/resources/services/svc-{token}");
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
