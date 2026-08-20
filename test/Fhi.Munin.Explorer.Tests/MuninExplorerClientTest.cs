using System.Net;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The client against a stubbed transport: does a real API response round-trip into the
/// contracts, and does a missing resource come back as "nothing" rather than an exception.
/// </summary>
public class MuninExplorerClientTest
{
    private const string DefaultBaseAddress = "https://munin.skytest.fhi.no/";

    private static MuninExplorerClient Client(HttpMessageHandler handler, string baseAddress = DefaultBaseAddress) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(baseAddress) });

    private static MuninExplorerClient WithResponse(string fixture, out StubHttpHandler handler)
    {
        handler = StubHttpHandler.Ok(TestData.Read(fixture));
        return Client(handler);
    }

    private static MuninExplorerClient WithStatus(HttpStatusCode status) => Client(StubHttpHandler.Status(status));

    // ---------------------------------------------------------------- round-trip of real payloads

    [Fact]
    public async Task SearchVariablesAsync_WhenTheApiAnswersWithARealResponse_ThenThePageAndItsRowsAreRead()
    {
        var page = await WithResponse("variables.json", out _).SearchVariablesAsync(null);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(18289, page.TotalCount);
        Assert.Equal(9145, page.TotalPages);
        Assert.Equal("V_ALS.F1.ALSFRSR1TALE", page.Items[0].Code);
    }

    [Fact]
    public async Task GetFiltersAsync_WhenTheApiAnswersWithARealResponse_ThenEveryFacetIsRead()
    {
        var filters = await WithResponse("filters.json", out _).GetFiltersAsync();

        Assert.Equal(3, filters.KildeTyper.Count);
        Assert.Equal("befolkningsbasertHelseundersokelse", filters.KildeTyper[0].Value);
        Assert.Equal(41, filters.Kilder.Count);
        Assert.Equal(20, filters.Instruments.Count);
        Assert.Equal(3876, filters.KildeKodeverkCount);
        Assert.Equal(18289, filters.TotalCount);

        // Datatypes arrive as bare codes with no label — the point of the note on DataTypeFacet.
        Assert.Equal(["1", "10", "2", "3", "4", "6", "7"], filters.DataTypes.Select(d => d.Value));

        // Datakategorier are raw EHDS tokens, label and all, so a caller matches whole tokens
        // rather than stripping the prefix off them.
        Assert.Equal("ehds-cat:biobanks", filters.DataCategories[0].Value);
        Assert.Equal(38, filters.DataCategories[1].Count);

        // A root-level variabelgruppe has no parent; the delkilde facet carries its kilde.
        Assert.Null(filters.Variabelgrupper[0].ParentId);
        Assert.NotEqual(Guid.Empty, filters.Delkilder[0].KildeId);

        // Kodeverk without a resolved name is expected, not a parse failure.
        Assert.Equal("3402", filters.AdministrativtKodeverk[0].Oid);

        // Only the lower bound is known in the test environment.
        Assert.Equal(1868, filters.DateRange?.Min?.Year);
        Assert.Null(filters.DateRange?.Max);
    }

    [Fact]
    public async Task GetKilderAsync_WhenTheApiAnswersWithARealResponse_ThenTheSummaryListIsRead()
    {
        var kilder = await WithResponse("kilder.json", out _).GetKilderAsync();

        Assert.Equal(3, kilder.Count);

        var als = kilder[0];
        Assert.Equal("K_ALS", als.Code);
        Assert.Equal("Als registeret", als.Name);
        Assert.Equal("nasjonaltMedisinskKvalitetsregister", als.Kildetype);
        Assert.True(als.IsActive);
        Assert.True(als.HasVariableDescription);
        Assert.Equal(9, als.DatasamlingCount);
        Assert.Equal(230, als.TotalVariables);
        Assert.Null(als.HealthDcatScore); // never computed yet — see the note on the property
        Assert.Equal("alsregister@stolav.no", als.AdditionalProperties["Epost"]);
    }

    [Fact]
    public async Task GetKildeAsync_WhenTheApiAnswersWithARealResponse_ThenTheDetailAndItsDatasamlingerAreRead()
    {
        var kilde = await WithResponse("kilde.json", out _).GetKildeAsync(Guid.NewGuid());

        Assert.NotNull(kilde);
        Assert.Equal("K_ALS", kilde.Code);
        Assert.Equal("Als registeret", kilde.PreferredTerm);
        Assert.Equal(230, kilde.TotalVariables);
        Assert.Equal(9, kilde.Datasamlinger.Count);
        Assert.Equal(72, kilde.PropertyMetadata.Count);
        Assert.Null(kilde.DataTo); // ongoing collection

        // Inheritance: no own data controller, but an effective one resolved from the kilde.
        var inklusjon = kilde.Datasamlinger[0];
        Assert.Equal("Inklusjon", inklusjon.Name);
        Assert.Equal(1, inklusjon.PresentationOrder);
        Assert.Null(inklusjon.DataController);
        Assert.Equal("St. Olavs hospital HF", inklusjon.EffectiveDataController);
        Assert.Equal("nasjonaltMedisinskKvalitetsregister", inklusjon.EffectiveKildetype);

        // Property metadata is what makes the free-form bag renderable.
        var code = kilde.PropertyMetadata.First(p => p.Key == "Code");
        Assert.Equal("Kode", code.DisplayNameTranslations["no"]);
        Assert.Empty(code.Options); // a String property has no options to choose between

        // A SingleSelect does, and they arrive parsed and language-resolved, which is why a caller
        // is told to prefer them over picking OptionsJson apart itself.
        var kildetype = kilde.PropertyMetadata.First(p => p.Key == "Kildetype");
        Assert.Equal(8, kildetype.Options.Count);
        Assert.Equal("sentraltHelseregister", kildetype.Options[0].Value);
        Assert.Equal("Sentralt helseregister", kildetype.Options[0].DisplayName);
    }

    [Fact]
    public async Task GetKildeAsync_WhenTheKildeHasDelkilder_ThenTheDelkildeTreeAndItsDatasamlingerAreRead()
    {
        var kilde = await WithResponse("kilde-med-delkilder.json", out _).GetKildeAsync(Guid.NewGuid());

        Assert.NotNull(kilde);
        Assert.Equal("The Tromsø study", kilde.PreferredTerm);
        Assert.Equal(5, kilde.Delkilder.Count);
        Assert.Equal(3, kilde.Datasamlinger.Count); // hanging directly off the kilde

        var tromso4 = kilde.Delkilder.First(d => d.Code == "K_TR.TR4");
        Assert.Equal(2, tromso4.Datasamlinger.Count);
        Assert.Empty(tromso4.Children);

        // A delkilde's datasamling points back at it, and inherits the university as controller.
        var firstVisit = tromso4.Datasamlinger[0];
        Assert.Equal(tromso4.Id, firstVisit.ParentDelkildeId);
        Assert.Null(tromso4.DataController);
        Assert.Equal("UiT The Arctic University of Norway", tromso4.EffectiveDataController);
    }

    [Fact]
    public async Task GetKildeHierarchyAsync_WhenTheKildeHasDelkilder_ThenTheWholeTreeIsRead()
    {
        var hierarchy = await WithResponse("hierarchy.json", out _).GetKildeHierarchyAsync(Guid.NewGuid());

        Assert.NotNull(hierarchy);
        Assert.Equal("The Tromsø study", hierarchy.KildeName);
        Assert.Equal(5752, hierarchy.TotalVariableCount);
        Assert.Equal(5, hierarchy.Delkilder.Count);
        Assert.Equal(3, hierarchy.DirectDatasamlinger.Count);

        var tromso5 = hierarchy.Delkilder.First(d => d.Datasamlinger.Count == 4);
        Assert.Equal(1170, tromso5.VariableCount);

        var firstVisit = tromso5.Datasamlinger[0];
        Assert.Equal(["ehds-cat:population-health-surveys"], firstVisit.Categories);
        Assert.NotEmpty(firstVisit.Variabelgrupper);
        Assert.All(firstVisit.Variabelgrupper, g => Assert.NotEqual(Guid.Empty, g.Id));
    }

    [Fact]
    public async Task GetDatasamlingAsync_WhenTheApiAnswersWithARealResponse_ThenTheDetailAndItsParentAreRead()
    {
        var datasamling = await WithResponse("datasamling.json", out _).GetDatasamlingAsync(Guid.NewGuid());

        Assert.NotNull(datasamling);
        Assert.Equal("K_ALS.INKLUSJON", datasamling.Code);
        Assert.Equal("Inklusjon", datasamling.PreferredTerm);
        Assert.Equal("yearly", datasamling.StatisticsType);
        Assert.Equal(99, datasamling.VariableCount);
        Assert.Equal(18, datasamling.PropertyMetadata.Count);
        Assert.Equal("Als registeret", datasamling.ParentKildeName);
        Assert.Null(datasamling.ParentDelkildeId); // hangs directly off the kilde
        Assert.NotNull(datasamling.InclusionAndExclusionCriteria);

        // Own value absent, effective value inherited from the kilde.
        Assert.Null(datasamling.LegalBasis);
        Assert.NotNull(datasamling.EffectiveLegalBasis);
    }

    [Fact]
    public async Task GetVariableAsync_WhenTheApiAnswersWithARealResponse_ThenVersionsAndKodeverkAreRead()
    {
        var variable = await WithResponse("variable.json", out _).GetVariableAsync(Guid.NewGuid());

        Assert.NotNull(variable);
        Assert.Equal("V_ALS.F1.ALSFRSR1TALE", variable.Code);
        Assert.Equal("Active", variable.VersionStatus);
        Assert.Equal("2", variable.DataType);
        Assert.Equal("yearly", variable.DatasamlingStatisticsType);
        Assert.Equal(33, variable.PropertyMetadata.Count);
        Assert.Equal("ALSFRSR1Tale", variable.AdditionalProperties["DatabaseReferanse"]);

        var version = Assert.Single(variable.Versions);
        Assert.Equal(variable.VersionId, version.VersionId);
        Assert.Null(version.ValidTo); // still in force

        var link = Assert.Single(variable.KodeverkLinks);
        Assert.Equal("Kildekodeverk", link.KodeverkType);
        Assert.True(link.HasCodeValues);
        Assert.Null(link.DisplayName); // unresolved name is normal — fall back to the reference

        Assert.Equal("Funksjonsscore", Assert.Single(variable.AllVariabelgrupper).Name);
        Assert.Equal("Inklusjon", Assert.Single(variable.AllDatasamlinger).Name);
    }

    [Fact]
    public async Task GetVariableTimelineAsync_WhenTheApiAnswersWithARealResponse_ThenTheVersionsAreRead()
    {
        var timeline = await WithResponse("timeline.json", out _).GetVariableTimelineAsync(Guid.NewGuid());

        var version = Assert.Single(timeline);
        Assert.Equal("1. Tale", version.PreferredTerm);
        Assert.Equal("Active", version.Status);
        Assert.Equal(2010, version.ValidFrom?.Year);
        Assert.Null(version.PublishedAt); // not tracked for imported versions
        Assert.Equal("2", version.AdditionalProperties["DataType"]);
    }

    // ---------------------------------------------------------------------------- 404 and failure

    [Fact]
    public async Task GetKildeAsync_WhenTheKildeDoesNotExist_ThenNullRatherThanAThrow()
    {
        Assert.Null(await WithStatus(HttpStatusCode.NotFound).GetKildeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetKildeHierarchyAsync_WhenTheKildeDoesNotExist_ThenNullRatherThanAThrow()
    {
        Assert.Null(await WithStatus(HttpStatusCode.NotFound).GetKildeHierarchyAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetDatasamlingAsync_WhenTheDatasamlingDoesNotExist_ThenNullRatherThanAThrow()
    {
        Assert.Null(await WithStatus(HttpStatusCode.NotFound).GetDatasamlingAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetVariableAsync_WhenTheVariableDoesNotExist_ThenNullRatherThanAThrow()
    {
        Assert.Null(await WithStatus(HttpStatusCode.NotFound).GetVariableAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetVariableTimelineAsync_WhenTheVariableDoesNotExist_ThenAnEmptyListRatherThanAThrow()
    {
        Assert.Empty(await WithStatus(HttpStatusCode.NotFound).GetVariableTimelineAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetKilderAsync_WhenThereAreNoKilder_ThenAnEmptyListRatherThanAThrow()
    {
        Assert.Empty(await WithStatus(HttpStatusCode.NotFound).GetKilderAsync());
    }

    [Fact]
    public async Task GetFiltersAsync_WhenThereAreNoFacets_ThenEmptyFilterOptionsRatherThanAThrow()
    {
        var filters = await WithStatus(HttpStatusCode.NotFound).GetFiltersAsync();

        Assert.Empty(filters.Kilder);
        Assert.Equal(0, filters.TotalCount);
    }

    [Fact]
    public async Task GetKildeAsync_WhenTheApiFails_ThenItIsRethrown()
    {
        // A 500 is a fault the caller has to be able to tell apart from "not published".
        var client = WithStatus(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetKildeAsync(Guid.NewGuid()));
    }

    // ------------------------------------------------------------------------------ URL building

    [Fact]
    public async Task GetKilderAsync_WhenSearchAndKildetypeAreSet_ThenBothAreSentAsQueryParameters()
    {
        var handler = StubHttpHandler.Ok("[]");

        await Client(handler).GetKilderAsync("hjerte og kar", "sentraltHelseregister");

        Assert.Equal("/api/explorer/kilder", handler.LastUri?.AbsolutePath);
        Assert.Equal("?search=hjerte%20og%20kar&kildeType=sentraltHelseregister", handler.LastUri?.Query);
    }

    [Fact]
    public async Task GetKilderAsync_WhenNoParametersAreSet_ThenNoQueryIsSent()
    {
        var handler = StubHttpHandler.Ok("[]");

        await Client(handler).GetKilderAsync();

        Assert.Equal("", handler.LastUri?.Query);
    }

    [Theory]
    [InlineData(SortField.Default, SortDirection.Descending, "name", "desc")]
    [InlineData(SortField.Kilde, SortDirection.Ascending, "kilde", "asc")]
    [InlineData(SortField.Datasamling, SortDirection.Ascending, "datasamling", "asc")]
    [InlineData(SortField.Variabelgruppe, SortDirection.Descending, "variabelgruppe", "desc")]
    public async Task SearchVariablesAsync_WhenASortIsChosen_ThenTheApisOwnTokensAreSent(
        SortField field, SortDirection direction, string sort, string sortDir)
    {
        // The API takes these as free text and quietly falls back to its default order for anything
        // it does not recognise, so a wrong token would not fail — it would return a different order
        // than the UI says it is showing. Hence the tokens are pinned here.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).SearchVariablesAsync(null, sort: field, direction: direction);

        Assert.Equal($"?page=1&size=25&sort={sort}&sortDir={sortDir}", handler.LastUri?.Query);
    }

    [Fact]
    public async Task SearchVariablesAsync_WhenTheSortIsTheDefault_ThenNoSortParametersAreSent()
    {
        // The default order ascending is what the API does when neither parameter arrives, so
        // sending them would only make the URL longer — and a shorter URL caches better on a
        // public page.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).SearchVariablesAsync(null);

        Assert.Equal("?page=1&size=25", handler.LastUri?.Query);
    }

    [Fact]
    public async Task SearchVariablesAsync_WhenAFilterIsGiven_ThenItsParametersAreSentAlongsideThePaging()
    {
        var handler = StubHttpHandler.Ok("{}");
        var kilde = Guid.NewGuid();

        await Client(handler).SearchVariablesAsync(
            "tale", new VariableFilter { KildeIds = [kilde], DataTypes = ["1"] });

        Assert.Equal($"?page=1&size=25&search=tale&kildeIds={kilde}&datatypes=1", handler.LastUri?.Query);
    }

    [Fact]
    public async Task SearchVariablesAsync_WhenTheFilterNarrowsNothing_ThenTheUrlIsTheOneAnUnfilteredSearchAlwaysHad()
    {
        // Filtering must not lengthen the URL of a search that is not filtered — a public page's
        // cache hit rate depends on the unfiltered request staying byte-identical.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).SearchVariablesAsync(null, VariableFilter.None);

        Assert.Equal("?page=1&size=25", handler.LastUri?.Query);
    }

    [Fact]
    public async Task GetFiltersAsync_WhenALanguageIsGiven_ThenItIsAskedForInThatLanguage()
    {
        // The datatype facet's name is resolved server side from editable master data, in the
        // request's culture. Without this header a component rendering in English is labelled in
        // Norwegian — and because the API's output cache is keyed on the resolved culture, it can
        // be served the other language's cached body outright.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).GetFiltersAsync(language: "en");

        Assert.Equal(["en"], handler.LastAcceptLanguage);
    }

    [Fact]
    public async Task GetFiltersAsync_WhenNoLanguageIsGiven_ThenNoneIsAskedFor()
    {
        // No header rather than a guessed one: the API has its own default, and inventing a
        // language here would override it with whatever the caller happened not to say.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).GetFiltersAsync();

        Assert.Empty(handler.LastAcceptLanguage);
    }

    [Fact]
    public async Task GetFiltersAsync_WhenTheLanguageIsBlank_ThenNoneIsAskedFor()
    {
        // An empty string is not a language. Sending it would produce a malformed header rather
        // than a default.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).GetFiltersAsync(language: "   ");

        Assert.Empty(handler.LastAcceptLanguage);
    }

    [Fact]
    public async Task GetFiltersAsync_WhenAFilterIsGiven_ThenTheCountsAreAskedForWithTheSameNarrowing()
    {
        // The counts are cross-filtered. Asking for them with different narrowing than the search
        // used is how a list and the numbers beside it end up describing two different selections.
        var handler = StubHttpHandler.Ok("{}");
        var kilde = Guid.NewGuid();

        await Client(handler).GetFiltersAsync("tale", new VariableFilter { KildeIds = [kilde] });

        Assert.Equal($"?search=tale&kildeIds={kilde}", handler.LastUri?.Query);
    }

    [Fact]
    public async Task GetFiltersAsync_WhenOnlyAFilterIsGiven_ThenTheQuestionMarkIsStillWrittenOnce()
    {
        // The search parameter is what usually opens the query string; without it the filter has to
        // open it itself rather than appending to a URL that has no `?` yet.
        var handler = StubHttpHandler.Ok("{}");

        await Client(handler).GetFiltersAsync(filter: new VariableFilter { KildeType = "biobank" });

        Assert.Equal("?kildeType=biobank", handler.LastUri?.Query);
    }

    [Fact]
    public async Task SearchVariablesAsync_WhenASortFieldHasNoToken_ThenItFailsRatherThanFallingBack()
    {
        // The closed enum exists so the URL cannot ask for one order while the UI claims another.
        // A member added without a token here must therefore throw rather than quietly send `name`,
        // which is what a `_ => "name"` arm used to do.
        var handler = StubHttpHandler.Ok("{}");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Client(handler).SearchVariablesAsync(null, sort: (SortField)99));
    }

    [Fact]
    public async Task GetVariableAsync_WhenHistoricalVersionsAreWanted_ThenIncludeHistoricalIsSent()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        await Client(handler).GetVariableAsync(id, includeHistorical: true);

        Assert.Equal($"/api/explorer/variables/{id}", handler.LastUri?.AbsolutePath);
        Assert.Equal("?includeHistorical=true", handler.LastUri?.Query);
    }

    [Fact]
    public async Task GetVariableAsync_WhenHistoricalVersionsAreNotWanted_ThenNoQueryIsSent()
    {
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);

        await Client(handler).GetVariableAsync(Guid.NewGuid());

        Assert.Equal("", handler.LastUri?.Query);
    }

    [Fact]
    public async Task GetKildeAsync_WhenTheBaseAddressHasAPath_ThenThePathIsKept()
    {
        // The relative URLs only stay under a hosted path if the base address keeps its trailing
        // slash — without it, "api/explorer/..." replaces the last segment instead of extending it.
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        await Client(handler, "https://helsedata.no/munin/").GetKildeAsync(id);

        Assert.Equal($"https://helsedata.no/munin/api/explorer/kilder/{id}", handler.LastUri?.ToString());
    }
}
