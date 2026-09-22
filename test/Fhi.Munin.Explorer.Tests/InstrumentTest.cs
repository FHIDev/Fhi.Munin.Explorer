using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The instrument a variable was collected with: the list on the variable's own surfaces, and the
/// page one press away.
/// </summary>
/// <remarks>
/// The instrument page is a drill-in keyed by <c>?instrumentId=</c> rather than a route, because
/// this package has no router — so the assertions below are on what a link carries and on which
/// view the address opens, not on a navigation nothing here would perform.
/// </remarks>
public class InstrumentTest : ExplorerTestContext
{
    private static readonly Guid Sf36 = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Hads = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VariableId = new("33333333-3333-3333-3333-333333333333");

    private static InstrumentReference Reference(
        Guid id, string code, string preferredTerm, string? englishName = null) =>
        new()
        {
            Id = id,
            Code = code,
            PreferredTerm = preferredTerm,
            AdditionalProperties = englishName is null
                ? []
                : new Dictionary<string, string?> { ["NavnEngelsk"] = englishName },
        };

    private static VariableDetail Variable(params InstrumentReference[] instruments) => new()
    {
        Id = VariableId,
        Code = "V_ALS.F1.ALSFRSR1TALE",
        PreferredTerm = "1. Tale",
        Description = "Skalaen måler taleevne.",
        KildeName = "Als registeret",
        KildeShortName = "ALS",
        Instruments = instruments,
    };

    private static InstrumentDetail Instrument(
        string? englishName = null,
        string? englishDescription = null,
        int variableCount = 12)
    {
        return new InstrumentDetail
        {
            Id = Sf36,
            Code = "INS_SF36",
            PreferredTerm = "Kortversjon 36",
            Description = "Et generisk spørreskjema om helserelatert livskvalitet.",
            ValidFrom = new DateTimeOffset(2004, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidTo = null,
            VisibleVariableCount = variableCount,
            AdditionalProperties = new Dictionary<string, string?>
            {
                ["NavnEngelsk"] = englishName,
                ["BeskrivelseEngelsk"] = englishDescription,
                ["Opphav"] = "RAND",
            },
            PropertyMetadata = [Property("Opphav"), Property("NavnEngelsk")],
        };
    }

    /// <summary>
    /// One of the curated properties the fixture's instrument carries, under one group.
    /// </summary>
    /// <remarks>
    /// <c>NavnEngelsk</c> is among them because Munin sends metadata for every Instrument-scoped
    /// key, the English name included — which is what makes it a row the metadata block could draw
    /// twice, and so what the reader-dependent exclusion is about.
    /// </remarks>
    private static PropertyMetadataEntry Property(string key) => new()
    {
        Key = key,
        SortOrder = 10,
        DisplayNameTranslations = new Dictionary<string, string> { ["no"] = key },
        GroupTranslations = new Dictionary<string, string> { ["no"] = "Om instrumentet" },
    };

    // -----------------------------------------------------------------------
    // The contract

    [Fact]
    public void VariableDetail_WhenThePayloadCarriesInstruments_ThenEachOneIsRead()
    {
        // Inline rather than from a fixture, as the sections and the placement fields are: the
        // instrument list reached Munin after every capture under Testdata/ was taken, so a row
        // written into one would pin a payload the API does not send. (Fhi.Metadata-t6bvg)
        var detail = JsonSerializer.Deserialize<VariableDetail>(
            """
            {
              "instrumenter": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "code": "INS_SF36",
                  "preferredTerm": "Kortversjon 36",
                  "additionalProperties": { "NavnEngelsk": "Short Form 36" }
                }
              ]
            }
            """,
            MuninExplorerClient.Json);

        var instrument = Assert.Single(detail!.Instruments);

        Assert.Equal(Sf36, instrument.Id);
        Assert.Equal("INS_SF36", instrument.Code);
        Assert.Equal("Kortversjon 36", instrument.PreferredTerm);
        Assert.Equal("Short Form 36", instrument.AdditionalProperties["NavnEngelsk"]);
    }

    [Fact]
    public void VariableDetail_WhenThePayloadHasNoInstrumentsProperty_ThenTheListIsEmptyRatherThanNull()
    {
        // The API that predates the field, and the API that sends an explicit null for it: both
        // have to arrive as "no instruments" rather than as something the views throw on while
        // rendering.
        Assert.Empty(JsonSerializer.Deserialize<VariableDetail>("{}", MuninExplorerClient.Json)!.Instruments);
        Assert.Empty(JsonSerializer.Deserialize<VariableDetail>(
            """{ "instrumenter": null }""", MuninExplorerClient.Json)!.Instruments);
    }

    [Fact]
    public void InstrumentDetail_WhenReadFromTheDocumentedResponse_ThenEveryWireNameIsCovered()
    {
        // Strict, so a name spelled differently from the merged DTO fails here rather than
        // rendering as a default. Inline for the reason above: no capture under Testdata/ can hold
        // this endpoint yet.
        var strict = new JsonSerializerOptions(MuninExplorerClient.Json)
        {
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        };

        var instrument = JsonSerializer.Deserialize<InstrumentDetail>(
            """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "code": "INS_SF36",
              "preferredTerm": "Kortversjon 36",
              "beskrivelse": "Et generisk spørreskjema.",
              "gyldigFra": "2004-03-01T00:00:00",
              "gyldigTil": null,
              "visibleVariableCount": 12,
              "additionalProperties": { "NavnEngelsk": "Short Form 36" },
              "propertyMetadata": [],
              "sections": []
            }
            """,
            strict);

        Assert.NotNull(instrument);
        Assert.Equal("INS_SF36", instrument.Code);
        Assert.Equal("Et generisk spørreskjema.", instrument.Description);
        Assert.Equal(new DateTimeOffset(2004, 3, 1, 0, 0, 0, TimeSpan.Zero), instrument.ValidFrom);
        Assert.Null(instrument.ValidTo);
        Assert.Equal(12, instrument.VisibleVariableCount);
    }

    // -----------------------------------------------------------------------
    // The variable page

    private IRenderedComponent<VariableView> RenderVariableView(
        VariableDetail variable, string? language = null, bool linked = true) =>
        Render<VariableView>(b => b
            .Add(c => c.Variable, variable)
            .Add(c => c.Language, language)
            .Add(c => c.InstrumentHref,
                 linked ? id => $"/variabler?utm_source=nyhetsbrev&instrumentId={id}" : null));

    private static IReadOnlyList<IElement> InstrumentLinks<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll("a[href*='instrumentId=']");

    [Fact]
    public void VariableView_WhenTheVariableHasOneInstrument_ThenItLinksToThatInstrumentsPage()
    {
        var cut = RenderVariableView(Variable(Reference(Sf36, "INS_SF36", "Kortversjon 36")));

        var link = Assert.Single(InstrumentLinks(cut));

        Assert.Equal("Kortversjon 36", link.TextContent.Trim());

        // The host's own parameter travels with it: the address is the page the reader is on, with
        // one key added, and nothing of theirs erased.
        Assert.Equal($"/variabler?utm_source=nyhetsbrev&instrumentId={Sf36}", link.GetAttribute("href"));

        Assert.Equal("Instrument", cut.Find($"#{DetailSectionIds.Instruments} .headline").TextContent.Trim());
        Assert.Contains("Instrument", ContentsEntries(cut));
    }

    [Fact]
    public void VariableView_WhenTheVariableHasTwoInstruments_ThenBothAreListedAndLinked()
    {
        var cut = RenderVariableView(Variable(
            Reference(Sf36, "INS_SF36", "Kortversjon 36"),
            Reference(Hads, "INS_HADS", "Angst og depresjon")));

        Assert.Equal(
            [$"/variabler?utm_source=nyhetsbrev&instrumentId={Sf36}",
             $"/variabler?utm_source=nyhetsbrev&instrumentId={Hads}"],
            InstrumentLinks(cut).Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public void VariableView_WhenTheVariableHasNoInstrument_ThenNeitherTheSectionNorItsNavEntryIsDrawn()
    {
        // Most variables belong to none, so this is the ordinary page rather than the edge: a
        // heading over an empty list, or a nav entry pointing at a section that is not there, would
        // be on nearly every variable in the catalogue.
        var cut = RenderVariableView(Variable());

        Assert.Empty(cut.FindAll($"#{DetailSectionIds.Instruments}"));
        Assert.DoesNotContain("Instrument", ContentsEntries(cut));
    }

    [Fact]
    public void VariableView_WhenNoHostSuppliedAnAddress_ThenTheNamesAreWordsRatherThanDeadLinks()
    {
        // A standalone mount, which is what a host laying the surfaces out itself gets. The package
        // owns no URL, so a link here could only go nowhere.
        var cut = RenderVariableView(Variable(Reference(Sf36, "INS_SF36", "Kortversjon 36")), linked: false);

        Assert.Empty(InstrumentLinks(cut));
        Assert.Equal("Kortversjon 36", cut.Find($"#{DetailSectionIds.Instruments} li").TextContent.Trim());
    }

    private static IReadOnlyList<string> ContentsEntries<T>(IRenderedComponent<T> cut) where T : IComponent =>
        [.. cut.FindAll(".munin-explorer-page__toc a").Select(entry => entry.TextContent.Trim())];

    // -----------------------------------------------------------------------
    // The row's drill-in panel

    /// <summary>Answers one row, that row's detail, and one instrument.</summary>
    /// <remarks>
    /// It re-lists <see cref="IMuninExplorerClient"/> because <see cref="EmptyMuninExplorerClient"/>
    /// deliberately does not answer for the instrument endpoint: the interface's own default body is
    /// the only implementation of it there, and adding a virtual to the empty fake would take that
    /// away from every test in the suite. Re-listing remaps this one member and leaves the rest
    /// inherited.
    /// </remarks>
    private sealed class InstrumentClient(
        VariableDetail? detail = null, InstrumentDetail? instrument = null)
        : EmptyMuninExplorerClient, IMuninExplorerClient
    {

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items =
                [
                    new VariableSummary
                    {
                        Id = VariableId,
                        Code = "V_ALS.F1.ALSFRSR1TALE",
                        PreferredTerm = "1. Tale",
                        KildeName = "Als registeret",
                    },
                ],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(detail);

        public Task<InstrumentDetail?> GetInstrumentAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(instrument);
    }

    private IRenderedComponent<VariableSearch> RenderSearch(
        IMuninExplorerClient client,
        string? language = null,
        Guid? instrumentId = null)
    {
        Services.AddSingleton(client);

        return Render<VariableSearch>(b => b
            .Add(c => c.Language, language)
            .Add(c => c.SelectedInstrumentId, instrumentId)
            .Add(c => c.InstrumentHref, id => $"/variabler?utm_source=nyhetsbrev&instrumentId={id}")
            .Add(c => c.InstrumentVariablesHref, id => $"/variabler?instrumentIds={id}"));
    }

    [Fact]
    public void DetailPanel_WhenTheOpenVariableHasInstruments_ThenTheyAreListedAndLinkedThere()
    {
        var cut = RenderSearch(new InstrumentClient(Variable(
            Reference(Sf36, "INS_SF36", "Kortversjon 36"),
            Reference(Hads, "INS_HADS", "Angst og depresjon"))));

        cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem__expand-toggle").Click();

        var panel = cut.Find(".munin-explorer-detail");

        Assert.Equal(
            [$"/variabler?utm_source=nyhetsbrev&instrumentId={Sf36}",
             $"/variabler?utm_source=nyhetsbrev&instrumentId={Hads}"],
            panel.QuerySelectorAll("a[href*='instrumentId=']").Select(link => link.GetAttribute("href")));
    }

    [Fact]
    public void DetailPanel_WhenTheOpenVariableHasNoInstrument_ThenThereIsNoRowForOne()
    {
        // "Ikke oppgitt" under an Instrument label would be on nearly every panel in the catalogue.
        var cut = RenderSearch(new InstrumentClient(Variable()));

        cut.Find("ul.munin-explorer-data-list button.munin-explorer-dataitem__expand-toggle").Click();

        Assert.DoesNotContain("Instrument", cut.Find(".munin-explorer-detail").TextContent);
    }

    // -----------------------------------------------------------------------
    // The instrument page

    [Fact]
    public void InstrumentPage_WhenTheAddressNamesAnInstrument_ThenItOpensInPlaceOfTheList()
    {
        var cut = RenderSearch(new InstrumentClient(instrument: Instrument()), instrumentId: Sf36);

        var view = cut.FindComponent<InstrumentView>();

        Assert.Equal("Kortversjon 36", view.Find(".munin-explorer-page__header .headline").TextContent.Trim());
        Assert.Equal("INS_SF36", view.Find(".munin-explorer-page__header .caption").TextContent.Trim());
        Assert.Contains("Et generisk spørreskjema", view.Find(".ingress").TextContent);

        // The period in the words CatalogueDate.Period writes them, so this page and every other
        // surface that draws a range cannot disagree about an open end.
        Assert.Contains(
            CatalogueDate.Period(Instrument().ValidFrom, null, "no", Texts.For("no"))!,
            view.Find($"#{DetailSectionIds.Validity}").TextContent);

        // The catalogue's own properties, resolved exactly as the other detail views resolve them.
        Assert.Contains("Opphav", view.Find($"#{DetailSectionIds.Metadata}").TextContent);
        Assert.Contains("RAND", view.Find($"#{DetailSectionIds.Metadata}").TextContent);

        // The list is not underneath it: the instrument view replaces the rows rather than sitting
        // in one, which is what makes the address a page.
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list"));
    }

    [Fact]
    public void InstrumentPage_WhenItOffersItsVariables_ThenTheQueryNarrowsToThatInstrumentAndNothingElse()
    {
        var cut = RenderSearch(new InstrumentClient(instrument: Instrument()), instrumentId: Sf36);

        var link = cut.FindComponent<InstrumentView>().Find($"#{DetailSectionIds.Variables} a");

        Assert.Equal("Vis alle 12 variabler", link.TextContent.Trim());

        var filter = VariableFilter.Parse(new Uri(new Uri("http://localhost"), link.GetAttribute("href")).Query);

        Assert.Equal([Sf36], filter.InstrumentIds);
        Assert.Equal(VariableFilter.None with { InstrumentIds = [Sf36] }, filter);
    }

    [Fact]
    public void InstrumentPage_Always_ThenItsRootInventsNoClassNameOfItsOwn()
    {
        // The whole reason this view is built on the shared chassis: every name it emits already has
        // a rule in Fhi.Helsedata.Stiler, so shipping it needs no Stiler half at all.
        var cut = RenderSearch(new InstrumentClient(instrument: Instrument()), instrumentId: Sf36);

        var root = cut.FindComponent<InstrumentView>().Find("div");

        Assert.Equal(["munin-explorer-page"], root.ClassList);
    }

    [Fact]
    public void InstrumentPage_WhenTheReaderLeavesIt_ThenTheListIsBackAndTheHostIsTold()
    {
        Guid? reported = Guid.Empty;
        Services.AddSingleton<IMuninExplorerClient>(new InstrumentClient(instrument: Instrument()));

        var cut = Render<VariableSearch>(b => b
            .Add(c => c.SelectedInstrumentId, Sf36)
            .Add(c => c.SelectedInstrumentIdChanged, EventCallback.Factory.Create<Guid?>(
                this, value => reported = value)));

        var exit = cut.Find(".munin-explorer-drilldown button");

        // The words name the list because the list is what the press uncovers. InstrumentExit is
        // what keeps the two together, so a view opened over the whole variable would say so
        // instead of promising a page it does not go to.
        Assert.Equal(Texts.For("no").BackToVariables, exit.TextContent.Trim());

        exit.Click();

        Assert.Null(reported);
        Assert.Empty(cut.FindAll("[id^=munin-instrument-]"));
        Assert.NotEmpty(cut.FindAll("ul.munin-explorer-data-list"));
    }

    [Fact]
    public void InstrumentPage_WhenTheAddressAlsoNamesAVariable_ThenTheWayOutStillLandsWhereItSaysItDoes()
    {
        // The ordinary way in: a variable's Instrument link keeps variabelId beside instrumentId.
        // What that reopens underneath is the row's own disclosure in the list rather than the
        // whole variable, so the button naming the list is the button telling the truth.
        Services.AddSingleton<IMuninExplorerClient>(new InstrumentClient(Variable(), Instrument()));

        var cut = Render<VariableSearch>(b => b
            .Add(c => c.SelectedVariableId, VariableId)
            .Add(c => c.SelectedInstrumentId, Sf36));

        var exit = cut.Find(".munin-explorer-drilldown button");

        Assert.Equal(Texts.For("no").BackToVariables, exit.TextContent.Trim());

        exit.Click();

        Assert.Empty(cut.FindComponents<VariableView>());
        Assert.NotEmpty(cut.FindAll("ul.munin-explorer-data-list"));
    }

    [Fact]
    public void InstrumentPage_WhenTheApiPublishesNoSuchInstrument_ThenItSaysSoWhereTheVariablePageDoes()
    {
        // Null is "not published" rather than a failure — an id in a URL a stranger edited is an
        // ordinary event on a public page — so it reads as the variable drill-in's own not-found
        // state does, in the same status line and wearing the same infobox.
        var cut = RenderSearch(new InstrumentClient(), instrumentId: Sf36);

        var status = cut.Find(".munin-explorer-drilldown p[role=status]");

        Assert.Equal(Texts.For("no").InstrumentMissing, status.TextContent.Trim());
        Assert.Equal("infobox infobox--bg-yellow", status.GetAttribute("class"));
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list"));
    }

    [Fact]
    public void InstrumentPage_WhenTheHostsOwnClientPredatesTheEndpoint_ThenItSaysNotFoundRatherThanFailing()
    {
        // The default body on IMuninExplorerClient.GetInstrumentAsync, exercised by the one caller
        // it exists for: UnupgradedHostClient implements the interface itself and not this member,
        // so a member added without a default stops this build rather than a host's.
        Services.AddSingleton<IMuninExplorerClient>(new UnupgradedHostClient());

        var cut = Render<VariableSearch>(b => b.Add(c => c.SelectedInstrumentId, Sf36));

        Assert.Equal(
            Texts.For("no").InstrumentMissing,
            cut.Find(".munin-explorer-drilldown p[role=status]").TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    // The reader's language

    [Theory]
    // English name curated: the reader sees it, unmarked, because it is already their language.
    [InlineData("en", "Short Form 36", "Short Form 36", null)]
    // None curated: Norwegian stands in, and says so, so a screen reader switches voice for it.
    [InlineData("en", "", "Kortversjon 36", "no")]
    [InlineData("en", null, "Kortversjon 36", "no")]
    // A Norwegian reader gets the catalogue's own name whether or not an English one exists.
    [InlineData("no", "Short Form 36", "Kortversjon 36", null)]
    [InlineData("no", null, "Kortversjon 36", null)]
    public void InstrumentName_WhenTheReaderHasALanguage_ThenTheVariablePageSaysItInThatOne(
        string language, string? englishName, string expected, string? expectedLang)
    {
        var cut = RenderVariableView(
            Variable(Reference(Sf36, "INS_SF36", "Kortversjon 36", englishName)), language);

        var link = Assert.Single(InstrumentLinks(cut));

        Assert.Equal(expected, link.TextContent.Trim());
        Assert.Equal(expectedLang, link.GetAttribute("lang"));
    }

    [Theory]
    [InlineData("en", "Short Form 36", "Short Form 36", null)]
    [InlineData("en", "", "Kortversjon 36", "no")]
    [InlineData("no", "Short Form 36", "Kortversjon 36", null)]
    public void InstrumentName_WhenTheReaderHasALanguage_ThenTheInstrumentPageSaysItInTheSameOne(
        string language, string? englishName, string expected, string? expectedLang)
    {
        // Both ends of the link, because a name resolved twice is a name that can be resolved two
        // ways: a reader pressing "Short Form 36" must not land on a page headed "Kortversjon 36".
        var cut = RenderSearch(
            new InstrumentClient(instrument: Instrument(englishName: englishName)),
            language, instrumentId: Sf36);

        var heading = cut.FindComponent<InstrumentView>().Find(".munin-explorer-page__header .headline");

        Assert.Equal(expected, heading.TextContent.Trim());
        Assert.Equal(expectedLang, heading.GetAttribute("lang"));
    }

    [Theory]
    [InlineData("no", "Short Form 36", "Short Form 36")]
    [InlineData("en", "Short Form 36", null)]
    public void InstrumentMetadata_WhenTheEnglishNameIsCurated_ThenOnlyTheReaderWhoSawItLosesItFromTheList(
        string language, string englishName, string? expected)
    {
        // The metadata block leaves out what the name block above it already drew — and that is the
        // English name for an English reader only. A fixed exclusion would withhold it from the
        // Norwegian reader, who is the one with nowhere else to read it.
        var cut = RenderSearch(
            new InstrumentClient(instrument: Instrument(englishName: englishName)),
            language, instrumentId: Sf36);

        var metadata = cut.FindComponent<InstrumentView>().Find($"#{DetailSectionIds.Metadata}").TextContent;

        if (expected is null)
        {
            Assert.DoesNotContain("Short Form 36", metadata);
        }
        else
        {
            Assert.Contains(expected, metadata);
        }
    }

    [Theory]
    [InlineData("en", "A generic questionnaire.", "A generic questionnaire.", null)]
    [InlineData("en", "", "Et generisk spørreskjema om helserelatert livskvalitet.", "no")]
    [InlineData("no", "A generic questionnaire.", "Et generisk spørreskjema om helserelatert livskvalitet.", null)]
    public void InstrumentDescription_WhenTheReaderHasALanguage_ThenItFollowsTheSameRuleAsTheName(
        string language, string? englishDescription, string expected, string? expectedLang)
    {
        var cut = RenderSearch(
            new InstrumentClient(instrument: Instrument(englishDescription: englishDescription)),
            language, instrumentId: Sf36);

        var ingress = cut.FindComponent<InstrumentView>().Find(".ingress");

        Assert.Equal(expected, ingress.TextContent.Trim());
        Assert.Equal(expectedLang, ingress.GetAttribute("lang"));
    }

    // -----------------------------------------------------------------------
    // The address bar

    [Fact]
    public void Address_WhenALinkCarriesAnInstrument_ThenTheExplorerOpensOnItAndKeepsTheHostsParameters()
    {
        Services.AddSingleton<IMuninExplorerClient>(new InstrumentClient(instrument: Instrument()));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));

        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"http://localhost/variabler?utm_source=nyhetsbrev&instrumentId={Sf36}");

        var cut = Render<VariableExplorer>();

        Assert.Equal(
            "Kortversjon 36",
            cut.FindComponent<InstrumentView>().Find(".munin-explorer-page__header .headline").TextContent.Trim());

        // The address a variable's Instrument entry gets is this page with one key added, so the
        // host's own parameter is still on it and leaving the instrument lands where they were.
        var page = cut.FindComponent<VariableSearch>().Instance.InstrumentHref!(Hads);

        Assert.Equal($"/variabler?utm_source=nyhetsbrev&instrumentId={Hads}", page);

        // The link out of the page is built by the explorer rather than by the host, and it narrows
        // to this instrument alone — the count it names is the instrument's own.
        var href = cut.FindComponent<VariableSearch>().Instance.InstrumentVariablesHref!(Sf36);
        var state = ExplorerUrlState.Parse(new Uri(new Uri("http://localhost"), href).Query);

        Assert.Equal(VariableFilter.None with { InstrumentIds = [Sf36] }, state.Filter);
        Assert.Null(state.SelectedInstrumentId);
        Assert.Contains("utm_source=nyhetsbrev", href);
    }

    [Fact]
    public void Address_WhenItNamesBothAVariableAndAnInstrument_ThenTheInstrumentIsTheOneOpened()
    {
        // A link made on the instrument page carries both, because the variable underneath was
        // never torn down. Reopening the variable instead would send the sender's reader somewhere
        // the sender was not.
        Services.AddSingleton<IMuninExplorerClient>(
            new InstrumentClient(Variable(), Instrument()));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));

        Services.GetRequiredService<NavigationManager>()
                .NavigateTo($"http://localhost/variabler?variabelId={VariableId}&instrumentId={Sf36}");

        var cut = Render<VariableExplorer>();

        Assert.Single(cut.FindComponents<InstrumentView>());
        Assert.Empty(cut.FindComponents<VariableView>());
    }

    [Fact]
    public void QueryKeys_Always_ThenInstrumentIdIsOneTheExplorerDeclaresAndCanBeDeclined()
    {
        // A host with an instrument page of its own plausibly already means something by the key,
        // and a key Parse reads but the set does not name is the defect ExplorerUrlState's own
        // remarks describe: the host keeps it as one of theirs and it ends up in the URL twice.
        Assert.Contains("instrumentId", ExplorerUrlState.ScalarQueryKeys);
        Assert.Contains("instrumentId", ExplorerUrlState.QueryKeys);

        var state = ExplorerUrlState.Parse($"?instrumentId={Sf36}");

        Assert.Equal(Sf36, state.SelectedInstrumentId);
        Assert.Equal($"instrumentId={Sf36}", state.ToQueryString());
    }
}
