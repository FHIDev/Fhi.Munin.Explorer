using System.Text.Json;
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
    public void Render_Always_ThenEveryClassNameIsOneSomeStylesheetActuallyDefines()
    {
        // This view is where `headline-sm` lived: a typo for `headline-s` on nine block headings,
        // undefined in all seven of helsedata's bundles and in Stiler, so every one of them rendered
        // at the browser's own <h*> size on helsedata.no. It survived because the class name reaches
        // the DOM as an argument to @Heading rather than as a class attribute, which put it out of
        // reach of the CSS check in scripts/, and because that check only looked at names in the
        // munin-explorer prefix anyway — the ones we invent, never the ones we borrow.
        var cut = Render(Detail());

        // Compared against an empty list rather than asserted empty, so a failure names the classes
        // instead of saying only that there were some.
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

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
        Assert.DoesNotContain(cut.FindAll(".munin-explorer-group").Select(e => e.TextContent),
                              text => text == "Datatype");
        Assert.Single(cut.FindAll(".munin-explorer-group"));
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
                     cut.FindAll("table.munin-explorer-statistics thead th").Select(t => t.TextContent));

        Assert.Equal(["2022", "1", "9", "4", "2"],
                     cut.FindAll("table.munin-explorer-statistics tbody tr:first-child > *")
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

        var cells = Render(detail).FindAll("table.munin-explorer-statistics tbody td");

        Assert.All(cells, c => Assert.Equal("—", c.TextContent));
    }

    [Fact]
    public void Statistics_WhenTheirPropertiesArriveAsNull_ThenTheRowDrawsTheSameDashAnEmptyBagDoes()
    {
        // Deserialised rather than constructed, because the constructed shape proves nothing here:
        // Statistic.AdditionalProperties is declared non-nullable with an initialiser, and that
        // initialiser only survives a key ABSENT from the payload. System.Text.Json writes null
        // straight over it for an explicit "additionalProperties": null, so the null this is about
        // can only arrive through the deserialiser. Setting the property to null in C# would exercise
        // a state the type says cannot happen and say nothing about the one that does.
        //
        // The statistikker array has to be there and non-empty as well, or the table returns before
        // reading a bag at all and the test passes with the fault untouched — this went out once
        // already, guarded in CatalogueProperties for the three call sites that go through it while
        // this one read the bag straight off the contract one file away.
        var detail = JsonSerializer.Deserialize<VariableDetail>(
            """
            {
              "id": "6f1d4a5c-0000-4000-8000-000000000002",
              "code": "ALSFRSR1Tale",
              "preferredTerm": "1. Tale",
              "datasamlingStatistikkType": "yearly",
              "statistikker": [
                {
                  "id": "6f1d4a5c-0000-4000-8000-000000000003",
                  "code": "ALSFRSR1Tale",
                  "preferredTerm": "1. Tale",
                  "additionalProperties": null
                }
              ]
            }
            """)!;

        Assert.Null(detail.Statistics[0].AdditionalProperties);

        var cut = Render(detail);

        // The row is there, and every cell in it says what a missing number says — the year included,
        // which heads its own row rather than sitting in a td.
        Assert.Equal("—", cut.Find("table.munin-explorer-statistics tbody tr th").TextContent);
        Assert.All(cut.FindAll("table.munin-explorer-statistics tbody td"),
                   c => Assert.Equal("—", c.TextContent));
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
        [.. cut.FindAll(".munin-explorer-versions > li > button")
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

        Assert.Equal("—", cut.Find(".munin-explorer-versions__from").TextContent);
        Assert.Equal("Pågående", cut.Find(".munin-explorer-versions__to").TextContent);
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
        var toggles = cut.FindAll(".munin-explorer-versions > li > button");

        toggles[0].Click();
        cut.FindAll(".munin-explorer-versions > li > button")[1].Click();

        Assert.All(cut.FindAll(".munin-explorer-versions > li > button"),
                   b => Assert.Equal("true", b.GetAttribute("aria-expanded")));

        Assert.All(cut.FindAll(".munin-explorer-versions__detail"),
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
        cut.Find(".munin-explorer-versions > li > button").Click();

        var panel = cut.Find(".munin-explorer-versions__detail").TextContent;

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

        Assert.Equal("no", named.Find(".munin-explorer-versions__name").GetAttribute("lang"));

        var fallback = unnamed.Find(".munin-explorer-versions__name");

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

        var first = Render(detail).Find(".munin-explorer-versions__detail").GetAttribute("id");
        var second = Render(detail).Find(".munin-explorer-versions__detail").GetAttribute("id");

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Versions_WhenAToggleNamesAPanel_ThenThatPanelIsInTheSameView()
    {
        // The other half of the id rule: unique is not enough, it also has to still match.
        var detail = Detail() with { Versions = [Version(Guid.NewGuid())] };
        var cut = Render(detail);

        var controls = cut.Find(".munin-explorer-versions__toggle").GetAttribute("aria-controls");

        Assert.Equal(cut.Find(".munin-explorer-versions__detail").GetAttribute("id"), controls);
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
