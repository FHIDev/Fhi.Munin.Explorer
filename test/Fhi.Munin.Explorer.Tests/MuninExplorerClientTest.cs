using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The client against a stubbed transport: does a real API response round-trip into the
/// contracts, and does a missing resource come back as "nothing" rather than an exception.
/// </summary>
public class MuninExplorerClientTest
{
    private const string DefaultBaseAddress = "https://runa.munin.skytest.fhi.no/";

    private static MuninExplorerClient Client(HttpMessageHandler handler, string baseAddress = DefaultBaseAddress) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(baseAddress) });

    private static MuninExplorerClient WithResponse(string fixture, out StubHttpHandler handler)
    {
        handler = StubHttpHandler.Ok(TestData.Read(fixture));
        return Client(handler);
    }

    private static MuninExplorerClient WithStatus(HttpStatusCode status) => Client(StubHttpHandler.Status(status));

    /// <summary>The client answering one hand-written body, for a shape no fixture has.</summary>
    private static MuninExplorerClient WithJson(string json) => Client(StubHttpHandler.Ok(json));

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

        // Datatypes arrive labelled: Fhi.Metadata-xxi8k made the endpoint resolve the name in the
        // request's language, and this capture went unrefreshed until FixtureDriftTest noticed.
        Assert.Equal(["1", "10", "2", "3", "4", "6", "7"], filters.DataTypes.Select(d => d.Value));
        Assert.Equal("Fødselsnummer (11 siffer)", filters.DataTypes[1].DisplayName);

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
        Assert.Equal("Norsk register for ALS og andre motonevronsykdommer (ALS-registeret)", als.Name);
        Assert.Equal("nasjonaltMedisinskKvalitetsregister", als.Kildetype);
        Assert.True(als.IsActive);
        Assert.True(als.HasVariableDescription);
        Assert.Equal(9, als.DatasamlingCount);
        Assert.Equal(240, als.TotalVariables);
        Assert.Null(als.HealthDcatScore); // never computed yet — see the note on the property
        Assert.Equal("alsregister@stolav.no", als.AdditionalProperties["Epost"]);

        // The founding year the Opprettet column reads, asserted where the payload is: the key is
        // curated rather than modelled, so nothing about it is a compile error, and this capture is
        // what says the spelling the ordinal lookup uses is the API's own.
        Assert.Equal("2023", als.AdditionalProperties["Opprettet"]);
    }

    [Fact]
    public async Task GetKildeAsync_WhenTheApiAnswersWithARealResponse_ThenTheDetailAndItsDatasamlingerAreRead()
    {
        var kilde = await WithResponse("kilde.json", out _).GetKildeAsync(Guid.NewGuid());

        Assert.NotNull(kilde);
        Assert.Equal("K_ALS", kilde.Code);

        // Not a contradiction of the test above, which reads a kilder.json re-taken for its
        // Opprettet key: this capture and the four siblings still carrying K_ALS's old name are
        // older, so the corpus is coherent per file rather than as one pass.
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
    public async Task GetKildePropertyMetadataAsync_WhenTheApiAnswersWithAVocabulary_ThenBothLabelsSurvive()
    {
        // The two entries are copied verbatim out of the propertyMetadata a real kilde detail
        // carries — Testdata/kilde-med-delkilder.json — because the sibling endpoint serves the
        // very same DTO, built by the same helper on the API side. What matters here is that
        // optionsJson arrives as a *string* holding JSON rather than as JSON, and that both labels
        // are still inside it: the list is fetched language-agnostically, so the component picks
        // label or labelEn per render and cannot use a label the request already resolved.
        const string vocabulary = """
            [
              {
                "key": "accessRights",
                "displayNameTranslations": { "no": "Tilgangsrettigheter", "en": "Access rights" },
                "groupTranslations": { "no": "EHDS / HealthDCAT-AP", "en": "EHDS / HealthDCAT-AP" },
                "sortOrder": 300,
                "type": "SingleSelect",
                "optionsJson": "[{\"value\":\"eu-access:NON_PUBLIC\",\"label\":\"Ikke-offentlig\",\"labelEn\":\"Non-public\"}]",
                "options": [ { "value": "eu-access:NON_PUBLIC", "displayName": "Ikke-offentlig" } ]
              },
              {
                "key": "healthCategory",
                "displayNameTranslations": { "no": "Helsedatakategori", "en": "Health data category" },
                "groupTranslations": { "no": "EHDS / HealthDCAT-AP", "en": "EHDS / HealthDCAT-AP" },
                "sortOrder": 330,
                "type": "MultiSelect",
                "optionsJson": "[{\"value\":\"ehds-cat:biobanks\",\"label\":\"Biobanker\",\"labelEn\":\"Biobanks\"}]",
                "options": [ { "value": "ehds-cat:biobanks", "displayName": "Biobanker" } ]
              }
            ]
            """;

        var handler = StubHttpHandler.Ok(vocabulary);

        var entries = await Client(handler).GetKildePropertyMetadataAsync();

        Assert.Equal("/api/explorer/kilder/egenskaper", handler.LastUri?.AbsolutePath);

        // No Accept-Language, deliberately: see the remarks on the interface method.
        Assert.Equal("", handler.LastUri?.Query);

        Assert.Equal(["accessRights", "healthCategory"], entries.Select(entry => entry.Key));
        Assert.Contains("\"labelEn\":\"Biobanks\"", entries[1].OptionsJson);
        Assert.Equal("Biobanker", entries[1].Options[0].DisplayName);
    }

    [Fact]
    public async Task GetKildePropertyMetadataAsync_WhenTheApiServesNoVocabulary_ThenAnEmptyListRatherThanAThrow()
    {
        // An API that predates the endpoint answers 404, and a facet without labels is a facet
        // showing the catalogue's own tokens — worse to read, and not a reason to fail the page.
        Assert.Empty(await WithStatus(HttpStatusCode.NotFound).GetKildePropertyMetadataAsync());
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

    [Fact]
    public async Task GetKodeverkCodesAsync_WhenTheApiAnswersWithARealResponse_ThenTheEnvelopeAndItsCodesAreRead()
    {
        var codes = await WithResponse("kodeverk-codes.json", out _)
            .GetKodeverkCodesAsync(Guid.NewGuid(), "Kildekodeverk", "2336");

        Assert.NotNull(codes);

        // The envelope names the link it was fetched for, which is what lets a caller match the
        // answer to the line it asked from rather than trusting the order it came back in.
        Assert.Equal("Kildekodeverk", codes.KodeverkType);
        Assert.Equal("2336", codes.KodeverkReference);

        Assert.Equal(6, codes.Codes.Count);
        Assert.Equal("0", codes.Codes[0].Value);
        Assert.Equal("Velg verdi", codes.Codes[0].Name);
        Assert.Equal(2010, codes.Codes[0].ValidFrom?.Year);

        // No end date is the normal state of a code still in use — the whole of this kodeverk is.
        Assert.All(codes.Codes, code => Assert.Null(code.ValidTo));
    }

    [Fact]
    public async Task GetKodeverkCodesAsync_WhenAKodeverkRecordsNoStartDates_ThenValidFromIsNullRatherThanADefault()
    {
        // Kommunenummer is the live case: every code carries a gyldigTil from the import that
        // loaded it and no gyldigFra at all. A non-nullable ValidFrom would show all 885 of them as
        // starting on 01.01.0001, which reads as data rather than as an absence.
        var handler = StubHttpHandler.Ok("""
            {"kodeverkType":"AdministrativtKodeverk","kodeverkReference":"3402",
             "koder":[{"verdi":"0101","navn":"Halden","gyldigFra":null,
                       "gyldigTil":"2023-09-06T13:13:41.000Z"}]}
            """);

        var codes = await Client(handler).GetKodeverkCodesAsync(Guid.NewGuid(), "AdministrativtKodeverk", "3402");

        var code = Assert.Single(codes!.Codes);
        Assert.Null(code.ValidFrom);
        Assert.Equal(2023, code.ValidTo?.Year);
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
    public async Task GetKodeverkCodesAsync_WhenTheLinkHasNoServableCodes_ThenNullRatherThanAThrow()
    {
        // Every HelsefagligKodeverk link answers 404 here, and so does a reference the upstream
        // register does not know. Neither is a fault: the panel says "no code values" and carries
        // on, where a throw would take the whole variable panel down over one collapsed list.
        Assert.Null(await WithStatus(HttpStatusCode.NotFound)
            .GetKodeverkCodesAsync(Guid.NewGuid(), "HelsefagligKodeverk", "ICD-10"));
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

    // ------------------------------------------------------------------------------------ 429

    [Fact]
    public async Task GetKildeAsync_WhenTheApiRateLimits_ThenItsOwnExceptionCarriesTheRetryAfter()
    {
        var client = Client(StubHttpHandler.RateLimited(TimeSpan.FromSeconds(30)));

        var refused = await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => client.GetKildeAsync(Guid.NewGuid()));

        Assert.Equal(TimeSpan.FromSeconds(30), refused.RetryAfter);
    }

    [Fact]
    public async Task GetKildeAsync_WhenARateLimitCarriesNoRetryAfter_ThenTheWaitIsUnknownRatherThanZero()
    {
        // The header is optional and a proxy can drop it. Null says "we were not told"; a zero
        // would say "go now", which is the one thing a throttled caller must not read.
        var client = Client(StubHttpHandler.RateLimited());

        var refused = await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => client.GetKildeAsync(Guid.NewGuid()));

        Assert.Null(refused.RetryAfter);
    }

    [Fact]
    public async Task GetKildeAsync_WhenARetryAfterIsAnHttpDate_ThenItIsReadAsTheWaitItImplies()
    {
        // Retry-After's other legal form, which is a parse and a subtraction rather than a value
        // handed over as it stands. Bounded rather than exact because the header carries whole
        // seconds and the clock moves between writing it and reading it.
        var client = Client(StubHttpHandler.RateLimitedUntil(DateTimeOffset.UtcNow.AddMinutes(10)));

        var refused = await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => client.GetKildeAsync(Guid.NewGuid()));

        Assert.NotNull(refused.RetryAfter);
        Assert.InRange(refused.RetryAfter.Value, TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task GetKildeAsync_WhenAnHttpDateRetryAfterHasPassed_ThenTheWaitIsZeroRatherThanNegative()
    {
        // A reader whose clock runs behind the server's meets this on the first 429 they get, and a
        // negative TimeSpan travelling on the exception would print in a host's log as a wait that
        // ended before it began. Zero is the honest answer: nothing left to wait, as far as the
        // header said.
        var client = Client(StubHttpHandler.RateLimitedUntil(DateTimeOffset.UtcNow.AddMinutes(-1)));

        var refused = await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => client.GetKildeAsync(Guid.NewGuid()));

        Assert.Equal(TimeSpan.Zero, refused.RetryAfter);
    }

    [Fact]
    public async Task SearchVariablesAsync_WhenTheApiRateLimits_ThenItThrowsInsteadOfReturningAnEmptyPage()
    {
        // The trap this whole change is about. A 404 maps to null and this method maps that null
        // on to an empty page, because a search with no hits is a normal answer — so the empty-page
        // branch is sitting right there, one status away, and it is the wrong answer for a 429: it
        // would tell the reader their search found nothing for a search that was never run, and
        // hide the throttling from everything that logs on exceptions.
        var client = Client(StubHttpHandler.RateLimited(TimeSpan.FromSeconds(5)));

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => client.SearchVariablesAsync("tale"));
    }

    [Fact]
    public async Task GetKilderAsync_WhenTheApiRateLimits_ThenItThrowsInsteadOfReturningAnEmptyList()
    {
        // The same trap in the shape the collection endpoints take it: 404 comes back as [], which
        // reads as "the catalogue has no kilder" rather than "we were not allowed to ask".
        var client = Client(StubHttpHandler.RateLimited());

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => client.GetKilderAsync());
    }

    [Fact]
    public async Task GetFiltersAsync_WhenTheApiRateLimits_ThenItThrowsInsteadOfReturningEmptyFacets()
    {
        var client = Client(StubHttpHandler.RateLimited());

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(() => client.GetFiltersAsync());
    }

    [Fact]
    public async Task SearchVariablesAsync_WhenTheApiRateLimits_ThenTheCallIsNotRetried()
    {
        // No resilience handler, no wait-and-try-again loop. The limit is counted per address and
        // helsedata's cluster reaches Munin as one, so retrying on a Retry-After would fire every
        // reader's component at the same instant and rebuild the same burst against the same
        // window. Exactly one request leaves for one call, whatever the API answered.
        var handler = StubHttpHandler.RateLimited(TimeSpan.FromSeconds(30));

        await Assert.ThrowsAsync<MuninExplorerRateLimitedException>(
            () => Client(handler).SearchVariablesAsync(null));

        Assert.Equal(1, handler.Calls);
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

    [Fact]
    public async Task GetKodeverkCodesAsync_WhenTheReferenceNeedsEscaping_ThenItStaysOnePathSegment()
    {
        // Both segments go into the path, and a reference is the catalogue's own text: V-AK sends
        // dotted OIDs, V-HK sends things like NCMP-NCSP-NCRP. A slash in one would otherwise be
        // read as a route separator, and the request would 404 for a link that does exist.
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        await Client(handler).GetKodeverkCodesAsync(id, "AdministrativtKodeverk", "2.16.578/1 1");

        Assert.Equal($"/api/explorer/variables/{id}/kodeverk/AdministrativtKodeverk/2.16.578%2F1%201/codes",
                     handler.LastUri?.AbsolutePath);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../..")]
    [InlineData("a/./b")]
    [InlineData("..\\..")]
    [InlineData("a\\..\\b")]
    public async Task GetKodeverkCodesAsync_WhenAReferenceCarriesADotSegment_ThenNothingIsSentAtAll(
        string reference)
    {
        // Escaping a slash is not enough on its own: a dot is unreserved, so EscapeDataString
        // leaves it alone, and percent-encoding it by hand changes nothing either — Uri unescapes
        // %2E and removes the dot segment afterwards. So a reference of ".." would walk out of the
        // codes endpoint and address something else on the same host with the bearer token
        // attached, and the only way to keep it from doing so is not to send it.
        //
        // The backslash forms are here for the same reason the guard splits on one: EscapeDataString
        // writes "\" as %5C, and a server that decodes the target before normalising it can resolve
        // that as a separator. The guard refuses any part that is nothing but dots, which is wider
        // than the "." and ".." that actually normalise — deliberately so, since no real reference
        // is all dots.
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);

        var refused = await Assert.ThrowsAsync<ArgumentException>(() =>
            Client(handler).GetKodeverkCodesAsync(Guid.NewGuid(), "AdministrativtKodeverk", reference));

        Assert.Equal("kodeverkReference", refused.ParamName);
        Assert.Null(handler.LastUri);
    }

    [Fact]
    public async Task GetKodeverkCodesAsync_WhenTheTypeCarriesADotSegment_ThenItIsRefusedToo()
    {
        // The type is the API's own vocabulary and is three enum names today, but it is passed
        // through verbatim in the same way — one rule for both segments, not a rule for the one
        // that happens to be documented as free text.
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);

        var refused = await Assert.ThrowsAsync<ArgumentException>(() =>
            Client(handler).GetKodeverkCodesAsync(Guid.NewGuid(), "..", "2336"));

        Assert.Equal("kodeverkType", refused.ParamName);
        Assert.Null(handler.LastUri);
    }

    [Fact]
    public async Task GetKodeverkCodesAsync_WhenAReferenceIsADottedOid_ThenItsDotsAreLeftReadable()
    {
        // The other half of the same rule. Only a segment that is nothing but dots normalises, so
        // an OID — which is most of what V-AK sends — keeps the spelling the catalogue published
        // rather than being turned into %2E noise in every log and network tab.
        var handler = StubHttpHandler.Status(HttpStatusCode.NotFound);
        var id = Guid.NewGuid();

        await Client(handler).GetKodeverkCodesAsync(id, "AdministrativtKodeverk", "2.16.578.1.12.4.1.1.7113");

        Assert.Equal($"/api/explorer/variables/{id}/kodeverk/AdministrativtKodeverk/2.16.578.1.12.4.1.1.7113/codes",
                     handler.LastUri?.AbsolutePath);
    }

    // ------------------------------------------------- explicit nulls where a collection is due

    [Fact]
    public async Task GetVariableAsync_WhenACollectionArrivesAsAnExplicitNull_ThenItIsReadAsEmpty()
    {
        // The failure this closes: every collection on every contract is declared non-nullable
        // with an initialiser, and that initialiser only survives a key ABSENT from the payload.
        // An explicit null is written straight over it, and the first read of the result throws
        // while rendering — past the try/catch around the fetch, which on a Blazor Server host
        // takes the circuit and the page it is mounted in down.
        //
        // additionalProperties is the key the API has actually been seen doing it on, twice. The
        // three beside it are here because nothing marks them as incapable of it, and because the
        // point of handling this on the serialiser rather than at a read site is that it covers
        // the keys nobody has read yet.
        var detail = await WithJson("""
            {
              "id": "6f1d4a5c-0000-4000-8000-000000000002",
              "code": "V_ALS.F1.ALSFRSR1TALE",
              "additionalProperties": null,
              "propertyMetadata": null,
              "versjoner": null,
              "statistikker": [
                { "code": "ALSFRSR1Tale", "additionalProperties": null }
              ]
            }
            """).GetVariableAsync(Guid.NewGuid());

        Assert.NotNull(detail);
        Assert.Empty(detail.AdditionalProperties);
        Assert.Empty(detail.PropertyMetadata);
        Assert.Empty(detail.Versions);
        Assert.Empty(detail.Statistics[0].AdditionalProperties);
    }

    [Fact]
    public async Task SearchVariablesAsync_WhenTheItemsArriveAsAnExplicitNull_ThenThePageIsEmptyRatherThanBroken()
    {
        // The same rule one level up: a page is a collection too, and the count beside it is still
        // read. Nothing is inferred from the null except that there is nothing in it.
        var page = await WithJson("""
            { "items": null, "totalCount": 0, "page": 1, "pageSize": 25, "totalPages": 0 }
            """).SearchVariablesAsync(null);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public void Json_WhenAContractIsWrittenBack_ThenItsCollectionsAreStillArraysAndObjects()
    {
        // The converter has a write half, and the way to get it wrong is to hand the value back to
        // the serialiser as the interface it was just resolved for — which resolves the same
        // converter again and recurses until the stack ends. That much ShapeDrift would already
        // catch: it serialises every deserialised contract with these same options, on eight
        // fixtures, in every CI run, so the recursion would take those down rather than wait for a
        // future caller.
        //
        // What it would not catch is what this pins: ShapeDrift only diffs our output against the
        // live body, so a write half that emitted the right kinds with the wrong keys, or a
        // dictionary written as an array, reads as drift in the API rather than as a bug here. This
        // says what the bytes are.
        var detail = new VariableDetail
        {
            Code = "V_ALS.F1.ALSFRSR1TALE",
            AdditionalProperties = new Dictionary<string, string?> { ["Kommentar"] = "noe" },
            Statistics = [new Statistic { Code = "ALSFRSR1Tale" }]
        };

        var json = JsonSerializer.Serialize(detail, MuninExplorerClient.Json);

        Assert.Contains("\"additionalProperties\":{\"Kommentar\":\"noe\"}", json);
        Assert.Contains("\"statistikker\":[{", json);

        var read = JsonSerializer.Deserialize<VariableDetail>(json, MuninExplorerClient.Json)!;

        Assert.Equal("noe", read.AdditionalProperties["Kommentar"]);
        Assert.Equal("ALSFRSR1Tale", read.Statistics[0].Code);
    }

    [Fact]
    public async Task GetKildeAsync_WhenMuninSendsAnExplicitNullTimestamp_ThenTheKildeStillReads()
    {
        // Munin declares these columns nullable and sends explicit nulls for dates elsewhere —
        // kodeverk codes do it for gyldigFra. On a non-nullable property that throws inside
        // ReadFromJsonAsync, and the caller loses the whole kilde rather than one field.
        var kilde = await WithJson("""
            {"id":"8ec4c2c4-662d-47a5-a946-f1086a014070","code":"K_ALS","navn":"Als registeret",
             "opprettet":null,"sistOppdatert":null}
            """).GetKildeAsync(Guid.NewGuid());

        Assert.NotNull(kilde);
        Assert.Equal("K_ALS", kilde.Code);
        Assert.Null(kilde.LastUpdated);
        Assert.Null(kilde.Created);
    }


    /// <summary>A property of the shape these carried before Fhi.Metadata-se0by.</summary>
    private sealed record NonNullableTimestamp
    {
        [JsonPropertyName("sistOppdatert")] public DateTimeOffset LastUpdated { get; init; }
    }

    [Fact]
    public void Deserialize_WhenAnExplicitNullMeetsANonNullableDate_ThenItThrowsRatherThanDefaulting()
    {
        // The mechanism, demonstrated once: System.Text.Json refuses a null for a value type, and
        // the refusal is an exception out of the whole read rather than a default in one field.
        // What it does NOT do is pin the contracts — that is the sweep below. (Fhi.Metadata-se0by)
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NonNullableTimestamp>(
            """{"sistOppdatert":null}""", MuninExplorerClient.Json));
    }

    [Fact]
    public void Deserialize_WhenMuninSendsAnExplicitNull_ThenEveryContractTimestampReadsItAsAbsent()
    {
        // The sweep, in the shape NullAsEmptyCollectionsTest uses for collections and for its
        // reason: a per-property spot check leaves the next one added in the position all of these
        // were in. Every DateTimeOffset under Contracts/ has to tolerate a null on the wire.
        var offenders = TimestampProperties()
            .Where(p => Nullable.GetUnderlyingType(p.PropertyType) is null)
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.Equal([], offenders);

        // Not merely declared nullable — actually read back from a payload that sends the null.
        foreach (var property in TimestampProperties())
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
            var read = JsonSerializer.Deserialize(
                $$"""{"{{name}}":null}""", property.DeclaringType!, MuninExplorerClient.Json);

            Assert.NotNull(read);
            Assert.Null(property.GetValue(read));
        }
    }

    /// <summary>Every public date property under <c>Contracts/</c>, nullable or not.</summary>
    private static IReadOnlyList<PropertyInfo> TimestampProperties() =>
        [.. typeof(IMuninExplorerClient).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == typeof(IMuninExplorerClient).Namespace
                           && !typeof(Exception).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.GetMethod is not null
                               && (property.PropertyType == typeof(DateTimeOffset)
                                   || property.PropertyType == typeof(DateTimeOffset?)))];
}
