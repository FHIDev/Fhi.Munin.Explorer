using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
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
    [InlineData(ExportFormat.Xlsx, "Xlsx")]
    [InlineData(ExportFormat.Csv, "Csv")]
    public async Task ExportListAsync_WhenAFormatIsChosen_ThenItIsSentAsItsNameNotItsNumber(
        ExportFormat format, string expected)
    {
        // The API reads enums as PascalCase strings, and this package serialises with
        // JsonSerializerDefaults.Web, which has no string-enum converter — so an enum sent as
        // itself goes out as 0 or 1. If the API then falls back to its default, a reader asking for
        // CSV is handed an xlsx, and nothing anywhere says so.
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
    public async Task ExportListAsync_WhenTheApiSendsNoFileName_ThenAPlainOneIsUsedRatherThanNothing()
    {
        var handler = new NoDispositionHandler();

        var file = await Client(handler).ExportListAsync([One]);

        Assert.False(string.IsNullOrWhiteSpace(file.FileName));
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
