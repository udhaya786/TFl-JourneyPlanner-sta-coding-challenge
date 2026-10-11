using Microsoft.Playwright;
using Reqnroll;
using TflJourneyPlanner.Pages;

namespace TflJourneyPlanner.Support;

/// <summary>Holds the Playwright objects for one scenario (injected by Reqnroll).</summary>
public class ScenarioContextHolder
{
    public IPlaywright Playwright { get; set; } = default!;
    public IBrowser Browser { get; set; } = default!;
    public IPage Page { get; set; } = default!;
    public PlanJourneyPage Journey { get; set; } = default!;
}

[Binding]
public class Hooks
{
    private readonly ScenarioContextHolder _ctx;

    public Hooks(ScenarioContextHolder ctx) => _ctx = ctx;

    [BeforeScenario("ui")]
    public async Task BeforeScenario()
    {
        _ctx.Playwright = await Playwright.CreateAsync();
        _ctx.Browser = await _ctx.Playwright.Chromium.LaunchAsync(new()
        {
            Headless = Environment.GetEnvironmentVariable("HEADED") != "1"
        });
        _ctx.Page = await _ctx.Browser.NewPageAsync();
        _ctx.Journey = new PlanJourneyPage(_ctx.Page);
    }

    [AfterScenario("ui")]
    public async Task AfterScenario(ScenarioContext scenario)
    {
        if (scenario.TestError != null)
        {
            Directory.CreateDirectory("screenshots");
            await _ctx.Page.ScreenshotAsync(new()
            {
                Path = $"screenshots/{DateTime.UtcNow:yyyyMMdd_HHmmss}.png",
                FullPage = true
            });
        }
        await _ctx.Browser.CloseAsync();
        _ctx.Playwright.Dispose();
    }
}
