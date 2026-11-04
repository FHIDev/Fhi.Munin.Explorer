using System.Net;
using System.Text.Json;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The code picker's calls (Fhi.Metadata-emsc0.3.7): search, tree, count, the chosen codes' write and read.</summary>
public class DesiredCodesClientTest
{
    private static readonly Guid Variable = new("bc8a6515-af36-44d0-aa10-b498813327b4");
    private static readonly Guid ListId = new("1f9d0b7e-3c14-4a2f-8f0b-2b6a4f1c9d31");
    private static readonly Guid ItemId = new("83ced574-6d32-447e-ad65-c54c63327476");
    private const string Codes = "/api/explorer/variables/bc8a6515-af36-44d0-aa10-b498813327b4/kodeverk/HelsefagligKodeverk/ICD-10/codes";

    private static MuninExplorerClient Client(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://munin.example/") });

    private const string Page = """
        {"kodeverkType":"HelsefagligKodeverk","kodeverkReference":"ICD-10","totalt":143,"skip":50,"take":50,
         "koder":[{"verdi":"C34","navn":"Ondartet svulst i bronkie og lunge","gyldigFra":null,"gyldigTil":null,"harUnderkoder":true},
                  {"verdi":"C34.0","navn":"Hovedbronkie","gyldigFra":"2010-01-01T00:00:00","gyldigTil":"2019-12-31T00:00:00","harUnderkoder":false}]}
        """;

    [Fact]
    public async Task SearchKodeverkCodesAsync_WhenTheApiAnswersAPage_ThenTheQueryIsEscapedAndThePageIsRead()
    {
        var handler = StubHttpHandler.Ok(Page);

        var page = await Client(handler).SearchKodeverkCodesAsync(Variable, "HelsefagligKodeverk", "ICD-10", "lunge & bronkie", 50, 50);

        Assert.Equal(Codes + "/search", handler.LastUri?.AbsolutePath);
        Assert.Equal("?q=lunge%20%26%20bronkie&skip=50&take=50", handler.LastUri?.Query);
        Assert.Equal(143, page!.Total);
        Assert.True(page.Codes[0].HasChildren);
        Assert.Equal(2019, page.Codes[1].ValidTo?.Year);
    }

    [Fact]
    public async Task SearchKodeverkCodesAsync_WhenTheLinkIsUnknown_ThenNull() =>
        Assert.Null(await Client(StubHttpHandler.Status(HttpStatusCode.NotFound))
            .SearchKodeverkCodesAsync(Variable, "HelsefagligKodeverk", "ICD-10", "C34"));

    [Theory]
    [InlineData(null, "?skip=0&take=50")]
    [InlineData("C34", "?node=C34&skip=0&take=50")]
    public async Task GetKodeverkCodeChildrenAsync_WhenANodeIsGivenOrNot_ThenTheTopLevelOrItsChildrenAreAsked(string? node, string query)
    {
        var handler = StubHttpHandler.Ok(Page);

        Assert.NotNull(await Client(handler).GetKodeverkCodeChildrenAsync(Variable, "HelsefagligKodeverk", "ICD-10", node));
        Assert.Equal(Codes + "/children", handler.LastUri?.AbsolutePath);
        Assert.Equal(query, handler.LastUri?.Query);
    }

    [Theory]
    [InlineData(DesiredCodeKinds.Code, "C34.0", null, "?kode=C34.0")]
    [InlineData(DesiredCodeKinds.Node, "C34", null, "?node=C34")]
    [InlineData(DesiredCodeKinds.Range, "C30", "C39", "?fra=C30&til=C39")]
    public async Task CountKodeverkCodesAsync_WhenEachKindIsCounted_ThenItsOwnParametersAreSent(string kind, string code, string? to, string query)
    {
        var handler = StubHttpHandler.Ok("""{"antall":17}""");

        var count = await Client(handler).CountKodeverkCodesAsync(Variable, new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", kind, code, to));

        Assert.Equal(17, count);
        Assert.Equal(Codes + "/count", handler.LastUri?.AbsolutePath);
        Assert.Equal(query, handler.LastUri?.Query);
    }

    [Fact]
    public async Task CountKodeverkCodesAsync_WhenTheCodeIsUnknown_ThenNull() =>
        Assert.Null(await Client(StubHttpHandler.Status(HttpStatusCode.NotFound))
            .CountKodeverkCodesAsync(Variable, new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Code, "X")));

    [Fact]
    public async Task SetMyListItemDesiredCodesAsync_WhenChoicesAreSent_ThenTheyTravelWithTheTypeAndTheText()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);
        var choices = new[]
        {
            new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Node, "C34"),
            new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Range, "C30", "C39"),
        };

        var result = await Client(handler).SetMyListItemDesiredCodesAsync(ListId, ItemId, " fra 2012 ", choices);

        Assert.Equal(DesiredDataOutcome.Saved, result.Outcome);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal($"/api/explorer/my/lists/{ListId}/items/{ItemId}/desired-data", handler.LastUri?.AbsolutePath);
        Assert.Equal(
            """{"type":"freeText","freeText":"fra 2012","selections":[{"kodeverkType":"HelsefagligKodeverk","kodeverkReference":"ICD-10","kind":"node","kode":"C34","til":null},{"kodeverkType":"HelsefagligKodeverk","kodeverkReference":"ICD-10","kind":"range","kode":"C30","til":"C39"}]}""",
            handler.LastBody);
    }

    [Fact]
    public async Task SetMyListItemDesiredCodesAsync_WhenOnlyCodesAreChosen_ThenTheTypeIsStillSent()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);

        await Client(handler).SetMyListItemDesiredCodesAsync(ListId, ItemId, null, [new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Code, "C34.0")]);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("freeText", body.RootElement.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("freeText").ValueKind);
        Assert.Equal(1, body.RootElement.GetProperty("selections").GetArrayLength());
    }

    [Fact]
    public async Task SetMyListItemDesiredCodesAsync_WhenEverythingIsCleared_ThenAnEmptyListClearsTheStoredChoices()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);

        await Client(handler).SetMyListItemDesiredCodesAsync(ListId, ItemId, "  ", []);

        Assert.Equal("""{"type":null,"freeText":null,"selections":[]}""", handler.LastBody);
    }

    [Fact]
    public async Task SetMyListItemDesiredDataAsync_WhenOnlyTextIsWritten_ThenNoSelectionsAreSentSoTheStoredOnesStay()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NoContent);

        await Client(handler).SetMyListItemDesiredDataAsync(ListId, ItemId, "C76");

        Assert.Equal("""{"type":"freeText","freeText":"C76"}""", handler.LastBody);
    }

    [Theory]
    [InlineData("""{"error":"Valg 3 finnes ikke","index":2}""", null, 2)]
    [InlineData("""{"error":"Høyst 200 valg","maxSelections":200,"received":201}""", 200, null)]
    public async Task SetMyListItemDesiredCodesAsync_WhenTheApiRefusesAChoice_ThenWhichOneOrTheCeilingComesBack(string refusal, int? max, int? index)
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.BadRequest, refusal);

        var result = await Client(handler).SetMyListItemDesiredCodesAsync(ListId, ItemId, null, [new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Code, "X")]);

        Assert.Equal(DesiredDataOutcome.Refused, result.Outcome);
        Assert.Equal(max, result.MaxSelections);
        Assert.Equal(index, result.InvalidIndex);
    }

    [Fact]
    public async Task SetMyListItemDesiredCodesAsync_WhenTheCodeListCannotBeReadNow_ThenListUnavailable()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.ServiceUnavailable, """{"error":"Kodelisten kan ikke leses nå."}""");

        var result = await Client(handler).SetMyListItemDesiredCodesAsync(ListId, ItemId, null, [new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Code, "C34")]);

        Assert.Equal(DesiredDataOutcome.ListUnavailable, result.Outcome);
    }

    [Fact]
    public async Task SetMyListItemDesiredDataAsync_WhenTheApiAnswers503WithoutCodes_ThenItIsAFaultAsBefore() =>
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(StubHttpHandler.Status(HttpStatusCode.ServiceUnavailable))
            .SetMyListItemDesiredDataAsync(ListId, ItemId, "C76"));

    [Fact]
    public async Task GetSharedListAsync_WhenItemsCarryChosenCodes_ThenTheyAreReadAsTextAndAMalformedOneCostsOnlyItself()
    {
        var handler = StubHttpHandler.Ok($$"""
            {"name":"Lunge","items":[{"variabelId":"{{Variable}}","desiredDataCodes":[
              {"kodeverkType":"HelsefagligKodeverk","kodeverkReference":"ICD-10","kind":"node","kode":"C34","navn":"<b>Lunge</b>","utgatt":false,"antall":12},
              {"kind":"range","kode":"C30","til":"C39","utgatt":"yes","antall":"many"},
              {"kind":"kode","kode":""},
              "C76",
              {"kind":"kode","kode":42}
            ]}]}
            """);

        var shared = await Client(handler).GetSharedListAsync("ab12cd34ef");

        var codes = Assert.Single(shared!.Items).DesiredDataCodes;
        Assert.Equal(3, codes.Count);
        Assert.Equal("<b>Lunge</b>", codes[0].Name);
        Assert.Equal(12, codes[0].Count);
        Assert.Equal(("C30", "C39", false, (int?)null), (codes[1].Code, codes[1].To, codes[1].Expired, codes[1].Count));
        Assert.Equal("42", codes[2].Code);
    }

    [Fact]
    public async Task GetSharedListAsync_WhenAClientPostedMoreThan200Codes_ThenOnly200AreKept()
    {
        var many = string.Join(",", Enumerable.Range(0, 250).Select(i => $$"""{"kind":"kode","kode":"K{{i}}"}"""));
        var handler = StubHttpHandler.Ok($$"""{"name":"x","items":[{"variabelId":"{{Variable}}","desiredDataCodes":[{{many}}]}]}""");

        var shared = await Client(handler).GetSharedListAsync("ab12cd34ef");

        Assert.Equal(200, Assert.Single(shared!.Items).DesiredDataCodes.Count);
    }

    [Fact]
    public void DesiredCodeView_WhenKept_ThenItGoesBackAsTheSameChoice() =>
        Assert.Equal(
            new DesiredCodeChoice("HelsefagligKodeverk", "ICD-10", DesiredCodeKinds.Range, "C30", "C39"),
            new DesiredCodeView { KodeverkType = "HelsefagligKodeverk", KodeverkReference = "ICD-10", Kind = "range", Code = "C30", To = "C39", Name = "x", Count = 9 }.ToChoice());
}
