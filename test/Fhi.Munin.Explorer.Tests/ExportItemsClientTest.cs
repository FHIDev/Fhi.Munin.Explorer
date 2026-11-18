using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The supported successor to the obsolete <c>ExportListAsync</c>: a browser list's rows, by (variable,
/// datasamling), through Munin's anonymous export (Fhi.Metadata-emsc0.3.12).
/// </summary>
public class ExportItemsClientTest
{
    private static readonly Guid Variable = new("b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90");
    private static readonly Guid Datasamling = new("3e5a8c11-7b42-49df-a6c8-1d904f2e6b73");

    private sealed class FileHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        public Uri? LastUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            var content = new ByteArrayContent([1, 2, 3]);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "Variabelliste.zip" };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private static IMuninExplorerClient Client(HttpMessageHandler handler) =>
        new MuninExplorerClient(new HttpClient(handler) { BaseAddress = new Uri("https://munin.example/") });

    [Fact]
    public async Task ExportItemsAsync_WhenAsked_ThenItPostsTheItemsUnderTheApisNamesAndKeepsItsFile()
    {
        // The wire names are Munin's ExportListRequest: a misspelt one binds to nothing and the API
        // exports by the ids alone, which loses the datasamling the reader chose.
        var handler = new FileHandler();

        var file = await Client(handler).ExportItemsAsync(
            [new VariableDatasamlingKey(Variable, Datasamling)], new Uri("https://helsedata.example/variabler"),
            ExportFormat.Csv, includeKodeverk: true);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/explorer/lists/export", handler.LastUri!.AbsolutePath);

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(Variable, item.GetProperty("variabelId").GetGuid());
        Assert.Equal(Datasamling, item.GetProperty("datasamlingId").GetGuid());
        Assert.Equal(Variable, Assert.Single(root.GetProperty("variabelIds").EnumerateArray()).GetGuid());
        Assert.Equal("csv", root.GetProperty("format").GetString());
        Assert.True(root.GetProperty("includeKodeverk").GetBoolean());
        Assert.Equal("https://helsedata.example/variabler", root.GetProperty("explorerPageUrl").GetString());

        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal("Variabelliste.zip", file.FileName);
    }

    [Fact]
    public async Task ExportItemsAsync_WhenOverTheApisCeiling_ThenItRefusesBeforeSending()
    {
        // Munin's MaxVariabelCount is 2000; a request past it costs a round trip for a 400.
        var handler = new FileHandler();
        var rows = Enumerable.Range(0, 2001).Select(_ => new VariableDatasamlingKey(Guid.NewGuid(), null)).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => Client(handler).ExportItemsAsync(rows));

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ExportItemsAsync_WhenARowHasNoDatasamling_ThenItCrossesAsNullRatherThanAnEmptyId()
    {
        // Munin refuses Guid.Empty as a datasamling with a 400, and reads a null as the variable's own.
        var handler = new FileHandler();

        await Client(handler).ExportItemsAsync([new VariableDatasamlingKey(Variable, null)]);

        using var body = JsonDocument.Parse(handler.LastBody!);
        var item = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("datasamlingId").ValueKind);
    }
}
