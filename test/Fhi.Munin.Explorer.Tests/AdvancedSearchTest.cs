using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

// Avansert søk (Fhi.Metadata-ipok2.2): conditions in the filter and the link, the form, the chips, and the switch.
public class AdvancedSearchTest : ExplorerTestContext
{
    private static readonly IReadOnlyList<ConditionField> Fields =
        JsonSerializer.Deserialize<IReadOnlyList<ConditionField>>(TestData.Read("vilkar.json"), MuninExplorerClient.Json)!;

    private static ConditionField Field(string token) => Fields.Single(field => field.Field == token);

    private sealed class ConditionClient : EmptyMuninExplorerClient
    {
        public List<VariableFilter?> Searches { get; } = [];

        public IReadOnlyList<ConditionField> Answer { get; init; } = Fields;

        public int FieldCalls { get; private set; }

        public int FailFieldCalls { get; init; }

        public TaskCompletionSource? HeldVocabulary { get; init; }

        public override async Task<IReadOnlyList<PropertyMetadataEntry>> GetKildePropertyMetadataAsync(CancellationToken cancellationToken = default)
        {
            if (HeldVocabulary is { } held)
            {
                await held.Task;
            }

            return [];
        }

        public override Task<IReadOnlyList<ConditionField>> GetConditionFieldsAsync(CancellationToken cancellationToken = default)
        {
            FieldCalls++;
            return FieldCalls <= FailFieldCalls
                ? Task.FromException<IReadOnlyList<ConditionField>>(new HttpRequestException("503"))
                : Task.FromResult(Answer);
        }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(filter);
            return Task.FromResult(new Page<VariableSummary>
            {
                Items = [new VariableSummary { Id = Guid.NewGuid(), Code = "V_A", PreferredTerm = "Blodtrykk" }],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1
            });
        }
    }

    private IRenderedComponent<VariableSearch> RenderWith(
        ConditionClient client, bool offered = true, VariableFilter? filter = null, Action<VariableFilter>? changed = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton(new AdvancedSearchSwitch(offered));
        return Render<VariableSearch>(b =>
        {
            b.Add(c => c.Filter, filter);
            if (changed is not null)
            {
                b.Add(c => c.FilterChanged, changed);
            }
        });
    }

    private static IElement? Toggle(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button[aria-expanded]").FirstOrDefault(b => b.TextContent.StartsWith("Avansert søk", StringComparison.Ordinal));

    private static IElement Button(IRenderedComponent<VariableSearch> cut, string text) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static IReadOnlyList<IElement> Rows(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("fieldset fieldset").Where(f => f.QuerySelector("legend")?.TextContent.StartsWith("Betingelse", StringComparison.Ordinal) == true).ToList();

    private static SearchCondition Condition(string text) =>
        SearchCondition.TryParse(text, out var condition) ? condition : throw new ArgumentException(text);

    // --- The contract ---

    [Fact]
    public void ToQueryString_WhenConditionsAreSet_ThenEachIsARepeatedVilkarThatParsesBack()
    {
        var filter = new VariableFilter
        {
            Conditions = [Condition("beskrivelse:inneholder:blodtrykk"), Condition("tilgangsniva:erEnAv:apne-data,statistikkbank")]
        };

        var query = filter.ToQueryString();

        Assert.Equal("vilkar=beskrivelse%3Ainneholder%3Ablodtrykk&vilkar=tilgangsniva%3AerEnAv%3Aapne-data%2Cstatistikkbank", query);
        Assert.Equal(filter, VariableFilter.Parse(query));
        Assert.Equal(2, filter.ActiveCount);
        Assert.Contains("vilkar", VariableFilter.QueryKeys);
        Assert.Contains("VILKAR", ExplorerUrlState.QueryKeys);
    }

    [Fact]
    public void Parse_WhenATextValueIsAsLongAsTheApiTakes_ThenTheConditionIsKept()
    {
        // The generic 200-character cap on a parameter would drop it: field and operator come on top of the value.
        var value = new string('x', SearchCondition.MaxTextLength);

        var parsed = VariableFilter.Parse("vilkar=" + Uri.EscapeDataString($"beskrivelse:inneholderIkke:{value}"));

        Assert.Equal(value, Assert.Single(parsed.Conditions).Value);
    }

    [Theory]
    [InlineData("vilkar=navn")]
    [InlineData("vilkar=navn%3Ainneholder")]
    [InlineData("vilkar=navn%3Ainneholder%3A%20%20")]
    [InlineData("vilkar=n%20a%3Ainneholder%3Ax")]
    [InlineData("vilkar=%3Ainneholder%3Ax")]
    public void Parse_WhenAConditionIsMalformed_ThenItIsDropped(string query) =>
        Assert.Empty(VariableFilter.Parse(query).Conditions);

    [Fact]
    public void Parse_WhenALinkCarriesMoreConditionsThanTheApiTakes_ThenOnlyTheFirstTenAreKept()
    {
        var query = string.Join('&', Enumerable.Range(0, 12).Select(i => $"vilkar=navn%3Ainneholder%3A{i}"));

        var parsed = VariableFilter.Parse(query);

        Assert.Equal(SearchCondition.MaxCount, parsed.Conditions.Count);
        Assert.Equal("9", parsed.Conditions[^1].Value);
    }

    [Fact]
    public void ToQuery_WhenMoreConditionsThanTheApiTakesAreSet_ThenOnlyTenAreSent()
    {
        var filter = new VariableFilter { Conditions = [.. Enumerable.Range(0, 12).Select(i => Condition($"navn:inneholder:{i}"))] };

        Assert.Equal(SearchCondition.MaxCount, filter.ToQuery().Count(pair => pair.Name == "vilkar"));
    }

    [Theory]
    [InlineData("navn", "inneholder", "blod", true)]
    [InlineData("navn", "mellom", "blod", false)]
    [InlineData("gyldigFra", "mellom", "2010-01-01..2020-12-31", true)]
    [InlineData("gyldigFra", "mellom", "2020-01-01..2010-12-31", false)]
    [InlineData("gyldigFra", "mellom", "2010-01-01", false)]
    [InlineData("gyldigFra", "for", "2010-01-01", true)]
    [InlineData("gyldigFra", "etter", "01.01.2010", false)]
    [InlineData("tilgangsniva", "harAlle", "apne-data,statistikkbank", true)]
    [InlineData("tilgangsniva", "erEnAv", "apne-data,ukjent", false)]
    [InlineData("personidentifikasjon", "harAlle", "anonymous", false)]
    [InlineData("personidentifikasjon", "erIkkeEnAv", "ANONYMOUS", true)]
    public void Accepts_WhenAConditionIsCheckedAgainstItsField_ThenOnlyWhatTheApiTakesPasses(
        string field, string op, string value, bool accepted) =>
        Assert.Equal(accepted, Field(field).Accepts(new SearchCondition(field, op, value)));

    [Theory]
    [InlineData("navn", "inneholder", "   ")]
    [InlineData("tilgangsniva", "erEnAv", ",")]
    [InlineData("tilgangsniva", "erEnAv", " , ")]
    public void Accepts_WhenTheValueHoldsNothing_ThenItIsRefused(string field, string op, string value) =>
        Assert.False(Field(field).Accepts(new SearchCondition(field, op, value)));

    [Theory]
    [InlineData(SearchCondition.MaxTextLength, true)]
    [InlineData(SearchCondition.MaxTextLength + 1, false)]
    public void Accepts_WhenATextIsAtOrPastItsCap_ThenOnlyTheCapPasses(int length, bool accepted) =>
        Assert.Equal(accepted, Field("navn").Accepts(new SearchCondition("navn", "inneholder", new string('a', length))));

    [Theory]
    [InlineData(SearchCondition.MaxCodes, true)]
    [InlineData(SearchCondition.MaxCodes + 1, false)]
    public void Accepts_WhenTheCodesAreAtOrPastTheirCap_ThenOnlyTheCapPasses(int count, bool accepted) =>
        Assert.Equal(accepted, Field("tilgangsniva").Accepts(
            new SearchCondition("tilgangsniva", "erEnAv", string.Join(',', Enumerable.Repeat("apne-data", count)))));

    // --- The client and the switch ---

    [Fact]
    public async Task GetConditionFieldsAsync_WhenTheApiAnswers_ThenTheFieldsAreReadFromItsRoute()
    {
        var handler = StubHttpHandler.Ok(TestData.Read("vilkar.json"));
        var client = new MuninExplorerClient(new HttpClient(handler) { BaseAddress = new Uri("https://runa.munin.skytest.fhi.no/") });

        var fields = await client.GetConditionFieldsAsync();

        Assert.Equal("/api/explorer/variables/vilkar", handler.LastUri?.AbsolutePath);
        Assert.Equal(7, fields.Count);
        Assert.Equal(["statistikkbank", "tilgang-krever-soknad", "apne-data", "soknad-via-helsedata"], fields.Single(f => f.Field == "tilgangsniva").Options);
        Assert.Empty(fields.Single(f => f.Field == "navn").Options);
    }

    [Fact]
    public async Task GetConditionFieldsAsync_WhenTheApiPredatesAdvancedSearch_ThenTheListIsEmpty()
    {
        var client = new MuninExplorerClient(new HttpClient(StubHttpHandler.Status(HttpStatusCode.NotFound))
        {
            BaseAddress = new Uri("https://runa.munin.skytest.fhi.no/")
        });

        Assert.Empty(await client.GetConditionFieldsAsync());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void AddMuninExplorer_WhenTheDelegateOverloadsHostHasTheKey_ThenAdvancedSearchFollowsIt(string? value, bool on)
    {
        var services = new ServiceCollection();
        var settings = new Dictionary<string, string?>();
        if (value is not null)
        {
            settings["MuninExplorer:Features:AdvancedSearch"] = value;
        }

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        var provider = services.AddMuninExplorer(o => o.ApiBaseUrl = "https://explorer.munin.skytest.fhi.no").BuildServiceProvider();

        Assert.Equal(on, provider.GetRequiredService<AdvancedSearchSwitch>().On);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("true", true)]
    [InlineData("yes", false)]
    public void AddMuninExplorer_WhenTheConfigurationOverloadsSectionHasTheKey_ThenAdvancedSearchFollowsIt(string? value, bool on)
    {
        var settings = new Dictionary<string, string?> { ["MuninExplorer:ApiBaseUrl"] = "https://explorer.munin.skytest.fhi.no" };
        if (value is not null)
        {
            settings["MuninExplorer:Features:AdvancedSearch"] = value;
        }

        var provider = new ServiceCollection()
            .AddMuninExplorer(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();

        Assert.Equal(on, provider.GetRequiredService<AdvancedSearchSwitch>().On);
    }

    [Fact]
    public void AddMuninExplorer_WhenTheDelegateSetsAdvancedSearch_ThenItWinsOverTheKey()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MuninExplorer:Features:AdvancedSearch"] = "true" }).Build());

        var provider = services.AddMuninExplorer(o =>
        {
            o.ApiBaseUrl = "https://explorer.munin.skytest.fhi.no";
            o.Features.AdvancedSearch = false;
        }).BuildServiceProvider();

        Assert.False(provider.GetRequiredService<AdvancedSearchSwitch>().On);
    }

    // --- The component ---

    [Fact]
    public void Render_WhenTheFeatureIsOff_ThenTheLinksConditionsAreDroppedAndTheHostIsTold()
    {
        // A narrowing the reader can neither see nor remove is worse than none.
        VariableFilter? reported = null;
        var client = new ConditionClient();
        var cut = RenderWith(client, offered: false,
            filter: new VariableFilter { Conditions = [Condition("navn:inneholder:blod")] }, changed: f => reported = f);

        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        Assert.Null(Toggle(cut));
        Assert.Empty(client.Searches[0]!.Conditions);
        Assert.Equal(0, client.FieldCalls);
        Assert.NotNull(reported);
        Assert.Empty(reported!.Conditions);
        Assert.DoesNotContain("munin-explorer-filters__chip", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenTheFeatureIsOffAndTheLinkHasNoConditions_ThenTheHostIsNotTold()
    {
        VariableFilter? reported = null;
        var client = new ConditionClient();
        var cut = RenderWith(client, offered: false, changed: f => reported = f);

        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        Assert.Null(reported);
    }

    [Fact]
    public void Render_WhenALinkCarriesAConditionTheApiWouldRefuse_ThenOnlyThatOneIsDropped()
    {
        VariableFilter? reported = null;
        var client = new ConditionClient();
        var cut = RenderWith(client, filter: new VariableFilter
        {
            Conditions = [Condition("navn:inneholder:blod"), Condition("navn:mellom:blod"), Condition("ukjent:erLik:x")]
        }, changed: f => reported = f);

        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        Assert.Equal([Condition("navn:inneholder:blod")], client.Searches[0]!.Conditions);
        Assert.Equal([Condition("navn:inneholder:blod")], reported!.Conditions);
    }

    [Fact]
    public void Render_WhenALinkCarriesConditions_ThenTheyAreSearchedChippedAndCountedOnTheToggle()
    {
        // A shared URL with conditions restores the same search (acceptance 4).
        var client = new ConditionClient();
        var filter = VariableFilter.Parse("vilkar=beskrivelse%3Ainneholder%3Ablodtrykk&vilkar=tilgangsniva%3AerEnAv%3Astatistikkbank");
        var cut = RenderWith(client, filter: filter);

        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        Assert.Equal(filter.Conditions, client.Searches[0]!.Conditions);
        Assert.Equal("Avansert søk (2)", Toggle(cut)!.TextContent);
        Assert.Contains("Beskrivelse inneholder «blodtrykk»", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Tilgangsnivå er en av statistikkbank", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.FindAll("button").SingleOrDefault(b => b.TextContent == "Endre avansert søk"));
    }

    [Fact]
    public void ShowHits_WhenARowIsFilledIn_ThenTheSearchCarriesItAndTheFormCloses()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        var row = Assert.Single(Rows(cut));
        row.QuerySelectorAll("select")[0].Change("beskrivelse");
        Rows(cut)[0].QuerySelectorAll("select")[1].Change("inneholderIkke");
        Rows(cut)[0].QuerySelector("input[type=text]")!.Change("  blodtrykk ");
        Button(cut, "Vis treff").Click();

        cut.WaitForAssertion(() => Assert.Equal([Condition("beskrivelse:inneholderIkke:blodtrykk")], client.Searches[^1]!.Conditions));
        Assert.Equal("false", Toggle(cut)!.GetAttribute("aria-expanded"));
        Assert.Equal("Avansert søk (1)", Toggle(cut)!.TextContent);
    }

    [Fact]
    public void ShowHits_WhenAFieldIsAChoiceList_ThenTheTickedCodesGoInTheApisOrder()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        Rows(cut)[0].QuerySelectorAll("select")[0].Change("tilgangsniva");
        var boxes = Rows(cut)[0].QuerySelectorAll("input[type=checkbox]");
        Assert.Equal(4, boxes.Length);
        boxes[2].Change(true);
        Rows(cut)[0].QuerySelectorAll("input[type=checkbox]")[0].Change(true);
        Button(cut, "Vis treff").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal([Condition("tilgangsniva:erEnAv:statistikkbank,apne-data")], client.Searches[^1]!.Conditions));
    }

    [Fact]
    public void ShowHits_WhenARangeHasOneDate_ThenNothingIsAppliedAndTheRowSaysWhy()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        var before = client.Searches.Count;

        Toggle(cut)!.Click();
        Rows(cut)[0].QuerySelectorAll("select")[0].Change("gyldigFra");
        Assert.Equal(2, Rows(cut)[0].QuerySelectorAll("input[type=date]").Length);
        Rows(cut)[0].QuerySelectorAll("input[type=date]")[0].Change("2010-01-01");
        Button(cut, "Vis treff").Click();

        Assert.Equal(before, client.Searches.Count);
        Assert.Contains("Skriv to gyldige datoer", Rows(cut)[0].QuerySelector("[role=alert]")!.TextContent, StringComparison.Ordinal);
        Assert.Equal("true", Rows(cut)[0].QuerySelector("input[type=date]")!.GetAttribute("aria-invalid"));
    }

    [Fact]
    public void ShowHits_WhenEveryRowIsEmpty_ThenTheConditionsInForceAreRemoved()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client, filter: new VariableFilter { Conditions = [Condition("navn:inneholder:blod")] });
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        Toggle(cut)!.Click();
        Button(cut, "Nullstill").Click();
        Button(cut, "Vis treff").Click();

        cut.WaitForAssertion(() => Assert.Empty(client.Searches[^1]!.Conditions));
        Assert.Equal("Avansert søk", Toggle(cut)!.TextContent);
    }

    [Fact]
    public void Form_WhenRowsAreAddedAndRemoved_ThenEachRowAndControlHasADistinctName()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        Button(cut, "Legg til betingelse").Click();
        Button(cut, "Legg til betingelse").Click();

        var names = Rows(cut).SelectMany(row => row.QuerySelectorAll("select, input, button")).Select(AccessibleName.Of).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Contains("Betingelse 3 Operator", names);
        Assert.Contains("Fjern betingelse 2", names);

        Button(cut, "Fjern betingelse 2").Click();

        Assert.Equal(["Betingelse 1", "Betingelse 2"], Rows(cut).Select(r => r.QuerySelector("legend")!.TextContent));
    }

    [Fact]
    public void Form_WhenTenRowsAreThere_ThenNoMoreCanBeAdded()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();
        for (var i = 1; i < SearchCondition.MaxCount; i++)
        {
            Button(cut, "Legg til betingelse").Click();
        }

        Assert.Equal(SearchCondition.MaxCount, Rows(cut).Count);
        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent == "Legg til betingelse");
    }

    [Fact]
    public void Form_WhenTheApiOffersNoFields_ThenItSaysAdvancedSearchIsUnavailable()
    {
        var client = new ConditionClient { Answer = [] };
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();

        Assert.Contains("Avansert søk er ikke tilgjengelig", cut.Find("[role=alert]").TextContent, StringComparison.Ordinal);
        Assert.Empty(Rows(cut));
    }

    [Fact]
    public void Chip_WhenOneConditionIsRemoved_ThenOnlyThatOneLeavesTheSearch()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client, filter: new VariableFilter
        {
            Conditions = [Condition("navn:inneholder:blod"), Condition("kode:begynnerMed:V_")]
        });
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        cut.FindAll("button").Single(b => b.GetAttribute("aria-label") == "Fjern filteret Navn inneholder «blod»").Click();

        cut.WaitForAssertion(() => Assert.Equal([Condition("kode:begynnerMed:V_")], client.Searches[^1]!.Conditions));
    }

    [Fact]
    public void ClearAll_WhenFacetsAndConditionsAreSet_ThenBothGo()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client, filter: new VariableFilter
        {
            DataTypes = ["tekst"],
            Conditions = [Condition("navn:inneholder:blod")]
        });
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        cut.FindAll(".munin-explorer-filters__active button").Single(b => b.TextContent == "Fjern alle filtre").Click();

        cut.WaitForAssertion(() => Assert.True(client.Searches[^1]!.IsEmpty));
    }

    [Fact]
    public void Edit_WhenPressedFromTheChipRow_ThenTheFormOpensOnTheConditionsInForce()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client, filter: new VariableFilter
        {
            Conditions = [Condition("gyldigFra:mellom:2010-01-01..2020-12-31"), Condition("personidentifikasjon:erEnAv:anonymous")]
        });
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        Button(cut, "Endre avansert søk").Click();

        var rows = Rows(cut);
        Assert.Equal(2, rows.Count);
        Assert.Equal(["2010-01-01", "2020-12-31"], rows[0].QuerySelectorAll("input[type=date]").Select(i => i.GetAttribute("value")));
        Assert.True(rows[1].QuerySelectorAll("input[type=checkbox]").Single(b => b.ParentElement!.TextContent.Contains("Anonym", StringComparison.Ordinal)).HasAttribute("checked"));
    }

    [Fact]
    public void Chip_WhenTheReaderIsEnglish_ThenTheConditionReadsInEnglish()
    {
        var client = new ConditionClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        var cut = Render<VariableSearch>(b => b
            .Add(c => c.Language, "en")
            .Add(c => c.Filter, new VariableFilter { Conditions = [Condition("gyldigFra:for:2015-06-01"), Condition("navn:erLik:BMI")] }));

        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));

        Assert.Contains("Valid from before 2015-06-01", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Name is “BMI”", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("Advanced search (2)", cut.FindAll("button[aria-expanded]").First(b => b.TextContent.StartsWith("Advanced", StringComparison.Ordinal)).TextContent);
    }

    // --- Focus: where a keyboard reader lands after each press ---

    // bUnit writes an element's reference only into the render that created it, so each is noted while it shows.
    private readonly Dictionary<string, string> _references = [];

    private void Remember(IRenderedComponent<VariableSearch> cut)
    {
        foreach (var element in cut.FindAll("select, button"))
        {
            if (element.GetAttribute("blazor:elementReference") is { Length: > 0 } reference)
            {
                _references[Key(element)] = reference;
            }
        }
    }

    // A button with no id is known by its words, less the count «Avansert søk (1)» gains once a condition applies.
    private static string Key(IElement element) => element.Id is { Length: > 0 } id
        ? id
        : $"{element.TagName}:{System.Text.RegularExpressions.Regex.Replace(element.TextContent.Trim(), @" \(\d+\)$", "")}";

    private void AssertFocusOn(IRenderedComponent<VariableSearch> cut, Func<IElement> target) =>
        cut.WaitForAssertion(() =>
        {
            Remember(cut);
            var focused = (ElementReference)JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0]!;
            Assert.Equal(_references[Key(target())], focused.Id);
        });

    private static IElement FieldList(IElement row) => row.QuerySelector("select")!;

    private IRenderedComponent<VariableSearch> OpenWithRows(int rows)
    {
        var cut = RenderWith(new ConditionClient());
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Remember(cut);
        Toggle(cut)!.Click();
        Remember(cut);
        for (var row = 1; row < rows; row++)
        {
            Button(cut, "Legg til betingelse").Click();
            Remember(cut);
        }

        cut.WaitForAssertion(() => Assert.Equal(rows, Rows(cut).Count));
        Remember(cut);
        return cut;
    }

    [Fact]
    public void ShowHits_WhenTwoRowsSayTheSame_ThenTheConditionIsAppliedOnce()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Toggle(cut)!.Click();
        Rows(cut)[0].QuerySelector("input[type=text]")!.Change("blod");
        Button(cut, "Legg til betingelse").Click();
        Rows(cut)[1].QuerySelector("input[type=text]")!.Change("blod");

        Button(cut, "Vis treff").Click();

        cut.WaitForAssertion(() => Assert.Equal([Condition("navn:inneholder:blod")], client.Searches[^1]!.Conditions));
    }

    [Theory]
    [InlineData(0, "navn:inneholder:a")]
    [InlineData(1, "navn:inneholder:a")]
    [InlineData(0, "Navn:INNEHOLDER:a")]
    [InlineData(1, "Navn:INNEHOLDER:a")]
    public void Render_WhenALinkCarriesTheSameConditionTwice_ThenItIsSearchedAndShownOnce(int failedFieldCalls, string again)
    {
        var client = new ConditionClient { FailFieldCalls = failedFieldCalls };
        var cut = RenderWith(client, filter: new VariableFilter { Conditions = [Condition("navn:inneholder:a"), Condition(again)] });

        cut.WaitForAssertion(() => Assert.Equal([Condition("navn:inneholder:a")], client.Searches[^1]!.Conditions));
        Assert.Single(cut.FindAll("button"), b => b.GetAttribute("aria-label") == "Fjern filteret Navn inneholder «a»");
    }

    [Fact]
    public async Task Edit_WhenPressedWhileTheOpeningStillLoads_ThenFocusStaysOnTheFirstRowItLands()
    {
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderWith(new ConditionClient { HeldVocabulary = held },
            filter: new VariableFilter { Conditions = [Condition("navn:inneholder:a")] });
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Toggle(cut)!.Click();
        Button(cut, "Endre avansert søk").Click();

        await cut.InvokeAsync(held.SetResult);
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        Remember(cut);
        Rows(cut)[0].QuerySelector("input")!.Change("b");
        Remember(cut);

        AssertFocusOn(cut, () => FieldList(Rows(cut)[0]));
    }

    [Fact]
    public void Open_WhenTheBrowserRefusesFocus_ThenTheFormStillOpens()
    {
        JSInterop.SetupVoid("Blazor._internal.domWrapper.focus", _ => true).SetException(new JSException("gone"));
        var cut = RenderWith(new ConditionClient());
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Toggle(cut)!.Click();

        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        Button(cut, "Legg til betingelse").Click();
        Assert.Equal(2, Rows(cut).Count);
    }

    [Fact]
    public void ShowHits_WhenTheTextHoldsAPastedTab_ThenItIsAppliedAsASpaceALinkCanCarry()
    {
        var client = new ConditionClient();
        var cut = RenderWith(client);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Toggle(cut)!.Click();
        Rows(cut)[0].QuerySelector("input[type=text]")!.Change("blod\ttrykk");

        Button(cut, "Vis treff").Click();

        cut.WaitForAssertion(() => Assert.Equal([Condition("navn:inneholder:blod trykk")], client.Searches[^1]!.Conditions));
    }

    [Fact]
    public void Open_WhenEndreWasPressedWhileTheFieldsWereUnavailable_ThenTheNextOpeningKeepsFocusOnTheFirstRow()
    {
        var filter = new VariableFilter { Conditions = [Condition("navn:inneholder:a")] };
        var cut = RenderWith(new ConditionClient { FailFieldCalls = 2 }, filter: filter);
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        Toggle(cut)!.Click();
        Button(cut, "Endre avansert søk").Click();
        Toggle(cut)!.Click();

        Toggle(cut)!.Click();
        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        Remember(cut);
        Rows(cut)[0].QuerySelector("input")!.Change("b");
        Remember(cut);

        AssertFocusOn(cut, () => FieldList(Rows(cut)[0]));
    }

    [Fact]
    public void Open_WhenTheFormOpens_ThenFocusGoesToTheFirstRowsFieldList()
    {
        var cut = OpenWithRows(1);

        AssertFocusOn(cut, () => FieldList(Rows(cut)[0]));
    }

    [Fact]
    public void AddCondition_WhenARowIsAdded_ThenFocusGoesToItsFieldList()
    {
        var cut = OpenWithRows(2);

        AssertFocusOn(cut, () => FieldList(Rows(cut)[1]));
    }

    [Fact]
    public void RemoveDraft_WhenAnEarlierRowGoes_ThenFocusGoesToTheRowTakingItsPlace()
    {
        // Focus starts on row 3, the last added, so only the removal can move it to what was row 2.
        var cut = OpenWithRows(3);
        var second = Key(FieldList(Rows(cut)[1]));

        Button(cut, "Fjern betingelse 1").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, Rows(cut).Count));
        Assert.Equal(second, Key(FieldList(Rows(cut)[0])));
        AssertFocusOn(cut, () => FieldList(Rows(cut)[0]));
    }

    [Fact]
    public void RemoveDraft_WhenTheLastRowGoes_ThenFocusGoesToTheRowBefore()
    {
        var cut = OpenWithRows(2);

        Button(cut, "Fjern betingelse 2").Click();

        cut.WaitForAssertion(() => Assert.Single(Rows(cut)));
        AssertFocusOn(cut, () => FieldList(Rows(cut)[0]));
    }

    [Fact]
    public void RemoveDraft_WhenTheOnlyRowGoes_ThenFocusGoesToTheAddButton()
    {
        var cut = OpenWithRows(1);

        Button(cut, "Fjern betingelse 1").Click();

        cut.WaitForAssertion(() => Assert.Empty(Rows(cut)));
        AssertFocusOn(cut, () => Button(cut, "Legg til betingelse"));
    }

    [Fact]
    public void ShowHits_WhenARowIsRefused_ThenFocusGoesToThatRowsFieldList()
    {
        // Row 1 is refused while focus is on row 2, where adding it left it, so only the refusal can move it back.
        var cut = OpenWithRows(2);
        Rows(cut)[0].QuerySelector("select")!.Change("gyldigFra");
        Rows(cut)[0].QuerySelectorAll("input[type=date]")[0].Change("2010-01-01");
        Rows(cut)[1].QuerySelector("input[type=text]")!.Change("blodtrykk");
        AssertFocusOn(cut, () => FieldList(Rows(cut)[1]));

        Button(cut, "Vis treff").Click();

        AssertFocusOn(cut, () => FieldList(Rows(cut)[0]));
    }

    [Fact]
    public void ShowHits_WhenTheRowsApply_ThenFocusReturnsToTheToggle()
    {
        var cut = OpenWithRows(1);
        Rows(cut)[0].QuerySelector("input[type=text]")!.Change("blodtrykk");

        Button(cut, "Vis treff").Click();

        cut.WaitForAssertion(() => Assert.Equal("false", Toggle(cut)!.GetAttribute("aria-expanded")));
        AssertFocusOn(cut, () => Toggle(cut)!);
    }
}
