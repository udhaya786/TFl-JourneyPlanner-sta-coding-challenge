using NUnit.Framework;
using Reqnroll;
using TflJourneyPlanner.Pages;
using TflJourneyPlanner.Support;

namespace TflJourneyPlanner.Steps;

[Binding]
public class PlanJourneySteps
{
    private readonly ScenarioContextHolder _ctx;
    private PlanJourneyPage Journey => _ctx.Journey;

    public PlanJourneySteps(ScenarioContextHolder ctx) => _ctx = ctx;

    // ─── Background ─────────────────────────────────────────────────

    [Given("I am on the TfL Plan a journey page")]
    public async Task GivenIAmOnThePage() => await Journey.OpenAsync();

    [Given("I have accepted cookies")]
    public async Task GivenIHaveAcceptedCookies() => await Journey.AcceptCookiesAsync();

    // ─── Entering locations ─────────────────────────────────────────

    [When("I enter {string} in the From field and select {string}")]
    public async Task WhenEnterFromAndSelect(string partial, string option) =>
        await Journey.SetFromViaAutocompleteAsync(partial, option);

    [When("I enter {string} in the To field and select {string}")]
    public async Task WhenEnterToAndSelect(string partial, string option) =>
        await Journey.SetToViaAutocompleteAsync(partial, option);

    [When("I enter {string} in the From field without selecting a suggestion")]
    public async Task WhenEnterFromNoSelect(string text)
    {
        await Journey.FromCombobox.ClickAsync();
        await Journey.FromCombobox.FillAsync(text);
    }

    [When("I enter {string} in the To field without selecting a suggestion")]
    public async Task WhenEnterToNoSelect(string text)
    {
        await Journey.ToCombobox.ClickAsync();
        await Journey.ToCombobox.FillAsync(text);
    }

    [When("I plan my journey")]
    public async Task WhenIPlanMyJourney() => await Journey.PlanJourneyAsync();

    [Given("I have planned a journey from {string} to {string}")]
    public async Task GivenIHavePlannedAJourney(string from, string to)
    {
        // TfL suggestions match on the typed prefix; first 3 chars mirrors the original tests
        await Journey.SetFromViaAutocompleteAsync(from[..3].ToLowerInvariant(), from);
        await Journey.SetToViaAutocompleteAsync(to[..3].ToLowerInvariant(), to);
        await Journey.PlanJourneyAsync();
        await Journey.OpenWalkingAndCyclingSectionAsync();
    }

    // ─── Results ────────────────────────────────────────────────────

    [Then("I should see the walking and cycling results")]
    public async Task ThenWalkingAndCyclingVisible() =>
        await Journey.ExpectWalkingAndCyclingVisibleAsync();

    [Then("the walking time should be displayed")]
    public async Task ThenWalkingTimeDisplayed()
    {
        var minutes = await Journey.GetWalkingMinutesAsync();
        Assert.That(minutes, Is.GreaterThan(0), "Walking time should be a positive number of minutes");
    }

    [Then("the cycling time should be displayed")]
    public async Task ThenCyclingTimeDisplayed()
    {
        var minutes = await Journey.GetCyclingMinutesAsync();
        Assert.That(minutes, Is.GreaterThan(0), "Cycling time should be a positive number of minutes");
    }

    // ─── Preferences ────────────────────────────────────────────────

    [When("I edit my preferences to routes with least walking")]
    public async Task WhenEditPreferences()
    {
        await Journey.OpenEditPreferencesAsync();
        await Journey.ChooseLeastWalkingRoutesAsync();
    }

    [When("I update the journey")]
    public async Task WhenUpdateJourney() => await Journey.SaveUpdatedJourneyAsync();

    [Then("the updated journey should be displayed")]
    public async Task ThenUpdatedJourneyDisplayed() =>
        await Journey.ExpectUpdatedJourneyAppliedAsync();

    [Then("the journey time should be displayed")]
    public async Task ThenJourneyTimeDisplayed()
    {
        var headingMinutes = await Journey.GetFirstOptionHeadingMinutesAsync();
        var totalMinutes = await Journey.GetFirstOptionTotalTimeMinutesAsync();

        Assert.That(headingMinutes, Is.GreaterThan(0), "Journey time should be a positive number of minutes");
        Assert.That(totalMinutes, Is.EqualTo(headingMinutes),
            "Option heading time and 'Total time' should agree");
    }

    // ─── Details / access info ──────────────────────────────────────

    [When("I view the details of the first journey option")]
    public async Task WhenViewDetails() => await Journey.OpenFirstOptionDetailsAsync();

    // TODO: extend beyond the heading to assert the actual access information content.
    [Then("the complete access information for {string} should be displayed")]
    public async Task ThenAccessInfoDisplayed(string station) =>
        await Journey.ExpectCoventGardenAccessInfoVisibleAsync();

    // ─── Error cases ────────────────────────────────────────────────

    [Then("I should see a message that no journey results were found")]
    public async Task ThenNoResults() => await Journey.ExpectInvalidJourneyErrorAsync();

    [Then("I should see the From and To required errors")]
    public async Task ThenRequiredErrors() =>
        await Journey.ExpectRequiredErrorsForEmptyJourneyAsync();
}
