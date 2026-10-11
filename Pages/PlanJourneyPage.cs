using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TflJourneyPlanner.Pages;

/// <summary>
/// Page object for the TfL "Plan a journey" widget.
/// Port of plan-journey.page.ts (Microsoft.Playwright, async/await).
/// </summary>
public class PlanJourneyPage
{
    private readonly IPage _page;
    public const string Url = "https://tfl.gov.uk/plan-a-journey/?cid=plan-a-journey";

    public PlanJourneyPage(IPage page) => _page = page;

    // ─── Cookies ────────────────────────────────────────────────────

    public ILocator CookieOverlay => _page.Locator("#cb-cookieoverlay");

    public async Task OpenAsync() =>
        await _page.GotoAsync(Url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

    /// <summary>Accept cookies; force-hide the overlay if it still blocks the page.</summary>
    public async Task AcceptCookiesAsync()
    {
        try
        {
            await CookieOverlay.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }
        catch (TimeoutException) { /* overlay may not appear */ }

        var buttons = new[] { "Accept all cookies", "Accept", "Got it", "Allow all" };
        foreach (var name in buttons)
        {
            var btn = _page.GetByRole(AriaRole.Button, new() { Name = name });
            if (await btn.First.IsVisibleAsync())
            {
                await btn.First.ClickAsync();
                break;
            }
        }

        // Fallback: hide the overlay so it can't intercept clicks
        await _page.EvaluateAsync(@"() => {
            const o = document.querySelector('#cb-cookieoverlay');
            if (o) { o.style.display = 'none'; o.style.pointerEvents = 'none'; }
        }");
    }

    // ─── Journey inputs ─────────────────────────────────────────────

    public ILocator FromCombobox => _page.GetByRole(AriaRole.Combobox, new() { Name = "From location" });
    public ILocator ToCombobox => _page.GetByRole(AriaRole.Combobox, new() { Name = "To location" });
    public ILocator ChangeTimeLink => _page.GetByRole(AriaRole.Link, new() { Name = "change time" });
    public ILocator TimeSelect => _page.GetByLabel("Time", new() { Exact = true });
    public ILocator PlanJourneyButton => _page.GetByRole(AriaRole.Button, new() { Name = "Plan my journey" });

    public async Task SetFromViaAutocompleteAsync(string partial, string fullOption)
    {
        await FromCombobox.ClickAsync();
        await FromCombobox.FillAsync(partial);
        await _page.GetByRole(AriaRole.Option, new() { Name = fullOption }).First.ClickAsync();
    }

    public async Task SetToViaAutocompleteAsync(string partial, string fullOption)
    {
        await ToCombobox.ClickAsync();
        await ToCombobox.FillAsync(partial);
        await _page.GetByRole(AriaRole.Option, new() { Name = fullOption }).First.ClickAsync();
    }

    public async Task SetTimeAsync(string value)
    {
        await ChangeTimeLink.ClickAsync();
        await TimeSelect.SelectOptionAsync(value); // e.g. "0915"
    }

    public async Task PlanJourneyAsync() => await PlanJourneyButton.ClickAsync();

    // ─── Validation / error messages ────────────────────────────────

    public ILocator FromRequiredError => _page.GetByText("The From field is required.");
    public ILocator ToRequiredError => _page.GetByText("The To field is required.");
    public ILocator InvalidJourneyError => _page.GetByText("Sorry, we can't find a");

    public ILocator AnyJourneyError =>
        _page.GetByText("Journey planner could not find any results to your search. Please try again.");

    public async Task SubmitEmptyJourneyAsync() => await PlanJourneyButton.ClickAsync();

    public async Task SubmitInvalidJourneyAsync(string fromText, string toText)
    {
        await FromCombobox.ClickAsync();
        await FromCombobox.FillAsync(fromText);
        await ToCombobox.ClickAsync();
        await ToCombobox.FillAsync(toText);
        await PlanJourneyButton.ClickAsync();
    }

    public async Task ExpectRequiredErrorsForEmptyJourneyAsync()
    {
        await Expect(FromRequiredError).ToBeVisibleAsync();
        await Expect(ToRequiredError).ToBeVisibleAsync();
    }

    public async Task ExpectInvalidJourneyErrorAsync()
    {
        var error = _page.GetByText(new Regex("Journey planner could not find any results", RegexOptions.IgnoreCase));
        await Expect(error).ToBeVisibleAsync(new() { Timeout = 20000 });
    }

    // ─── Results: walking and cycling ───────────────────────────────

    public ILocator WalkingAndCyclingHeading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Walking and cycling" });

    public ILocator CyclingTab => _page.GetByRole(AriaRole.Link, new() { Name = "Cycling" });

    public async Task OpenWalkingAndCyclingSectionAsync()
    {
        await WalkingAndCyclingHeading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await WalkingAndCyclingHeading.ClickAsync();
    }

    public async Task OpenCyclingTabAsync() => await CyclingTab.ClickAsync();

    // Result cards, e.g. link "Walking Walking speed: Moderate Distance: 0.4km 6mins"
    public ILocator WalkingOption =>
        _page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex(@"^Walking\b.*mins") });

    public ILocator CyclingOption =>
        _page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex(@"^Cycling\b.*mins") });

    public Task<int> GetWalkingMinutesAsync() => GetMinutesAsync(WalkingOption);
    public Task<int> GetCyclingMinutesAsync() => GetMinutesAsync(CyclingOption);

    private static async Task<int> GetMinutesAsync(ILocator option)
    {
        await Expect(option).ToBeVisibleAsync(new() { Timeout = 30000 });
        var text = await option.InnerTextAsync();
        var match = Regex.Match(text, @"(\d+)\s*mins?");
        if (!match.Success)
            throw new InvalidOperationException($"No minutes value found in: '{text}'");
        return int.Parse(match.Groups[1].Value);
    }

    // ─── Edit preferences ───────────────────────────────────────────

    public ILocator EditPreferencesButton => _page.GetByRole(AriaRole.Button, new() { Name = "Edit preferences" });
    public ILocator WalkingMinutesSelect => _page.GetByLabel("I only want to walk for a");
    public ILocator WalkingSpeedLabel => _page.Locator("label", new() { HasTextString = "My walking speed is" });
    public ILocator UpdateJourneyButton => _page.GetByRole(AriaRole.Button, new() { Name = "Update journey" });
    public ILocator CancelPreferencesButton => _page.GetByRole(AriaRole.Button, new() { Name = "Cancel" });

    public async Task OpenEditPreferencesAsync() => await EditPreferencesButton.ClickAsync();

    public async Task ChooseLeastWalkingMinutesAsync(string minutes) =>
        await WalkingMinutesSelect.SelectOptionAsync(minutes); // e.g. "10"

    // "Routes with least walking" option inside the Edit preferences panel.
    // Label text is matched loosely; confirm against the live panel.
    public ILocator LeastWalkingOption =>
        _page.GetByText(new Regex("least walking", RegexOptions.IgnoreCase))
             .Filter(new() { Visible = true })
             .First;

    public async Task ChooseLeastWalkingRoutesAsync()
    {
        // Assert first so a miss prints a snapshot of the open preferences panel
        await Expect(LeastWalkingOption).ToBeVisibleAsync(new() { Timeout = 10000 });
        await LeastWalkingOption.ClickAsync();
    }

    public async Task SaveUpdatedJourneyAsync() => await UpdateJourneyButton.ClickAsync();
    public async Task CancelPreferencesAsync() => await CancelPreferencesButton.ClickAsync();

    // ─── Directions / details ───────────────────────────────────────

    public ILocator ViewDirectionsButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "View directions", Exact = true });

    public ILocator HideDirectionsLinks => _page.GetByRole(AriaRole.Link, new() { Name = "Hide directions" });

    public async Task ToggleDirectionsAsync() => await ViewDirectionsButton.ClickAsync();

    // Generic "Option 1" (original was hard-coded to "walking, Piccadilly")
    // First visible "View details" button (options 2 and 3 are collapsed, so theirs are hidden)
    public ILocator FirstOptionViewDetailsButton =>
        _page.Locator("button.show-detailed-results")
             .Filter(new() { HasTextRegex = new Regex("View details", RegexOptions.IgnoreCase), Visible = true })
             .First;

    public ILocator HideDetailsButton => _page.GetByRole(AriaRole.Button, new() { Name = "Hide details" });

    public ILocator CoventGardenAccessHeading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Covent Garden Underground Station" });

    // CHANGED vs TS: waits on the element instead of a fixed 5s sleep, and FAILS
    // instead of console.warn + silent return (which would let the test pass falsely).
    public async Task OpenFirstOptionDetailsAsync()
    {
        try
        {
            await Expect(FirstOptionViewDetailsButton).ToBeVisibleAsync(new() { Timeout = 10000 });
        }
        catch
        {
            await DumpDetailsDiagnosticsAsync();
            throw;
        }
        await FirstOptionViewDetailsButton.ClickAsync();

        // Let the expanded details render (best effort; the real assertion is in the Then step)
        try
        {
            await _page.Locator("summary.accordion-heading")
                .Filter(new() { HasTextRegex = new Regex("Accessibility details", RegexOptions.IgnoreCase), Visible = true })
                .First.WaitForAsync(new() { Timeout = 5000 });
        }
        catch (TimeoutException) { /* diagnostics below will show what is on screen */ }
    }

    // Diagnostic: lists elements on the results page that mention details / access info.
    private async Task DumpDetailsDiagnosticsAsync()
    {
        var report = await _page.EvaluateAsync<string>("""
            () => {
              const re = /details|access info|step.free|lift/i;
              const lines = [];
              document.querySelectorAll('main *').forEach(e => {
                const own = Array.from(e.childNodes)
                  .filter(n => n.nodeType === 3)
                  .map(n => n.textContent.trim())
                  .join(' ').trim();
                if (own && re.test(own)) {
                  const r = e.getBoundingClientRect();
                  const cs = getComputedStyle(e);
                  const visible = r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none';
                  lines.push(e.tagName.toLowerCase() + '.' + e.className + ' | ' + own.slice(0, 80) + ' | visible=' + visible);
                }
              });
              return lines.join('\n');
            }
            """);
        Console.WriteLine("=== DETAILS DIAGNOSTICS ===\n" + report);
        await File.WriteAllTextAsync("details-diagnostics.txt", report);
    }

    public async Task HideDetailsAsync()
    {
        await Expect(HideDetailsButton).ToBeVisibleAsync();
        await HideDetailsButton.ClickAsync();
    }

    public async Task ExpectCoventGardenAccessInfoVisibleAsync()
    {
        try
        {
            // 1. The detailed steps show the leg departing from Covent Garden Underground Station
            await Expect(_page.Locator("main")).ToContainTextAsync(
                new Regex(@"Covent Garden Underground Station\s+at\s+\d{1,2}:\d{2}"),
                new() { Timeout = 10000, UseInnerText = true });

            // 2. Open the "Accessibility details" accordion for the option
            var summary = _page.Locator("summary.accordion-heading")
                .Filter(new() { HasTextRegex = new Regex("Accessibility details", RegexOptions.IgnoreCase), Visible = true })
                .First;
            await Expect(summary).ToBeVisibleAsync(new() { Timeout = 10000 });
            await summary.ClickAsync();

            // 3. It lists walking time and the access items (stairs / escalator / lift / walkway)
            var panel = summary.Locator("xpath=..");
            await Expect(panel).ToContainTextAsync("Total walking time");
            await Expect(panel).ToContainTextAsync(
                new Regex("stairs|escalator|lift|walkway", RegexOptions.IgnoreCase));
        }
        catch
        {
            await DumpDetailsDiagnosticsAsync();
            throw;
        }
    }

    // ─── Assertion helpers ──────────────────────────────────────────

    // ─── Journey time (first public-transport option) ───────────────

    // Heading looks like: "Option 1: walking, Piccadilly line, walking, 11 mins"
    public ILocator FirstOptionHeading =>
        _page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("^Option 1:") });

    public async Task<int> GetFirstOptionHeadingMinutesAsync()
    {
        await Expect(FirstOptionHeading).ToBeVisibleAsync(new() { Timeout = 30000 });
        var text = await FirstOptionHeading.InnerTextAsync();
        var match = Regex.Match(text, @"(\d+)\s*mins?\s*$");
        if (!match.Success)
            throw new InvalidOperationException($"No journey time in heading: '{text}'");
        return int.Parse(match.Groups[1].Value);
    }

    // Text looks like: "... Total time: 11mins Cost of fare ..."
    public async Task<int> GetFirstOptionTotalTimeMinutesAsync()
    {
        var node = _page.GetByText(new Regex(@"Total time:\s*\d+\s*mins?")).First;
        await Expect(node).ToBeVisibleAsync(new() { Timeout = 10000 });
        var text = await node.InnerTextAsync();
        var match = Regex.Match(text, @"Total time:\s*(\d+)");
        if (!match.Success)
            throw new InvalidOperationException($"No total time in: '{text}'");
        return int.Parse(match.Groups[1].Value);
    }

    public async Task ExpectWalkingAndCyclingVisibleAsync() =>
        await Expect(WalkingAndCyclingHeading).ToBeVisibleAsync();

    // CHANGED vs TS: original swallowed the failure with .catch(() => {}).
    public async Task ExpectUpdatedJourneyAppliedAsync() =>
        await Expect(UpdateJourneyButton).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
}
