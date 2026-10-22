using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// One rule across the component families, so a family that stops following it fails beside the
// rest (Fhi.Metadata-pfejp.7). Each fixture keeps one field always translated, so a fallback case
// also fails on a component that ignores the English altogether.
public sealed class EnglishCatalogueTextTest : ExplorerTestContext
{
    private const string NorwegianName = "Norsk pasientregister";
    private const string EnglishName = "Norwegian Patient Registry";
    private const string NorwegianDescription = "Registeret dekker alle sykehusopphold.";
    private const string EnglishDescription = "The registry covers every hospital stay.";

    /// <summary>The innermost element whose own text is exactly <paramref name="text"/>.</summary>
    private static IElement Holding<T>(IRenderedComponent<T> cut, string text, string selector = "*")
        where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll(selector).Where(element => element.TextContent.Trim() == text).Last();

    /// <summary>The language the text is marked with, or null where it inherits the host page's.</summary>
    private static string? LangOf(IElement element)
    {
        for (var at = element; at is not null; at = at.ParentElement)
        {
            if (at.GetAttribute("lang") is { } lang)
            {
                return lang;
            }
        }

        return null;
    }

    private static bool Shows<T>(IRenderedComponent<T> cut, string text)
        where T : Microsoft.AspNetCore.Components.IComponent =>
        cut.FindAll("*").Any(element => element.TextContent.Trim() == text);

    // ---- Contracts ----------------------------------------------------------------------------

    [Fact]
    public void VariableRow_WhenMuninSendsEnglishNames_ThenEachLandsInItsOwnProperty()
    {
        var row = JsonSerializer.Deserialize<VariableSummary>(
            """
            {
              "preferredTerm": "Fødselsvekt", "preferredTermEn": "Birth weight",
              "kildeName": "Medisinsk fødselsregister", "kildeNameEn": "Medical Birth Registry",
              "datasamlingName": "Fødsler", "datasamlingNameEn": "Births",
              "variabelgruppeName": "Barnet", "variabelgruppeNameEn": "The child"
            }
            """,
            MuninExplorerClient.Json)!;

        Assert.Equal("Birth weight", row.PreferredTermEn);
        Assert.Equal("Medical Birth Registry", row.KildeNameEn);
        Assert.Equal("Births", row.DatasamlingNameEn);
        Assert.Equal("The child", row.VariabelgruppeNameEn);
    }

    [Fact]
    public void VariableRow_WhenMuninSendsNoEnglish_ThenTheEnglishPropertiesAreNull()
    {
        var row = JsonSerializer.Deserialize<VariableSummary>("""{ "preferredTerm": "Fødselsvekt" }""", MuninExplorerClient.Json)!;

        Assert.Null(row.PreferredTermEn);
        Assert.Null(row.KildeNameEn);
        Assert.Null(row.DatasamlingNameEn);
        Assert.Null(row.VariabelgruppeNameEn);
    }

    [Fact]
    public void Facets_WhenMuninSendsTheNorwegianBesideTheResolvedName_ThenEveryNamedFacetCarriesIt()
    {
        // A facet's name follows Accept-Language; nameNo is the Norwegian whatever it asked for.
        var facets = JsonSerializer.Deserialize<FilterOptions>(
            """
            {
              "kilder": [ { "name": "Source", "nameNo": "Kilde" } ],
              "delkilder": [ { "name": "Sub-source", "nameNo": "Delkilde" } ],
              "datasamlinger": [ { "name": "Collection", "nameNo": "Datasamling" } ],
              "variabelgrupper": [ { "name": "Group", "nameNo": "Gruppe" } ],
              "hierarkiVariabelgrupper": [ { "name": "Group", "nameNo": "Gruppe" }, { "name": "Uten" } ],
              "filtere": [ { "name": "Saved filter", "nameNo": "Filter" } ]
            }
            """,
            MuninExplorerClient.Json)!;

        Assert.Equal("Kilde", facets.Kilder[0].NameNo);
        Assert.Equal("Delkilde", facets.Delkilder[0].NameNo);
        Assert.Equal("Datasamling", facets.Datasamlinger[0].NameNo);
        Assert.Equal("Gruppe", facets.Variabelgrupper[0].NameNo);
        Assert.Equal("Gruppe", facets.HierarchyVariabelgrupper[0].NameNo);
        Assert.Null(facets.HierarchyVariabelgrupper[1].NameNo);
        Assert.Equal("Filter", facets.Filters[0].NameNo);
    }

    [Fact]
    public void HierarchyGroup_WhenMuninSendsAnEnglishName_ThenItIsCarried()
    {
        var group = JsonSerializer.Deserialize<HierarchyVariabelgruppe>(
            """{ "name": "Barnet", "nameEn": "The child" }""", MuninExplorerClient.Json)!;

        Assert.Equal("The child", group.NameEn);
        Assert.Null(JsonSerializer.Deserialize<HierarchyVariabelgruppe>("""{ "name": "Barnet" }""", MuninExplorerClient.Json)!.NameEn);
    }

    // ---- The rule itself ----------------------------------------------------------------------

    [Theory]
    [InlineData("en", "English", "English", false)]
    [InlineData("en-GB", "English", "English", false)]
    [InlineData("en", null, "Norsk", true)]
    [InlineData("en", "  ", "Norsk", true)]
    [InlineData("no", "English", "Norsk", true)]
    [InlineData(null, "English", "Norsk", true)]
    public void Pick_WhenAskedForAField_ThenEnglishOnlyForAnEnglishReaderWithEnglishToShow(
        string? reader, string? english, string expected, bool norwegian)
    {
        Assert.Equal((expected, norwegian), ReaderLanguage.Pick("Norsk", english, reader));
    }

    // ---- KildeView ----------------------------------------------------------------------------

    private static KildeDetail Kilde(bool english) => new()
    {
        Id = Guid.NewGuid(),
        Code = "K_NPR",
        PreferredTerm = NorwegianName,
        PreferredTermEn = english ? EnglishName : null,
        Description = NorwegianDescription,
        DescriptionEn = english ? EnglishDescription : null,
        Delkilder =
        [
            new() { Id = Guid.NewGuid(), Code = "K_NPR.SOM", Name = "Somatikk", NameEn = english ? "Somatics" : null,
                    Description = "Somatiske opphold.", DescriptionEn = english ? "Somatic stays." : null },
        ],
        Datasamlinger =
        [
            new() { Id = Guid.NewGuid(), Name = "Opphold", NameEn = english ? "Stays" : null,
                    Description = "Alle opphold.", DescriptionEn = english ? "Every stay." : null },
            new() { Id = Guid.NewGuid(), Name = "Utskrivninger", NameEn = "Discharges" },
        ],
    };

    private IRenderedComponent<KildeView> RenderKilde(KildeDetail kilde, string language)
    {
        Services.AddSingleton<IMuninExplorerClient>(new HierarchyClient(english: false));

        return Render<KildeView>(b => b.Add(c => c.Kilde, kilde).Add(c => c.Language, language));
    }

    [Fact]
    public void KildeView_WhenTheReaderReadsEnglishAndMuninHasIt_ThenEveryNameAndDescriptionIsEnglishAndUnmarked()
    {
        var cut = RenderKilde(Kilde(english: true), "en");

        Assert.Equal(EnglishName, cut.Find("h2").TextContent.Trim());
        Assert.Equal("en", LangOf(cut.Find("h2")));

        foreach (var text in new[] { EnglishDescription, "Somatics", "Somatic stays.", "Stays", "Every stay." })
        {
            Assert.Equal("en", LangOf(Holding(cut, text)));
        }

        Assert.False(Shows(cut, NorwegianName));
        Assert.False(Shows(cut, "Somatikk"));
    }

    [Fact]
    public void KildeView_WhenTheReaderReadsEnglishAndMuninHasNone_ThenTheNorwegianIsShownMarkedNo()
    {
        var cut = RenderKilde(Kilde(english: false), "en");

        Assert.Equal(NorwegianName, cut.Find("h2").TextContent.Trim());

        foreach (var text in new[] { NorwegianName, NorwegianDescription, "Somatikk", "Somatiske opphold.", "Opphold", "Alle opphold." })
        {
            Assert.Equal("no", LangOf(Holding(cut, text)));
        }

        Assert.Equal("en", LangOf(Holding(cut, "Discharges")));
    }

    [Fact]
    public void KildeView_WhenOnlyTheNameIsTranslated_ThenTheNameIsEnglishAndTheDescriptionNorwegianMarkedNo()
    {
        var cut = RenderKilde(Kilde(english: true) with { DescriptionEn = null }, "en");

        Assert.Equal(EnglishName, cut.Find("h2").TextContent.Trim());
        Assert.Equal("en", LangOf(cut.Find("h2")));
        Assert.Equal("no", LangOf(Holding(cut, NorwegianDescription)));
    }

    [Fact]
    public void KildeView_WhenTheReaderReadsNorwegian_ThenTheNorwegianIsShownWhateverEnglishThereIs()
    {
        var cut = RenderKilde(Kilde(english: true), "no");

        Assert.Equal(NorwegianName, cut.Find("h2").TextContent.Trim());
        Assert.True(Shows(cut, NorwegianDescription));
        Assert.True(Shows(cut, "Somatikk"));
        Assert.False(Shows(cut, EnglishName));
        Assert.False(Shows(cut, "Stays"));
        Assert.False(Shows(cut, "Discharges"));
        Assert.Equal("Utskrivninger", Holding(cut, "Utskrivninger").TextContent.Trim());
    }

    [Fact]
    public void KildeView_WhenTheNameIsEnglish_ThenTheCodeLineStillStandsUnderIt()
    {
        // The code is left out only where the heading fell back to it; an English name is still a name.
        var cut = RenderKilde(Kilde(english: true), "en");

        Assert.Contains("K_NPR", cut.Find(".munin-explorer-kilde__identifiers").TextContent);
    }

    // ---- DatasamlingView ----------------------------------------------------------------------

    private static DatasamlingDetail Datasamling(bool english) => new()
    {
        Id = Guid.NewGuid(),
        Code = "D_NPR.OPPHOLD",
        PreferredTerm = "Opphold",
        PreferredTermEn = english ? "Stays" : null,
        Description = NorwegianDescription,
        DescriptionEn = english ? EnglishDescription : null,
        ParentKildeId = Guid.NewGuid(),
        ParentKildeName = NorwegianName,
        ParentKildeNameEn = EnglishName,
    };

    private IRenderedComponent<DatasamlingView> RenderDatasamling(DatasamlingDetail datasamling, string language) =>
        Render<DatasamlingView>(b => b.Add(c => c.Datasamling, datasamling).Add(c => c.Language, language));

    [Fact]
    public void DatasamlingView_WhenTheReaderReadsEnglishAndMuninHasIt_ThenNameDescriptionAndKildeAreEnglish()
    {
        var cut = RenderDatasamling(Datasamling(english: true), "en");

        Assert.Equal("Stays", cut.Find("h2").TextContent.Trim());
        Assert.Equal("en", LangOf(cut.Find("h2")));
        Assert.Equal("en", LangOf(Holding(cut, EnglishDescription)));
        Assert.Equal("en", LangOf(Holding(cut, EnglishName)));
        Assert.False(Shows(cut, NorwegianName));
    }

    [Fact]
    public void DatasamlingView_WhenTheReaderReadsEnglishAndMuninHasNone_ThenTheNorwegianIsShownMarkedNo()
    {
        var cut = RenderDatasamling(Datasamling(english: false), "en");

        Assert.Equal("Opphold", cut.Find("h2").TextContent.Trim());

        foreach (var text in new[] { "Opphold", NorwegianDescription })
        {
            Assert.Equal("no", LangOf(Holding(cut, text)));
        }

        Assert.Equal("en", LangOf(Holding(cut, EnglishName)));
        Assert.False(Shows(cut, NorwegianName));
    }

    [Fact]
    public void DatasamlingView_WhenTheReaderReadsNorwegian_ThenTheNorwegianIsShown()
    {
        var cut = RenderDatasamling(Datasamling(english: true), "no");

        Assert.Equal("Opphold", cut.Find("h2").TextContent.Trim());
        Assert.True(Shows(cut, NorwegianDescription));
        Assert.True(Shows(cut, NorwegianName));
        Assert.False(Shows(cut, EnglishName));
    }

    // ---- VariableView -------------------------------------------------------------------------

    private static VariableDetail Variable(bool english) => new()
    {
        Id = Guid.NewGuid(),
        Code = "V_NPR.ALDER",
        PreferredTerm = "Alder ved innleggelse",
        PreferredTermEn = english ? "Age at admission" : null,
        Description = NorwegianDescription,
        DescriptionEn = english ? EnglishDescription : null,
        KildeName = NorwegianName,
        KildeNameEn = english ? EnglishName : null,
        AllDatasamlinger = [new() { Id = Guid.NewGuid(), Name = "Opphold", NameEn = english ? "Stays" : null }],
        AllVariabelgrupper = [new() { Id = Guid.NewGuid(), Name = "Pasienten", NameEn = "The patient" }],
    };

    private IRenderedComponent<VariableView> RenderVariable(VariableDetail variable, string language) =>
        Render<VariableView>(b => b.Add(c => c.Variable, variable).Add(c => c.Language, language));

    [Fact]
    public void VariableView_WhenTheReaderReadsEnglishAndMuninHasIt_ThenEveryCatalogueNameIsEnglish()
    {
        var cut = RenderVariable(Variable(english: true), "en");

        Assert.Equal("Age at admission", cut.Find("h2").TextContent.Trim());

        foreach (var text in new[] { "Age at admission", EnglishDescription, EnglishName, "Stays", "The patient" })
        {
            Assert.Equal("en", LangOf(Holding(cut, text)));
        }

        Assert.False(Shows(cut, "Alder ved innleggelse"));
    }

    [Fact]
    public void VariableView_WhenTheReaderReadsEnglishAndMuninHasNone_ThenTheNorwegianIsShownMarkedNo()
    {
        var cut = RenderVariable(Variable(english: false), "en");

        foreach (var text in new[] { "Alder ved innleggelse", NorwegianDescription, NorwegianName, "Opphold" })
        {
            Assert.Equal("no", LangOf(Holding(cut, text)));
        }

        Assert.Equal("en", LangOf(Holding(cut, "The patient")));
    }

    [Fact]
    public void VariableView_WhenTheReaderReadsNorwegian_ThenTheNorwegianIsShown()
    {
        var cut = RenderVariable(Variable(english: true), "no");

        Assert.Equal("Alder ved innleggelse", cut.Find("h2").TextContent.Trim());
        Assert.True(Shows(cut, "Pasienten"));
        Assert.False(Shows(cut, "Age at admission"));
        Assert.False(Shows(cut, "The patient"));
    }

    // ---- KildeTrailBlock ----------------------------------------------------------------------

    [Theory]
    [InlineData("en", true, new[] { EnglishName, "Stays" }, false)]
    [InlineData("no", true, new[] { NorwegianName, "Opphold" }, true)]
    public void KildeTrail_WhenAVariableIsPlaced_ThenItsStepsFollowThePickRule(
        string language, bool english, string[] expected, bool norwegian)
    {
        var steps = KildeTrailBlock.Steps(Variable(english), Texts.For(language), null);

        Assert.Equal(expected, steps.Select(step => step.Text));
        Assert.All(steps, step => Assert.Equal(norwegian, step.Norwegian));
    }

    [Fact]
    public void KildeTrail_WhenOnlyTheKildeIsTranslated_ThenThatStepIsEnglishAndTheDatasamlingNorwegian()
    {
        var variable = Variable(english: false) with { KildeNameEn = EnglishName };

        var steps = KildeTrailBlock.Steps(variable, Texts.For("en"), null);

        Assert.Equal([(EnglishName, false), ("Opphold", true)], steps.Select(step => (step.Text, step.Norwegian)));
    }

    // ---- KildeHierarchyView -------------------------------------------------------------------

    private sealed class HierarchyClient(bool english) : EmptyMuninExplorerClient
    {
        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new()
            {
                KildeId = id,
                DirectDatasamlinger =
                [
                    new()
                    {
                        Id = Guid.NewGuid(), Name = "Opphold", NameEn = "Stays", VariableCount = 2,
                        Variabelgrupper = [new() { Id = Guid.NewGuid(), Name = "Pasienten", NameEn = english ? "The patient" : null, VariableCount = 2 }],
                    },
                ],
            });
    }

    private IRenderedComponent<KildeHierarchyView> RenderHierarchy(bool english, string language)
    {
        Services.AddSingleton<IMuninExplorerClient>(new HierarchyClient(english));

        return Render<KildeHierarchyView>(b => b.Add(c => c.KildeId, Guid.NewGuid()).Add(c => c.Language, language));
    }

    [Theory]
    [InlineData("en", true, "Stays", null, "The patient", null)]
    [InlineData("en", false, "Stays", null, "Pasienten", "no")]
    [InlineData("no", true, "Opphold", null, "Pasienten", null)]
    public void Hierarchy_WhenANodeIsNamed_ThenItsLabelFollowsThePickRule(
        string language, bool english, string datasamling, string? datasamlingLang, string gruppe, string? gruppeLang)
    {
        var cut = RenderHierarchy(english, language);

        cut.WaitForAssertion(() => Assert.True(Shows(cut, gruppe)));
        Assert.Equal(datasamlingLang, Holding(cut, datasamling, "span").GetAttribute("lang"));
        Assert.Equal(gruppeLang, Holding(cut, gruppe, "span").GetAttribute("lang"));
        // What a screen reader uses: the mark, or the view's own root, never the host page's lang (WCAG 3.1.2).
        Assert.Equal(datasamlingLang ?? language, LangOf(Holding(cut, datasamling, "span")));
        Assert.Equal(gruppeLang ?? language, LangOf(Holding(cut, gruppe, "span")));
    }

    // ---- VariableSearch: rows, facets and the hierarchy trail ---------------------------------

    private static readonly Guid KildeId = Guid.NewGuid();

    private sealed class SearchClient(bool english) : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items =
                [
                    new()
                    {
                        Id = Guid.NewGuid(), Code = "V_NPR.ALDER",
                        PreferredTerm = "Alder ved innleggelse", PreferredTermEn = "Age at admission",
                        KildeId = KildeId, KildeName = NorwegianName, KildeNameEn = english ? EnglishName : null,
                        DatasamlingName = "Opphold", DatasamlingNameEn = english ? "Stays" : null,
                        VariabelgruppeName = "Pasienten", VariabelgruppeNameEn = english ? "The patient" : null,
                    },
                ],
                TotalCount = 1,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        // As Munin answers: name in the Accept-Language where it has one, nameNo always Norwegian.
        public override Task<FilterOptions> GetFiltersAsync(
            string? search = null, VariableFilter? filter = null, string? language = null,
            CancellationToken cancellationToken = default)
        {
            var asked = ReaderLanguage.IsEnglish(language);

            return Task.FromResult(new FilterOptions
            {
                Kilder =
                [
                    new() { Id = KildeId, Name = asked && english ? "Source facet" : "Kilde-fasett", NameNo = "Kilde-fasett", Count = 1 },
                    new() { Id = Guid.NewGuid(), Name = asked ? "Other source" : "Annen kilde", NameNo = "Annen kilde", Count = 1 },
                ],
                TotalCount = 2,
            });
        }
    }

    private IRenderedComponent<VariableSearch> RenderSearch(bool english, string language, VariableFilter? filter = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new SearchClient(english));

        return Render<VariableSearch>(b =>
        {
            b.Add(c => c.Language, language);

            if (filter is not null)
            {
                b.Add(c => c.Filter, filter);
            }
        });
    }

    [Theory]
    // The explorer marks its own root with the page language, so English inherits "en" from it.
    [InlineData("en", true, "Age at admission", "en", new[] { "Stays", "The patient" }, "en")]
    [InlineData("en", false, "Age at admission", "en", new[] { "Opphold", "Pasienten" }, "no")]
    [InlineData("no", true, "Alder ved innleggelse", "no", new[] { "Opphold", "Pasienten" }, "no")]
    public void SearchRow_WhenDrawn_ThenItsNameAndColumnsFollowThePickRule(
        string language, bool english, string name, string nameLang, string[] columns, string columnLang)
    {
        var cut = RenderSearch(english, language);

        cut.WaitForAssertion(() => Assert.True(Shows(cut, name)));
        Assert.Equal(nameLang, LangOf(Holding(cut, name, "ul.munin-explorer-data-list span")));

        foreach (var text in columns)
        {
            Assert.Equal(columnLang, LangOf(Holding(cut, text, "ul.munin-explorer-data-list span")));
        }
    }

    [Fact]
    public void SearchRow_WhenEnglishAndTheKildeHasAShortName_ThenTheFullEnglishNameIsItsTooltip()
    {
        var cut = RenderSearch(english: true, "en");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".munin-explorer-dataitem-main__source")));

        Assert.Equal(EnglishName, cut.Find(".munin-explorer-dataitem-main__source").GetAttribute("title"));
    }

    [Theory]
    [InlineData("en", true, "Source facet", null, "Other source")]
    [InlineData("en", false, "Kilde-fasett", "no", "Other source")]
    [InlineData("no", true, "Kilde-fasett", null, "Annen kilde")]
    public void KildeFacet_WhenDrawn_ThenItsLabelFollowsThePickRule(
        string language, bool english, string label, string? lang, string witness)
    {
        var cut = RenderSearch(english, language);

        cut.WaitForAssertion(() => Assert.True(Shows(cut, label)));

        Assert.Equal(lang, Holding(cut, label, "span").GetAttribute("lang"));
        Assert.Null(Holding(cut, witness, "span").GetAttribute("lang"));
    }

    [Theory]
    [InlineData("en", true, "Source facet", null)]
    [InlineData("en", false, "Kilde-fasett", "no")]
    [InlineData("no", true, "Kilde-fasett", "no")]
    public void HierarchyTrail_WhenAKildeIsChosen_ThenItsStepFollowsThePickRule(
        string language, bool english, string label, string? lang)
    {
        var cut = RenderSearch(english, language, new VariableFilter { KildeIds = [KildeId] });

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".munin-explorer-breadcrumb button")));

        var step = cut.Find(".munin-explorer-breadcrumb button");
        Assert.Equal(label, step.TextContent.Trim());
        Assert.Equal(lang, step.QuerySelector("span[lang]")?.GetAttribute("lang"));
    }

    // ---- KildeSearch --------------------------------------------------------------------------

    private sealed class KilderClient(bool english) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
            [
                new() { Id = KildeId, Code = "K_NPR", Name = NorwegianName, NameEn = english ? EnglishName : null, IsActive = true },
                new() { Id = Guid.NewGuid(), Code = "K_MFR", Name = "Medisinsk fødselsregister", NameEn = "Medical Birth Registry", IsActive = true },
            ]);
    }

    [Theory]
    [InlineData("en", true, EnglishName, null, "Medical Birth Registry")]
    [InlineData("en", false, NorwegianName, "no", "Medical Birth Registry")]
    [InlineData("no", true, NorwegianName, null, "Medisinsk fødselsregister")]
    public void KildeRow_WhenDrawn_ThenItsNameFollowsThePickRule(
        string language, bool english, string name, string? lang, string witness)
    {
        Services.AddSingleton<IMuninExplorerClient>(new KilderClient(english));

        var cut = Render<KildeSearch>(b => b.Add(c => c.Language, language));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button.munin-explorer-kilder__name")));

        var button = Holding(cut, name, "button.munin-explorer-kilder__name");
        Assert.Equal(lang, button.GetAttribute("lang"));
        Assert.Null(Holding(cut, witness, "button.munin-explorer-kilder__name").GetAttribute("lang"));
    }
}
