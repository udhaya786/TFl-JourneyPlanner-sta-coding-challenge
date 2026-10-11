using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;
using Reqnroll;

namespace TflJourneyPlanner.Steps;

/// <summary>Per-scenario state for the API tests (injected by Reqnroll).</summary>
public class ApiContext : IDisposable
{
    public HttpClient Http { get; set; } = default!;
    public string Identifiers { get; set; } = "";
    public HttpResponseMessage Response { get; set; } = default!;
    public JsonElement Body { get; set; }
    public long ResponseTimeMs { get; set; }

    public void Dispose()
    {
        Response?.Dispose();
        Http?.Dispose();
    }
}

[Binding]
public class OpenLibraryApiSteps
{
    private const string BaseUrl = "https://openlibrary.org/api/books";
    private readonly ApiContext _api;

    public OpenLibraryApiSteps(ApiContext api) => _api = api;

    // ─── GIVEN ──────────────────────────────────────────────────────

    [Given("the OpenLibrary Books API is available")]
    public void GivenApiAvailable() =>
        _api.Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    [Given("I have book identifiers {string}")]
    public void GivenIdentifiers(string ids) => _api.Identifiers = ids;

    // ─── WHEN ───────────────────────────────────────────────────────

    [When("I send a GET request to the OpenLibrary Books API for those identifiers")]
    public async Task WhenSendGet()
    {
        var url = $"{BaseUrl}?bibkeys={_api.Identifiers}&format=json";

        var sw = Stopwatch.StartNew();
        _api.Response = await _api.Http.GetAsync(url);
        var json = await _api.Response.Content.ReadAsStringAsync();
        sw.Stop();
        _api.ResponseTimeMs = sw.ElapsedMilliseconds;

        _api.Body = JsonDocument.Parse(json).RootElement.Clone();
    }

    // ─── THEN ───────────────────────────────────────────────────────

    [Then("the response code should be {int}")]
    public void ThenStatus(int expected) =>
        Assert.That((int)_api.Response.StatusCode, Is.EqualTo(expected));

    [Then("the response time should be less than {int} milliseconds")]
    public void ThenTime(int max) =>
        Assert.That(_api.ResponseTimeMs, Is.LessThan(max),
            $"Response took {_api.ResponseTimeMs} ms");

    [Then("the response content type should be JSON")]
    public void ThenContentType() =>
        Assert.That(_api.Response.Content.Headers.ContentType?.MediaType,
            Does.Contain("json").IgnoreCase);

    [Then("the number of returned books should be {int}")]
    public void ThenCount(int expected) =>
        Assert.That(_api.Body.EnumerateObject().Count(), Is.EqualTo(expected));

    [Then("the response should contain correct details for each requested identifier")]
    public void ThenDetailsForAll()
    {
        foreach (var key in _api.Identifiers.Split(',', StringSplitOptions.RemoveEmptyEntries))
            AssertBookDetails(key);
    }

    [Then("the response should contain correct details for identifier {string}")]
    public void ThenDetailsForOne(string key) => AssertBookDetails(key);

    [Then("each returned book should include a thumbnail image URL")]
    public void ThenThumbnailUrls()
    {
        foreach (var book in _api.Body.EnumerateObject())
        {
            var url = book.Value.GetProperty("thumbnail_url").GetString();
            Assert.That(url, Is.Not.Null.And.Not.Empty, $"No thumbnail for {book.Name}");
            Assert.That(url, Does.Contain("covers.openlibrary.org"));
        }
    }

    [Then("the thumbnail images should match the stored baseline images in the repository")]
    public async Task ThenThumbnailsMatchBaseline()
    {
        foreach (var book in _api.Body.EnumerateObject())
        {
            var thumbnailUrl = book.Value.GetProperty("thumbnail_url").GetString()!;

            var imageResponse = await _api.Http.GetAsync(thumbnailUrl); // follows redirects
            Assert.That((int)imageResponse.StatusCode, Is.EqualTo(200),
                $"Could not download thumbnail for {book.Name}");
            var apiBytes = await imageResponse.Content.ReadAsByteArrayAsync();

            // "ISBN:0201558025" -> "ISBN_0201558025.jpg"
            var fileName = book.Name.Replace(':', '_') + ".jpg";
            var baselinePath = Path.Combine(AppContext.BaseDirectory, "BaselineImages", fileName);

            Assert.That(File.Exists(baselinePath), Is.True,
                $"Baseline image missing: {baselinePath}");

            var baselineBytes = await File.ReadAllBytesAsync(baselinePath);

            Assert.That(Convert.ToHexString(SHA256.HashData(apiBytes)),
                Is.EqualTo(Convert.ToHexString(SHA256.HashData(baselineBytes))),
                $"Thumbnail for {book.Name} differs from baseline");
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private void AssertBookDetails(string key)
    {
        Assert.That(_api.Body.TryGetProperty(key, out var book), Is.True,
            $"'{key}' missing from response");

        Assert.That(book.GetProperty("bib_key").GetString(), Is.EqualTo(key));
        Assert.That(book.GetProperty("info_url").GetString(), Does.Contain("openlibrary.org"));
    }
}
