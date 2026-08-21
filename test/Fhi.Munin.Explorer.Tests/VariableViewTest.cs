using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The whole variable: the view that opens in place of the list, because this package has no router
/// and a detail page is therefore a view rather than a route.
/// </summary>
public class VariableViewTest : BunitContext
{
    private static PropertyMetadataEntry Entry(string key, int sortOrder, string group, string? optionsJson = null) =>
        new()
        {
            Key = key,
            SortOrder = sortOrder,
            GroupTranslations = new Dictionary<string, string> { ["no"] = group },
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = key },
            OptionsJson = optionsJson,
        };

    private static VariableDetail Detail() => new()
    {
        Id = Guid.NewGuid(),
        Code = "ALSFRSR1Tale",
        PreferredTerm = "1. Tale",
        Description = "Skalaen maaler taleevne.",
        KildeName = "Als registeret",
        KildeShortName = "ALS",
        KildeType = "Nasjonalt medisinsk kvalitetsregister",
        DataType = "2",
        PropertyMetadata =
        [
            Entry("Kommentar", 50, "Beskrivelse"),
            // The vocabulary's Norwegian label for this field is an English word, which is what
            // makes the duplication below visible rather than merely redundant.
            Entry("DataType", 20, "Datatype", """[{"value":"2","label":"Integer","labelEn":"Integer"}]"""),
        ],
        AdditionalProperties = new Dictionary<string, string?>
        {
            ["Kommentar"] = "Gyldig fra 2019.",
            ["DataType"] = "2",
        },
    };

    private IRenderedComponent<VariableView> Render(VariableDetail detail, string? language = null) =>
        Render<VariableView>(b => b
            .Add(c => c.Variable, detail)
            .Add(c => c.Language, language));

    [Fact]
    public void Metadata_WhenAKeyIsAlreadyInTheSidebar_ThenItIsNotRepeatedInTheGroups()
    {
        // The bug this was written for. DataType is drawn in the sidebar from the typed field, and
        // it was ALSO drawn in the metadata under its own group — the same fact, twice, saying two
        // different things: "Heltall" in the sidebar from our own translation, "Integer" in the
        // group from the catalogue's vocabulary, whose Norwegian label for this field is English.
        var cut = Render(Detail());

        Assert.DoesNotContain("Integer", cut.Markup, StringComparison.Ordinal);

        // And the group goes with it, because nothing else in it was filled in — which is exactly
        // the five groups Runa draws where the payload offers six.
        //
        // Scoped to the group headings on purpose. The sidebar has a Datatype heading of its own and
        // is meant to: that is where this fact belongs. Asserting no "Datatype" heading anywhere
        // would fail on the very thing the fix keeps.
        Assert.DoesNotContain(cut.FindAll(".variable-explorer-group").Select(e => e.TextContent),
                              text => text == "Datatype");
        Assert.Single(cut.FindAll(".variable-explorer-group"));
    }

    [Fact]
    public void Statistics_WhenTheVariableHasThem_ThenTheyAreTheColumnsRunaShows()
    {
        // Measured on Runa 2026-08-21: year, then the four summary columns. The payload also carries
        // MED and counts of valid and missing cases; Runa draws none of them, so neither does this.
        var detail = Detail() with
        {
            DatasamlingStatisticsType = "yearly",
            Statistics =
            [
                new()
                {
                    AdditionalProperties = new Dictionary<string, string?>
                    {
                        ["SisteOppdaterteAarssett"] = "2022",
                        ["MIN"] = "1",
                        ["MAX"] = "9",
                        ["AVG"] = "4",
                        ["STD"] = "2",
                        ["MED"] = "3",
                        ["GyldigeTilfeller"] = "1475",
                    },
                },
            ],
        };

        var cut = Render(detail);

        Assert.Equal(["År", "Minimum", "Maksimum", "Gjennomsnitt", "Standardavvik"],
                     cut.FindAll("table.variable-explorer-statistics thead th").Select(t => t.TextContent));

        Assert.Equal(["2022", "1", "9", "4", "2"],
                     cut.FindAll("table.variable-explorer-statistics tbody tr:first-child > *")
                        .Select(c => c.TextContent));

        // The median and the case counts are in the payload and stay out of the table.
        Assert.DoesNotContain("1475", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Statistics_WhenTheKindIsKnown_ThenTheHeadingSaysWhichKind()
    {
        // "Statistikk (Årsbasert)". The kind changes what the numbers mean, so it belongs in the
        // heading rather than being left for the reader to assume.
        var detail = Detail() with
        {
            DatasamlingStatisticsType = "yearly",
            Statistics = [new() { AdditionalProperties = new Dictionary<string, string?> { ["SisteOppdaterteAarssett"] = "2022" } }],
        };

        Assert.Contains("Statistikk (Årsbasert)", Render(detail).Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Statistics_WhenTheKindIsOneWeHaveNeverSeen_ThenItIsShownRatherThanHidden()
    {
        // Only 'yearly' has ever come back from the test API. An unknown kind is shown as it
        // arrived: a heading reading "Statistikk (quarterly)" is ugly and honest, where a bare
        // "Statistikk" would quietly tell a reader these numbers mean something they may not.
        var detail = Detail() with
        {
            DatasamlingStatisticsType = "quarterly",
            Statistics = [new() { AdditionalProperties = new Dictionary<string, string?> { ["SisteOppdaterteAarssett"] = "2022" } }],
        };

        Assert.Contains("Statistikk (quarterly)", Render(detail).Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Statistics_WhenANumberIsMissing_ThenTheCellSaysSoRatherThanSittingEmpty()
    {
        // A blank cell reads as a rendering fault. A dash says the catalogue holds no number.
        var detail = Detail() with
        {
            DatasamlingStatisticsType = "yearly",
            Statistics = [new() { AdditionalProperties = new Dictionary<string, string?> { ["SisteOppdaterteAarssett"] = "2022" } }],
        };

        var cells = Render(detail).FindAll("table.variable-explorer-statistics tbody td");

        Assert.All(cells, c => Assert.Equal("—", c.TextContent));
    }

    [Fact]
    public void Statistics_WhenThereAreNone_ThenNoHeadingPromisesAny()
    {
        Assert.DoesNotContain("Statistikk", Render(Detail()).Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Heading_WhenTheReaderIsEnglish_ThenTheCataloguesOwnNameIsMarkedNorwegian()
    {
        var cut = Render(Detail(), "en");
        var heading = cut.Find(".headline-s");

        Assert.Equal("1. Tale", heading.TextContent);
        Assert.Equal("no", heading.GetAttribute("lang"));
    }
}
