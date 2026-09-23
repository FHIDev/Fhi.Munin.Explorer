using System.Net;
using System.Text.Json;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The two share endpoints. A code is readable by anyone holding it and is opened by either
/// frontend, so the wire names and what is left out of the body are the contract (Fhi.Metadata-ntpbd.1).
/// </summary>
public class SharedListClientTest
{
    private static readonly Guid One = new("b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90");
    private static readonly Guid Two = new("3e5a8c11-7b42-49df-a6c8-1d904f2e6b73");
    private static readonly Guid Kilde = new("8ec4c2c4-662d-47a5-a946-f1086a014070");

    private static MuninExplorerClient Client(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://munin.example/") });

    private static VariableListItem Annotated(Guid id, string name) => new()
    {
        VariableId = id,
        AddedAt = DateTimeOffset.UtcNow,
        VariableCode = "V_" + name,
        VariableName = name,
        KildeId = Kilde,
        KildeName = "Als registeret",
        KildeShortName = "ALS",
        DatasamlingName = "Inklusjon",
        VariabelgruppeName = "Diagnose",
        DataType = "2",
        DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DataTo = new DateTimeOffset(2020, 12, 31, 0, 0, 0, TimeSpan.Zero),
        VersionStatus = "Publisert",
        DesiredDataType = "freeText",
        DesiredDataFreeText = "privat notat om hva jeg vil ha",
    };

    // -----------------------------------------------------------------------
    // ShareListAsync

    [Fact]
    public async Task ShareListAsync_WhenAListIsShared_ThenItPostsNameAndItemsUnderRunasNamesWithoutPrivateNotes()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.Created, """{"code":"AB12CD"}""");

        var code = await Client(handler).ShareListAsync("Hjerte", [Annotated(One, "Alder"), Annotated(Two, "Kjønn")]);

        Assert.Equal("AB12CD", code);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/explorer/lists/share", handler.LastUri?.AbsolutePath);

        using var sent = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("Hjerte", sent.RootElement.GetProperty("name").GetString());

        var items = sent.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);

        string[] expected =
        [
            "variabelId", "variabelCode", "variabelName", "kildeId", "kildeName", "kildeKortNavn",
            "datasamlingName", "variabelgruppeName", "dataType", "dataFrom", "dataTo", "versjonStatus"
        ];

        foreach (var item in items)
        {
            Assert.Equal(expected.Order(), item.EnumerateObject().Select(p => p.Name).Order());
            Assert.False(item.TryGetProperty("desiredDataFreeText", out _));
            Assert.False(item.TryGetProperty("desiredDataType", out _));
        }

        Assert.Equal(One.ToString(), items[0].GetProperty("variabelId").GetString());
        Assert.Equal("ALS", items[0].GetProperty("kildeKortNavn").GetString());
        Assert.DoesNotContain("privat notat", handler.LastBody);
    }

    [Fact]
    public async Task ShareListAsync_WhenTheApiRefuses_ThenItThrows()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.BadRequest, "\"Items must not be empty.\"");

        await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler).ShareListAsync("Tom", []));
    }

    // -----------------------------------------------------------------------
    // GetSharedListAsync

    [Fact]
    public async Task GetSharedListAsync_WhenTheCodeIsUnknown_ThenItAnswersNull()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.NotFound, "");

        var shared = await Client(handler).GetSharedListAsync("zz99zz");

        Assert.Null(shared);
        Assert.Equal(1, handler.Calls);

        // Case-insensitive on the API's side; sent upper-cased so a cache sees one code.
        Assert.Equal("/api/explorer/lists/share/ZZ99ZZ", handler.LastUri?.AbsolutePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABC12")]
    [InlineData("ABC1234")]
    [InlineData("AB-12C")]
    [InlineData("ÆØÅ123")]
    [InlineData("../x/y")]
    public async Task GetSharedListAsync_WhenTheCodeIsNotSixAsciiLettersOrDigits_ThenNothingIsSent(string? code)
    {
        var handler = StubHttpHandler.Ok("""{"name":"x","items":[]}""");

        var shared = await Client(handler).GetSharedListAsync(code);

        Assert.Null(shared);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GetSharedListAsync_WhenTheApiFails_ThenItThrows()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.InternalServerError, "");

        await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler).GetSharedListAsync("AB12CD"));
    }

    [Fact]
    public async Task GetSharedListAsync_WhenRateLimited_ThenItThrowsTheRateLimitException()
    {
        var handler = StubHttpHandler.Answering(HttpStatusCode.TooManyRequests, "");

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => Client(handler).GetSharedListAsync("AB12CD"));
    }

    /// <summary>
    /// What Runa's shareList posts and the API stores verbatim: its own VariableListItem names,
    /// dates as strings, a repeated id and an item with none. A code made in Runa opens here.
    /// </summary>
    [Fact]
    public async Task GetSharedListAsync_WhenTheSnapshotWasWrittenByRuna_ThenItReadsDeduplicatedItemsInOrder()
    {
        var handler = StubHttpHandler.Ok($$"""
            {
              "name": "  Runas hjerteliste  ",
              "items": [
                {
                  "variabelId": "{{One}}",
                  "variabelCode": "ALDER",
                  "variabelName": "Alder ved diagnose",
                  "kildeId": "{{Kilde}}",
                  "kildeName": "Als registeret",
                  "kildeKortNavn": "ALS",
                  "datasamlingName": null,
                  "variabelgruppeName": "Demografi",
                  "dataType": "2",
                  "dataFrom": "2010-01-01",
                  "dataTo": "2020-12-31T00:00:00",
                  "versjonStatus": "Publisert"
                },
                { "variabelCode": "UTEN_ID", "variabelName": "Mangler id" },
                { "variabelId": "ikke-en-guid", "variabelName": "Ugyldig id" },
                { "variabelId": "{{One}}", "variabelName": "Duplikat" },
                { "variabelId": "{{Two}}", "dataFrom": "ikke en dato" }
              ]
            }
            """);

        var shared = await Client(handler).GetSharedListAsync("ab12cd");

        Assert.NotNull(shared);
        Assert.Equal("Runas hjerteliste", shared.Name);
        Assert.Equal([One, Two], shared.Items.Select(i => i.VariableId));

        var first = shared.Items[0];
        Assert.Equal("Alder ved diagnose", first.VariableName);
        Assert.Equal("ALDER", first.VariableCode);
        Assert.Equal(Kilde, first.KildeId);
        Assert.Equal("ALS", first.KildeShortName);
        Assert.Null(first.DatasamlingName);
        Assert.Equal("Demografi", first.VariabelgruppeName);
        Assert.Equal("2", first.DataType);
        Assert.Equal(2010, first.DataFrom?.Year);
        Assert.Equal(2020, first.DataTo?.Year);
        Assert.Equal("Publisert", first.VersionStatus);

        // Missing display fields and an unreadable date cost the field, not the item.
        Assert.Null(shared.Items[1].VariableName);
        Assert.Null(shared.Items[1].DataFrom);
    }

    /// <summary>Runa's cloneListFromSnapshot still reads the older ids-only snapshot, so this does too.</summary>
    [Fact]
    public async Task GetSharedListAsync_WhenTheSnapshotCarriesOnlyVariableIds_ThenTheyAreReadInOrderOnce()
    {
        var handler = StubHttpHandler.Ok($$"""
            {"name":"Eldre liste","variableIds":["{{Two}}","ikke-en-guid","{{One}}","{{Two}}",42]}
            """);

        var shared = await Client(handler).GetSharedListAsync("AB12CD");

        Assert.NotNull(shared);
        Assert.Equal([Two, One], shared.Items.Select(i => i.VariableId));
        Assert.All(shared.Items, i => Assert.Null(i.VariableName));
    }

    [Fact]
    public async Task GetSharedListAsync_WhenTheSnapshotHasNoName_ThenTheNameIsEmpty()
    {
        var handler = StubHttpHandler.Ok($$"""{"name":null,"items":[{"variabelId":"{{One}}"}]}""");

        var shared = await Client(handler).GetSharedListAsync("AB12CD");

        Assert.NotNull(shared);
        Assert.Equal("", shared.Name);
        Assert.Single(shared.Items);
    }
}
