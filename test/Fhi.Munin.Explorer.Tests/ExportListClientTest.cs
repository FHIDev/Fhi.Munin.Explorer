using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

// The ExportListAsync tests below call the obsolete member on purpose; they stay until 3.0.0 removes it.
#pragma warning disable CS0618

/// <summary>
/// The export call. Its answer is a file rather than a payload, so what matters is that the name
/// and the type come back from the API rather than being composed here — asking for CSV with
/// codebooks answers with a zip, and a caller that built the name itself would offer a .csv that
/// is not one.
/// </summary>
public class ExportListClientTest
{
    private const string Route = "/api/explorer/lists/export";

    private static readonly Guid One = new("b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90");
    private static readonly Guid Two = new("3e5a8c11-7b42-49df-a6c8-1d904f2e6b73");

    /// <summary>Answers with a file, and remembers what it was asked for.</summary>
    private sealed class FileHandler(string contentType, string fileName, byte[]? body = null)
        : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public Uri? LastUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public TimeSpan? LastLimit { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastLimit = request.Options.TryGetValue(RequestTimeoutHandler.Limit, out var limit) ? limit : null;
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            var content = new ByteArrayContent(body ?? [1, 2, 3, 4]);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = fileName
            };

            return new HttpResponseMessage(Status) { Content = content };
        }
    }

    private static IMuninExplorerClient Client(HttpMessageHandler handler) =>
        new MuninExplorerClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://munin.example/")
        });

    [Fact]
    public async Task ExportVariablesAsync_WhenAsked_ThenItGetsTheSearchAndFiltersAndKeepsTheApisFile()
    {
        var handler = new FileHandler("application/zip", "Variabler_2026-10-09.zip");
        var filter = new VariableFilter { KildeIds = [One] };

        var file = await Client(handler).ExportVariablesAsync("alder", filter, ExportFormat.Csv, includeKodeverk: true);

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("/api/explorer/variables/export", handler.LastUri!.AbsolutePath);
        var query = handler.LastUri.Query;
        Assert.Contains("search=alder", query);
        Assert.Contains("format=csv", query);
        Assert.Contains("includeKodeverk=true", query);
        Assert.Contains($"kildeIds={One}", query);
        Assert.Null(handler.LastBody);
        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal("Variabler_2026-10-09.zip", file.FileName);
    }

    [Fact]
    public async Task ExportVariablesAsync_WhenSent_ThenItAsksForLongerThanTheThirtySecondsOtherCallsGet()
    {
        var handler = new FileHandler("text/csv", "Variabler_2026-10-09.csv");

        await Client(handler).ExportVariablesAsync(null, null);

        Assert.Equal(TimeSpan.FromSeconds(150), handler.LastLimit);
    }

    [Fact]
    public async Task ExportMyListAsync_WhenSent_ThenItKeepsTheOrdinaryLimit()
    {
        var handler = new FileHandler("text/csv", "liste.csv");

        await Client(handler).ExportMyListAsync(One);

        Assert.Null(handler.LastLimit);
    }

    [Fact]
    public async Task ExportVariablesAsync_WhenCodebooksAreNotAskedFor_ThenNoIncludeKodeverkIsSent()
    {
        var handler = new FileHandler("text/csv", "Variabler_2026-10-09.csv");

        await Client(handler).ExportVariablesAsync(null, null, ExportFormat.Xlsx);

        Assert.DoesNotContain("includeKodeverk", handler.LastUri!.Query);
        Assert.Contains("format=xlsx", handler.LastUri.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ExportVariablesAsync_WhenTheApiRefuses_ThenItThrows(HttpStatusCode status)
    {
        var handler = new FileHandler("text/plain", "x") { Status = status };

        await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler).ExportVariablesAsync(null, null));
    }

    [Fact]
    public async Task ExportVariablesAsync_WhenRateLimited_ThenItThrowsTheRateLimitException()
    {
        var handler = new FileHandler("text/plain", "x") { Status = HttpStatusCode.TooManyRequests };

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => Client(handler).ExportVariablesAsync(null, null));
    }

    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExportListAsync_WhenAskedForExcel_ThenItPostsTheIdsToTheExportRoute()
    {
        var handler = new FileHandler(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "variabelliste-2026-08-26-141530.xlsx");

        await Client(handler).ExportListAsync([One, Two]);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(Route, handler.LastUri?.AbsolutePath);

        // The wire spells it variabelIds, with the Norwegian stem the rest of this API uses.
        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(2, sent.RootElement.GetProperty("variabelIds").GetArrayLength());
    }

    [Fact]
    public async Task ExportListAsync_WhenTheApiAnswers_ThenTheNameAndTypeAreTheApisOwn()
    {
        var handler = new FileHandler(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "variabelliste-2026-08-26-141530.xlsx");

        var file = await Client(handler).ExportListAsync([One]);

        Assert.Equal("variabelliste-2026-08-26-141530.xlsx", file.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.Equal([1, 2, 3, 4], file.Bytes);
    }

    [Fact]
    public async Task ExportListAsync_WhenCodebooksAreAskedFor_ThenTheFlagIsSent()
    {
        // Only the flag is asserted here. Whether CSV-with-codebooks actually answers as a zip is
        // the API's behaviour, and a mock that returns whatever the test handed it cannot prove it
        // — an earlier version of this test asserted the zip and proved nothing but its own setup.
        var handler = new FileHandler("application/zip", "variabelliste-2026-08-26-141530.zip");

        await Client(handler).ExportListAsync([One], ExportFormat.Csv, includeKodeverk: true);

        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.True(sent.RootElement.GetProperty("includeKodeverk").GetBoolean());
    }

    [Theory]
    // Lowercase, because that is what the API accepts. It spells the two out with
    // [JsonStringEnumMemberName], and rejects "Csv" with a 400. This test asserted the PascalCase
    // names until 2026-08-27 and passed the whole time, while every real download failed.
    [InlineData(ExportFormat.Xlsx, "xlsx")]
    [InlineData(ExportFormat.Csv, "csv")]
    public async Task ExportListAsync_WhenAFormatIsChosen_ThenItIsSentAsTheWireNameTheApiAccepts(
        ExportFormat format, string expected)
    {
        var handler = new FileHandler("text/csv", "variabelliste.csv");

        await Client(handler).ExportListAsync([One], format);

        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(expected, sent.RootElement.GetProperty("format").GetString());
    }

    [Fact]
    public async Task ExportListAsync_WhenAKildeFilterIsGiven_ThenItIsSent()
    {
        var handler = new FileHandler("text/csv", "variabelliste.csv");
        var kilde = Guid.NewGuid();

        await Client(handler).ExportListAsync([One], ExportFormat.Csv, kildeIdFilter: kilde);

        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(kilde, sent.RootElement.GetProperty("kildeIdFilter").GetGuid());
    }

    [Fact]
    public async Task ExportListAsync_WhenTheApiFails_ThenItThrowsRatherThanAnsweringWithAnEmptyFile()
    {
        // Not mapped to an empty result the way a missing variable is mapped to null: a caller that
        // handed the reader a zero-byte file for a 500 would be lying about what happened.
        var handler = new FileHandler("text/csv", "x.csv") { Status = HttpStatusCode.InternalServerError };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(handler).ExportListAsync([One]));
    }

    [Fact]
    public async Task ExportListAsync_WhenTheApiRateLimits_ThenItThrowsItsOwnExceptionRatherThanTheGenericOne()
    {
        // Told apart from a fault the way the my/lists writes are: a caller that cannot tell them
        // apart has no cause to name, and reports a throttled export as a broken one.
        var handler = StubHttpHandler.RateLimited(TimeSpan.FromSeconds(30));

        var refused = await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => Client(handler).ExportListAsync([One]));

        Assert.Equal(TimeSpan.FromSeconds(30), refused.RetryAfter);

        // No retry of its own, for the same reason none of the other calls get one.
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ExportListAsync_WhenTheApiSendsNoFileName_ThenAPlainOneIsUsedRatherThanNothing()
    {
        var handler = new NoDispositionHandler();

        var file = await Client(handler).ExportListAsync([One]);

        Assert.False(string.IsNullOrWhiteSpace(file.FileName));
    }

#pragma warning restore CS0618

    // ---- ExportMyListAsync (Fhi.Metadata-fiht4): the saved list's own export, which carries "Ønskede data" ----

    private static readonly Guid MyListId = new("5f0c2e7a-91b4-4d3e-8a6f-2c7d1b9e4a30");

    [Fact]
    public async Task ExportMyListAsync_WhenAsked_ThenItPostsToTheListsOwnExportRouteWithTheChoices()
    {
        var handler = new FileHandler("application/zip", "variabelliste.zip");

        var file = await Client(handler).ExportMyListAsync(MyListId, ExportFormat.Csv, includeKodeverk: true, kildeIds: [One, Two]);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/explorer/my/lists/{MyListId}/export", handler.LastUri?.AbsolutePath);
        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("csv", sent.RootElement.GetProperty("format").GetString());
        Assert.True(sent.RootElement.GetProperty("includeKodeverk").GetBoolean());
        Assert.Equal([One, Two], sent.RootElement.GetProperty("kildeIds").EnumerateArray().Select(e => e.GetGuid()));

        // No ids in the body: the API reads the list, which is what lets it fill in "Ønskede data".
        Assert.False(sent.RootElement.TryGetProperty("variabelIds", out _));
        Assert.Equal("application/zip", file!.ContentType);
        Assert.Equal("variabelliste.zip", file.FileName);
    }

    [Fact]
    public async Task ExportMyListAsync_WithNoKildeNarrowing_ThenKildeIdsIsNull()
    {
        var handler = new FileHandler("text/csv", "variabelliste.csv");

        await Client(handler).ExportMyListAsync(MyListId, ExportFormat.Xlsx, kildeIds: []);

        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(JsonValueKind.Null, sent.RootElement.GetProperty("kildeIds").ValueKind);
        Assert.Equal("xlsx", sent.RootElement.GetProperty("format").GetString());
    }

    [Fact]
    public async Task ExportMyListAsync_WhenTheListIsGone_ThenItAnswersNull()
    {
        var handler = new FileHandler("text/plain", "x") { Status = HttpStatusCode.NotFound };

        Assert.Null(await Client(handler).ExportMyListAsync(MyListId));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ExportMyListAsync_WhenTheApiDeclinesTheCaller_ThenItThrowsTheUnauthorisedType(HttpStatusCode status)
    {
        var handler = new FileHandler("text/plain", "x") { Status = status };

        await Assert.ThrowsAsync<MuninExplorerUnauthorizedException>(() => Client(handler).ExportMyListAsync(MyListId));
    }

    [Fact]
    public async Task ExportMyListAsync_WhenTheApiRateLimits_ThenItThrowsItsOwnException()
    {
        var handler = StubHttpHandler.RateLimited(TimeSpan.FromSeconds(30));

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => Client(handler).ExportMyListAsync(MyListId));
    }

    private sealed class NoDispositionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StringContent("x", Encoding.UTF8, "text/csv");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
