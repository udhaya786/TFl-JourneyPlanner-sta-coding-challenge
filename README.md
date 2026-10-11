# STA Coding Challenge – Web (TfL Journey Planner) and API (Open Library)

Automated tests written in **C# (.NET)** using **Playwright**, **Reqnroll** (Gherkin) and **NUnit**.

| Challenge | Target | Tests |
|---|---|---|
| Web | TfL "Plan a journey" widget – https://tfl.gov.uk/plan-a-journey | 5 scenarios (7 test cases, one is a 3-row outline) |
| API | Open Library Books API – `GET https://openlibrary.org/api/books` | 5 scenarios |

---

## Project structure

```
TflJourneyPlanner/
├── Features/
│   ├── PlanJourney.feature        # Web scenarios (@ui)
│   ├── OpenLibraryApi.feature     # API scenarios (@api)
│   └── AdditionalScenarios.feature  # Documented, not automated (@manual @ignore)
├── Steps/
│   ├── PlanJourneySteps.cs        # Step definitions – web
│   └── OpenLibraryApiSteps.cs     # Step definitions – API
├── Pages/
│   └── PlanJourneyPage.cs         # Page object for the Journey Planner
├── Support/
│   └── Hooks.cs                   # Browser lifecycle (@ui only), screenshot on failure
├── BaselineImages/                # Stored thumbnails used by the API image comparison
├── trx_to_html.py                 # Builds an HTML report from the test results
├── test-report.html               # Sample report from my last run
└── README.md
```

## Prerequisites

- .NET SDK (developed on .NET 10)
- PowerShell (`pwsh`) – only needed once to install the Playwright browser

## Setup and run

```bash
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium   # first time only

dotnet test                              # everything
dotnet test --filter "Category=ui"       # web tests only
dotnet test --filter "Category=api"      # API tests only
HEADED=1 dotnet test --filter "Category=ui"   # watch the browser
```

On failure the UI hook saves a full-page screenshot to `screenshots/`.

### HTML test report

```bash
dotnet test --logger trx --results-directory ./TestResults
python3 trx_to_html.py
open TestResults/test-report.html      # macOS; use xdg-open (Linux) or start (Windows)
```

`trx_to_html.py` turns the standard `.trx` results into a single self-contained HTML report, grouped by feature, with each Gherkin step shown as passed, failed or skipped. Each scenario carries UI/API and Functional/Non-functional tags (taken from the Gherkin tags), and the report can be filtered by them. Personal file paths are masked as `~`. A sample report from my last run is included as `test-report.html`.

---

## Development decisions

**Gherkin first.** Every test is a scenario in a `.feature` file, as required. Steps are written in business language ("I edit my preferences to routes with least walking") and the technical detail lives in the step and page classes.

**Page Object Model for the UI.** All locators and page interactions live in `PlanJourneyPage`. Step definitions only call intent-level methods, so a TfL markup change is fixed in one place.

**Tags split the two suites.** `@ui` scenarios launch a Playwright browser per scenario (isolated, no shared state); `@api` scenarios never start a browser and use a plain `HttpClient`.

**Locators.** Prefer role- and label-based locators (`GetByRole`, `GetByLabel`) because they mirror what a user or assistive technology sees. Where TfL exposes no accessible role (the "View details" button, the "Accessibility details" accordion) I fall back to the stable CSS class names observed on the live page.

**Autocomplete is used as the brief requires.** Locations are typed partially (`lei`, `cov`) and the option is selected from the suggestions.

**No silent passes.** Assertions fail loudly. Waits are on elements, not fixed sleeps.

**API image check.** The thumbnail URL from each response is downloaded and its SHA-256 compared with the baseline stored in `BaselineImages/`. This catches a changed cover as well as a missing one.

**Scenario outline for invalid input.** One scenario covers both locations invalid, and each location invalid on its own, since the brief says "1 or more invalid locations".

### Behaviour I discovered while building this

- For a short trip such as Leicester Square to Covent Garden (0.4 km), TfL offers only Walking and Cycling by default. Public transport options (the ones with "View details") appear only after setting the preference to **routes with least walking**, which is why the "View details" scenario sets that preference first.
- The first option is auto-expanded and its "View details" button is not inside the labelled option region, so it is located by its CSS class.
- The brief asks to verify "complete access information at Covent Garden". The page shows the Covent Garden leg in the detailed steps plus an **Accessibility details** panel (walking time, stairs, escalator, level walkway). The test asserts the Covent Garden step is shown and that the panel opens and lists the walking time and access items.

### Known limitations

- Tests run against the **live** TfL and Open Library sites, so they depend on the sites being available and on their markup and data not changing. Journey times and access wording vary by time and route, so assertions check structure and plausible values (positive minutes, heading time equals "Total time"), not hard-coded numbers.
- The response-time check (< 2000 ms) can be affected by network conditions.
- The baseline images are the small thumbnails returned today; if Open Library re-hosts or re-encodes them the comparison will fail and the baselines need refreshing.
- A small diagnostic helper in the page object (`DumpDetailsDiagnosticsAsync`) writes `details-diagnostics.txt` when the "View details" or access-information steps fail, to speed up diagnosis if TfL changes the page.

---

## Use of AI

**Disclaimer:** I used an AI assistant (Claude, by Anthropic) in a supporting role. The solution builds on my original TypeScript tests and my own choice of what to cover; the assistant helped me with:

- porting my TypeScript tests to C#,
- reviewing them against the brief to spot gaps,
- debugging failing locators against the live sites,
- drafting the HTML report script and parts of this README.

I ran all of the tests myself, reviewed the code, and I am responsible for the final result.

---

## Additional scenarios

As requested in the brief, further scenarios are documented in Gherkin in
[`Features/AdditionalScenarios.feature`](Features/AdditionalScenarios.feature). They are **not automated**:
each is tagged `@functional` or `@non-functional`, and the file is tagged `@manual @ignore`, so they appear
in the test report as "Not automated" and never fail a run.

| Area | Functional | Non-functional |
|---|---|---|
| TfL Journey Planner | Postcode/landmark input, same From/To, swap locations, Via, time options, preferences, step-free, mode filters, autocomplete, earlier/later journeys, disruptions, favourites, cookie consent | Accessibility (keyboard, screen reader, contrast, zoom), performance and slow network, responsive layout, cross-browser, unsafe input, service unavailable, peak load, privacy |
| Open Library API | `jscmd=data`, duplicate and malformed identifiers, unsupported HTTP methods | Schema and headers (caching, CORS), rate limiting |
