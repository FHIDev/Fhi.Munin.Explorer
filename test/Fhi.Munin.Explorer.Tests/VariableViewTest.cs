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

    private static VariableVersion Version(
        Guid id, string name = "Basaldose", string status = "Active",
        DateTimeOffset? from = null, DateTimeOffset? to = null, string description = "Avlest basaldose") =>
        new()
        {
            VersionId = id,
            PreferredTerm = name,
            Description = description,
            Status = status,
            ValidFrom = from,
            ValidTo = to,
        };

    private static string[] VersionRows(IRenderedComponent<VariableView> cut) =>
        [.. cut.FindAll(".variable-explorer-versions > li > button")
               .Select(b => b.TextContent.Replace("\n", " ").Trim())];

    [Fact]
    public void Versions_WhenOneIsTheVersionOnScreen_ThenItIsCurrentByIdRatherThanByPosition()
    {
        // The one that matters. Every version of every variable sampled on the test API comes back
        // Active - including four superseded ones on a single variable - so a badge read off the
        // status would call them all active and none of them current. It has to be an identity.
        //
        // The current version is deliberately NOT first here: taking position would pass on the
        // real payload, where it happens to be, and be wrong the first time it is not.
        var older = Guid.NewGuid();
        var current = Guid.NewGuid();

        var detail = Detail() with
        {
            VersionId = current,
            Versions = [Version(older), Version(current)],
        };

        var rows = VersionRows(Render(detail));

        Assert.Contains("Aktiv", rows[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Gjeldende", rows[0], StringComparison.Ordinal);
        Assert.Contains("Gjeldende", rows[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Versions_WhenOneHasNoName_ThenItSaysSoRatherThanRenderingBlank()
    {
        // Three of five versions on a real variable have no preferred term. A blank row reads as
        // something failing to draw; this says what is true — there is a version here, unnamed.
        var detail = Detail() with { Versions = [Version(Guid.NewGuid(), name: "")] };

        Assert.Contains("Versjon uten navn", VersionRows(Render(detail))[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Versions_WhenOneHasNoStartDate_ThenTheCellSaysSoAndDoesNotCollideWithTheEnd()
    {
        // Rendered as two cells, not one string. Joined, a version with no start reads
        // "— – Pågående" — a dash immediately followed by a dash, which is a puzzle, not a date.
        var detail = Detail() with { Versions = [Version(Guid.NewGuid(), from: null)] };
        var cut = Render(detail);

        Assert.Equal("—", cut.Find(".variable-explorer-versions__from").TextContent);
        Assert.Equal("Pågående", cut.Find(".variable-explorer-versions__to").TextContent);
    }

    [Fact]
    public void Versions_WhenAStatusIsOneWeHaveNeverSeen_ThenItIsShownRatherThanHidden()
    {
        // Only Active has ever come back. Historical is in the vocabulary and handled; anything
        // else is shown as it arrived rather than silently dropped or guessed at.
        var detail = Detail() with { Versions = [Version(Guid.NewGuid(), status: "Superseded")] };

        Assert.Contains("Superseded", VersionRows(Render(detail))[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Versions_WhenTwoAreOpened_ThenBothStayOpen()
    {
        // Disclosures, not tabs. Comparing two versions is the reason to open the history at all,
        // so closing one to read the next would remove the one thing it is for.
        var detail = Detail() with
        {
            Versions = [Version(Guid.NewGuid()), Version(Guid.NewGuid(), name: "Basaldose eldre")],
        };

        var cut = Render(detail);
        var toggles = cut.FindAll(".variable-explorer-versions > li > button");

        toggles[0].Click();
        cut.FindAll(".variable-explorer-versions > li > button")[1].Click();

        Assert.All(cut.FindAll(".variable-explorer-versions > li > button"),
                   b => Assert.Equal("true", b.GetAttribute("aria-expanded")));

        Assert.All(cut.FindAll(".variable-explorer-versions__detail"),
                   d => Assert.False(d.HasAttribute("hidden")));
    }

    [Fact]
    public void Versions_WhenOneIsOpened_ThenItShowsWhatRunaShows()
    {
        var detail = Detail() with
        {
            Versions =
            [
                Version(Guid.NewGuid(), from: new DateTimeOffset(2021, 8, 24, 0, 0, 0, TimeSpan.Zero)),
            ],
        };

        var cut = Render(detail);
        cut.Find(".variable-explorer-versions > li > button").Click();

        var panel = cut.Find(".variable-explorer-versions__detail").TextContent;

        Assert.Contains("Avlest basaldose", panel, StringComparison.Ordinal);
        Assert.Contains("Gyldig fra", panel, StringComparison.Ordinal);
        Assert.Contains("Gyldig til", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void Versions_WhenThereAreNone_ThenNoHeadingPromisesAHistory()
    {
        Assert.DoesNotContain("Versjonshistorikk", Render(Detail()).Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Versions_WhenAnEnglishReaderMeetsAnUnnamedOne_ThenTheFallbackIsNotMarkedNorwegian()
    {
        // Two texts in one span, and only one of them is the catalogue's. A preferred term is
        // Norwegian whoever reads it; "Version without a name" is this package's own English, and
        // lang="no" on it tells a screen reader to pronounce English by Norwegian rules — the
        // attribute's only job is to pick the voice, so a wrong one is worse than none.
        var named = Render(Detail() with { Versions = [Version(Guid.NewGuid(), name: "Basaldose")] }, "en");
        var unnamed = Render(Detail() with { Versions = [Version(Guid.NewGuid(), name: "")] }, "en");

        Assert.Equal("no", named.Find(".variable-explorer-versions__name").GetAttribute("lang"));

        var fallback = unnamed.Find(".variable-explorer-versions__name");

        Assert.Equal("Version without a name", fallback.TextContent);
        Assert.False(fallback.HasAttribute("lang"));
    }

    [Fact]
    public void Versions_WhenTwoViewsShowTheSameVersion_ThenTheirPanelIdsDiffer()
    {
        // A version id is unique in the catalogue but not on a page. A host is free to mount two of
        // these — a variable beside the one it replaced is the obvious case — and an id derived
        // from the version alone would then appear twice in one document: a duplicate id, and an
        // aria-controls on the second view's toggle resolving to the first view's panel.
        var detail = Detail() with { Versions = [Version(Guid.NewGuid())] };

        var first = Render(detail).Find(".variable-explorer-versions__detail").GetAttribute("id");
        var second = Render(detail).Find(".variable-explorer-versions__detail").GetAttribute("id");

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Versions_WhenAToggleNamesAPanel_ThenThatPanelIsInTheSameView()
    {
        // The other half of the id rule: unique is not enough, it also has to still match.
        var detail = Detail() with { Versions = [Version(Guid.NewGuid())] };
        var cut = Render(detail);

        var controls = cut.Find(".variable-explorer-versions__toggle").GetAttribute("aria-controls");

        Assert.Equal(cut.Find(".variable-explorer-versions__detail").GetAttribute("id"), controls);
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
