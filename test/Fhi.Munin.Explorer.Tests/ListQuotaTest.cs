using System.Net;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Munin's list quota 409 read as <see cref="MuninExplorerListQuotaException"/>, other 409s as before, and the
/// sentence a reader is told (Fhi.Metadata-oiips.61).</summary>
public class ListQuotaTest
{
    private static readonly Guid ListId = new("1f9d0b7e-3c14-4a2f-8f0b-2b6a4f1c9d31");

    private const string ListLimit = """{"error":"Du kan ha høyst 50 lister. Slett en liste før du lager en ny.","code":"list-limit","limit":50}""";
    private const string RowLimit = """{"error":"Your lists can hold at most 100,000 rows together. Remove some before you add more.","code":"row-limit","limit":100000}""";

    private sealed class FixedTokenProvider : IMuninExplorerTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("the-hosts-token");
    }

    private static MuninExplorerClient Client(StubHttpHandler handler) =>
        new(new HttpClient(new BearerTokenHandler(new FixedTokenProvider()) { InnerHandler = handler })
        {
            BaseAddress = new Uri("https://runa.munin.skytest.fhi.no/")
        });

    private static Task ListWriteAsync(MuninExplorerClient client, string call) => call switch
    {
        "create" => client.CreateMyListAsync("Min liste"),
        "variables" => client.AddVariablesToMyListAsync(ListId, [Guid.NewGuid()]),
        "items" => client.AddItemsToMyListAsync(ListId, [new VariableDatasamlingKey(Guid.NewGuid(), Guid.NewGuid())]),
        _ => throw new ArgumentOutOfRangeException(nameof(call), call, null)
    };

    [Theory]
    [InlineData("create")]
    [InlineData("variables")]
    [InlineData("items")]
    public async Task MuninExplorerClient_WhenAListWriteIsAnsweredWithTheListLimit_ThenTheQuotaIsThrownWithItsKindLimitAndText(string call)
    {
        var refused = await Assert.ThrowsAsync<MuninExplorerListQuotaException>(
            () => ListWriteAsync(Client(StubHttpHandler.Answering(HttpStatusCode.Conflict, ListLimit)), call));

        Assert.Equal(ListQuotaKind.Lists, refused.Kind);
        Assert.Equal(50, refused.Limit);
        Assert.Equal("Du kan ha høyst 50 lister. Slett en liste før du lager en ny.", refused.ApiText);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("variables")]
    [InlineData("items")]
    public async Task MuninExplorerClient_WhenAListWriteIsAnsweredWithTheRowLimit_ThenTheQuotaIsThrownAsRows(string call)
    {
        var refused = await Assert.ThrowsAsync<MuninExplorerListQuotaException>(
            () => ListWriteAsync(Client(StubHttpHandler.Answering(HttpStatusCode.Conflict, RowLimit)), call));

        Assert.Equal((ListQuotaKind.Rows, 100_000), (refused.Kind, refused.Limit));
    }

    // Each body is a 409 that is not the quota, so each must stay the plain exception a host already catches.
    [Theory]
    [InlineData("create", """{"error":"x","code":"something-else","limit":50}""")]
    [InlineData("variables", """{"error":"x","code":"row-limit"}""")]
    [InlineData("items", """{"error":"x","code":"list-limit","limit":0}""")]
    [InlineData("create", """{"error":"x"}""")]
    [InlineData("variables", "<html>Conflict</html>")]
    [InlineData("items", "")]
    public async Task MuninExplorerClient_WhenAListWritesConflictIsNotTheQuota_ThenItIsThrownAsBefore(string call, string body)
    {
        var refused = await Assert.ThrowsAsync<HttpRequestException>(
            () => ListWriteAsync(Client(StubHttpHandler.Answering(HttpStatusCode.Conflict, body)), call));

        Assert.IsNotType<MuninExplorerListQuotaException>(refused);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
    }

    [Fact]
    public async Task CreateMyListAsync_WhenAQuotaBodyComesWithAnotherStatus_ThenItIsNotReadAsTheQuota()
    {
        var refused = await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(StubHttpHandler.Answering(HttpStatusCode.BadRequest, ListLimit)).CreateMyListAsync("Min liste"));

        Assert.IsNotType<MuninExplorerListQuotaException>(refused);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task AddVariablesToMyListAsync_WhenTheListIsNotTheReaders_ThenItIsStillFalse()
    {
        Assert.False(await Client(StubHttpHandler.Status(HttpStatusCode.NotFound)).AddVariablesToMyListAsync(ListId, [Guid.NewGuid()]));
    }

    [Theory]
    [InlineData("""{"error":"  ","code":"row-limit","limit":5}""")]
    [InlineData("""{"code":"row-limit","limit":5}""")]
    public async Task CreateMyListAsync_WhenTheQuotaCarriesNoText_ThenApiTextIsNull(string body)
    {
        var refused = await Assert.ThrowsAsync<MuninExplorerListQuotaException>(
            () => Client(StubHttpHandler.Answering(HttpStatusCode.Conflict, body)).CreateMyListAsync("Min liste"));

        Assert.Null(refused.ApiText);
    }

    [Theory]
    [InlineData("no")]
    [InlineData("en")]
    public void ListQuota_WhenTheKindIsNoneMuninNames_ThenTheReaderGetsTheSaveErrorRatherThanAWrongLimit(string language)
    {
        var texts = Texts.For(language);

        Assert.Equal(texts.SaveError, texts.ListQuota(new MuninExplorerListQuotaException(default, 50, apiText: null)));
    }

    // Worded as Munin's MyListsController words its 409, so the reader reads the same sentence either side.
    [Theory]
    [InlineData("no", ListQuotaKind.Lists, 50, "Du kan ha høyst 50 lister. Slett en liste før du lager en ny.")]
    [InlineData("en", ListQuotaKind.Lists, 50, "You can have at most 50 lists. Delete a list before you make a new one.")]
    [InlineData("no", ListQuotaKind.Rows, 100_000, "Listene dine kan til sammen ha høyst 100 000 rader. Fjern noen før du legger til flere.")]
    [InlineData("en", ListQuotaKind.Rows, 100_000, "Your lists can hold at most 100,000 rows together. Remove some before you add more.")]
    public void ListQuota_WhenAWriteIsRefused_ThenTheReaderIsToldInTheirLanguageWithTheLimit(
        string language, ListQuotaKind kind, int limit, string expected)
    {
        Assert.Equal(expected, Texts.For(language).ListQuota(new MuninExplorerListQuotaException(kind, limit, apiText: null)));
    }
}
