using System.Text.Json;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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

    private IRenderedComponent<VariableView> Render(
        VariableDetail detail,
        string? language = null,
        IReadOnlyList<DetailTrailStep>? trail = null) =>
        Render<VariableView>(b => b
            .Add(c => c.Variable, detail)
            .Add(c => c.Language, language)
            .Add(c => c.Trail, trail));

    [Fact]
    public void Eyebrow_Always_ThenItNamesTheKindOfPageAndIsNotAHeading()
    {
        // The eyebrow is above the title and is a <p>: rendered as an <h*> it would be a second
        // title in the outline, naming a category rather than the thing on screen. Both languages,
        // because a word that only translates in one of them reads as the component's own noise.
        var eyebrow = Render(Detail()).Find(".munin-explorer-page__eyebrow");

        Assert.Equal("P", eyebrow.TagName);
        Assert.Equal("Variabel", eyebrow.TextContent.Trim());
        Assert.Equal("Variable", Render(Detail(), language: "en").Find(".munin-explorer-page__eyebrow").TextContent.Trim());
    }

    [Fact]
    public void Trail_WhenACallerSuppliesTheStepsAbove_ThenThisPageIsAppendedAsTheCurrentStep()
    {
        // The view appends its own name rather than the caller repeating it, so the last step is
        // the page by construction — and the one step a reader can never be sent to a dead link by.
        var cut = Render(Detail(), trail: [new DetailTrailStep("Kildeutforsker", "/kilder")]);

        var steps = cut.FindAll("nav.breadcrumbs li");

        Assert.Equal("Kildeutforsker", steps[0].TextContent.Trim());
        Assert.Equal("/kilder", Assert.Single(steps[0].QuerySelectorAll("a")).GetAttribute("href"));

        var last = steps[^1];

        Assert.Equal("page", last.GetAttribute("aria-current"));
        Assert.Empty(last.QuerySelectorAll("a"));
        Assert.Equal(
            cut.Find(".munin-explorer-whole__header .headline-s").TextContent.Trim(),
            last.TextContent.Trim());
    }

    [Fact]
    public void Trail_WhenNoCallerSuppliesSteps_ThenNoBreadcrumbIsDrawn()
    {
        // The state every mount of this view is in today outside the kildeutforsker: no addresses
        // to offer, so no trail rather than one step that goes nowhere.
        Assert.Empty(Render(Detail()).FindAll("nav.breadcrumbs"));
    }

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
    public void Chassis_WhenTheViewIsDrawn_ThenTheSharedNamesAreWornBesideThisViewsOwn()
    {
        // Both sets on every element but the body: the chassis is added beside this view's own
        // prefix rather than replacing it, so a host rule keyed on either one still draws.
        var cut = Render(Detail());

        Assert.Contains("munin-explorer-whole", cut.Find(".munin-explorer-page").ClassList);

        var body = Assert.Single(cut.FindAll(".munin-explorer-page__body"));

        // The body, and only the body, sheds its older name: an element wearing both would carry a
        // `grid-template-columns` from each block, settled by which stylesheet the host loaded last.
        Assert.Equal("munin-explorer-page__body", body.ClassName);
        Assert.Contains("munin-explorer-whole__main", cut.Find(".munin-explorer-page__main").ClassList);

        // The contents nav fills the column, so the body is two children in two tracks and the nav
        // comes first — ahead in the DOM of the sections it points into, not only beside them.
        Assert.Equal(
            ["munin-explorer-page__toc", "munin-explorer-page__main munin-explorer-whole__main"],
            body.Children.Select(child => child.ClassName));
    }

    [Fact]
    public void Metadata_WhenAKeyIsAlreadyABlockOfItsOwn_ThenItIsNotRepeatedInTheGroups()
    {
        // The bug this was written for. DataType has a block of its own drawn from the typed field,
        // and it was ALSO drawn in the metadata under its own group — the same fact, twice, saying
        // two different things: "Heltall" from our own translation, "Integer" from the catalogue's
        // vocabulary, whose Norwegian label for this field is English.
        var cut = Render(Detail());

        Assert.DoesNotContain("Integer", cut.Markup, StringComparison.Ordinal);

        // And the group goes with it, because nothing else in it was filled in — which is exactly
        // the five groups Runa draws where the payload offers six.
        //
        // Scoped to the group headings on purpose. The typed field has a Datatype heading of its
        // own and is meant to: that is where this fact belongs. Asserting no "Datatype" heading
        // anywhere would fail on the very thing the fix keeps.
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
        // Statistic.AdditionalProperties is declared non-nullable with an initialiser, and the null
        // this is about is one only a deserialiser can produce. Setting the property to null in C#
        // would exercise a state the type says cannot happen and say nothing about the one that does.
        //
        // Plain options, not MuninExplorerClient.Json, and that is the point: the client's options
        // carry NullAsEmptyCollections and never hand this view a null bag. A host substituting its
        // own IMuninExplorerClient reads the API with options of its own — these ones — which is
        // the case this view still guards for. MuninExplorerClientTest covers the other half.
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

    /// <summary>
    /// A pointer press on the first version's disclosure. <paramref name="clicks"/> is the browser's
    /// click count, so 2 is the second click of a double-click gesture and 0 is how a browser
    /// reports Enter or Space on a button, and <paramref name="shift"/> is the modifier held to
    /// extend a selection to where the pointer is.
    /// </summary>
    /// <remarks>The row is found on every call rather than held: each press re-renders it.</remarks>
    private static void PressVersion(
        IRenderedComponent<VariableView> cut, long clicks = 1, bool shift = false) =>
        cut.FindAll(".munin-explorer-versions > li > button")[0]
           .Click(new MouseEventArgs { Detail = clicks, ShiftKey = shift });

    /// <summary>Whether the first version's own panel is showing.</summary>
    private static bool VersionOpen(IRenderedComponent<VariableView> cut) =>
        !cut.FindAll(".munin-explorer-versions__detail")[0].HasAttribute("hidden");

    private static VariableDetail OneVersion() =>
        Detail() with { Versions = [Version(Guid.NewGuid())] };

    [Fact]
    public void Versions_WhenARowIsDoubleClicked_ThenItIsLeftOpen()
    {
        // The standing half of the guard the result row carries — all of it that applies, since
        // this component keeps no press to read RowPress's drag clause against. The second click
        // reached ToggleVersionAsync and shut the version. (Fhi.Metadata-j1j3i)
        var cut = Render(OneVersion());

        PressVersion(cut);
        PressVersion(cut, clicks: 2);

        Assert.True(VersionOpen(cut));
        Assert.Equal("true",
                     cut.FindAll(".munin-explorer-versions__toggle")[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Versions_WhenARowIsShiftClicked_ThenItIsLeftShut()
    {
        // The other gesture that stands still. A version row is dates and a term — text a reader
        // extends a selection across — and the click that ends one lands here.
        var cut = Render(OneVersion());

        PressVersion(cut, shift: true);

        Assert.False(VersionOpen(cut));
        Assert.Equal("false",
                     cut.FindAll(".munin-explorer-versions__toggle")[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Versions_WhenARowIsPressedTwiceAsSeparateGestures_ThenItStillTogglesBothWays()
    {
        // The guard is per gesture, not per control: two deliberate presses each arrive with a click
        // count of one, and a reader who opened a version must still be able to shut it.
        var cut = Render(OneVersion());

        PressVersion(cut);

        Assert.True(VersionOpen(cut));

        PressVersion(cut);

        Assert.False(VersionOpen(cut));
    }

    [Fact]
    public void Versions_WhenARowIsActivatedFromTheKeyboard_ThenEachActivationToggles()
    {
        // Enter and Space on a <button> arrive as a click with a count of zero, which is what keeps a
        // guard on the second click of a pointer gesture from swallowing a second keypress. Verified
        // rather than assumed, because a guard that caught this would make the version unclosable.
        var cut = Render(OneVersion());

        PressVersion(cut, clicks: 0);

        Assert.True(VersionOpen(cut));

        PressVersion(cut, clicks: 0);

        Assert.False(VersionOpen(cut));
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

    /// <summary>A variable whose data period is closed, so both dates in it are written out.</summary>
    private static VariableDetail WithDataPeriod() => Detail() with
    {
        DataFrom = new DateTimeOffset(2022, 9, 20, 0, 0, 0, TimeSpan.Zero),
        DataTo = new DateTimeOffset(2022, 11, 9, 0, 0, 0, TimeSpan.Zero),
    };

    /// <summary>The data period, which is the only thing in its own section.</summary>
    private static string DataPeriod(IRenderedComponent<VariableView> cut) =>
        cut.Find($"#{DetailSectionIds.DataPeriod} p.margin--none").TextContent;

    [Fact]
    public void Dates_Always_ThenTheMonthIsAbbreviated()
    {
        // The short form the 320px rail this block used to sit in needed, kept now the block reads
        // full width. Untested until the date helpers became shared, which is exactly when a
        // spelled-out month could arrive here without anything failing.
        Assert.Equal("20. sep. 2022 – 9. nov. 2022", DataPeriod(Render(WithDataPeriod())));
    }

    [Fact]
    public void Dates_WhenTheReaderIsEnglish_ThenTheAbbreviatedMonthCarriesNoNorwegianOrdinalDot()
    {
        // This view wrote "20. Sep 2022" to an English reader: the dot is what makes the number an
        // ordinal in Norwegian, and English uses none. The kilde view's own dates have followed the
        // reader all along — this one never did, because nothing here read them.
        Assert.Equal("20 Sep 2022 – 9 Nov 2022", DataPeriod(Render(WithDataPeriod(), "en")));
    }

    [Fact]
    public void Heading_WhenTheReaderIsEnglish_ThenTheCataloguesOwnNameIsMarkedNorwegian()
    {
        var cut = Render(Detail(), "en");
        var heading = cut.Find(".headline-s");

        Assert.Equal("1. Tale", heading.TextContent);
        Assert.Equal("no", heading.GetAttribute("lang"));
    }

    [Fact]
    public void FactLists_Always_ThenTheyWearTheGridInTheOneColumn()
    {
        // The markup half — the stylesheet half is asserted in KildeViewTest. Every block of this
        // view reads in one column now, so nothing here may end up back in a rail of its own.
        var cut = Render(Detail());

        Assert.Empty(cut.FindAll("aside"));
        Assert.NotEmpty(cut.Find(".munin-explorer-whole__main").QuerySelectorAll("dl.munin-explorer-page__fields"));
    }

    [Fact]
    public void SourceInformation_WhenTheOwningKildeHasNoKildetype_ThenTheRowSaysSoRatherThanDroppingOut()
    {
        // DetailBlocks.Facts drops a blank value entirely, so a reading site letting the API's null
        // through raw loses the row rather than drawing an empty one — and no compiler says so,
        // because the label helper has always taken a null. (Fhi.Metadata-l9l2n.61)
        var facts = Render(Detail() with { KildeType = null })
            .Find($"#{DetailSectionIds.Source} dl.munin-explorer-page__fields");

        Assert.Equal(
            ["Kildenavn", "Kortnavn", "Type datakilde"],
            facts.QuerySelectorAll("dt").Select(dt => dt.TextContent.Trim()));
        Assert.Equal("Ikke oppgitt", facts.QuerySelectorAll("dd")[2].TextContent.Trim());
    }

    [Fact]
    public void Heading_WhenTheVariableHasAName_ThenTheCodeIsStillUnderIt()
    {
        // The code came out of the variabelutforsker's hit list (Fhi.Metadata-l9l2n.69) and it must
        // not follow it out of here. This page and the saved list are what an applicant attaches to
        // an application, and the code is the thing they are asking for by; a reader who can see
        // the variable and not its identifier has the wrong half.
        Assert.Equal("ALSFRSR1Tale", Render(Detail()).Find("p.munin-explorer-whole__code").TextContent.Trim());
    }

    [Fact]
    public void Heading_WhenTheCatalogueLeftTheNameEmpty_ThenTheCodeStandsInAndIsNotDrawnTwice()
    {
        // The third view with the same shape and a third contract. The code caption below the
        // heading goes when the heading has taken it, so the page does not show it stacked twice.
        // (Fhi.Metadata-w13lk)
        var cut = Render(Detail() with { PreferredTerm = "" });

        Assert.Equal("ALSFRSR1Tale", cut.Find("h2").TextContent.Trim());
        Assert.Empty(cut.FindAll("p.munin-explorer-whole__code"));
    }

    [Fact]
    public void Heading_WhenTheCodeStandsInForTheName_ThenItIsNotMarkedAsNorwegian()
    {
        // A code is not Norwegian prose. The named case is asserted beside it so the marker cannot
        // be dropped wholesale and still pass.
        Assert.Null(Render(Detail() with { PreferredTerm = "" }, "en").Find("h2").GetAttribute("lang"));
        Assert.Equal("no", Render(Detail(), "en").Find("h2").GetAttribute("lang"));
    }

    [Theory]
    [InlineData("2", "Heltall")]
    [InlineData("Integer", "Heltall")]
    [InlineData("string", "Streng")]
    [InlineData("BOOLEAN", "Boolsk")]
    public void DataType_OnANorwegianPage_NeverShowsTheApisEnglishName(string rawValue, string expected)
    {
        // The read model is not fully re-normalized, so a legacy raw value can
        // still arrive alongside the canonical numeric codes. Either shape must resolve to
        // Norwegian. (Fhi.Metadata-88fui)
        var cut = Render(Detail() with { DataType = rawValue });

        Assert.Contains(expected, cut.Markup, StringComparison.Ordinal);
        foreach (var english in new[] { "String", "Integer", "Boolean", "Decimal", "Datetime" })
        {
            Assert.DoesNotContain(english, cut.Markup, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------------------------
    // The section each block sits in, which is what a contents nav will anchor on.
    // ---------------------------------------------------------------------------------

    /// <summary>Every section this view emits, in document order.</summary>
    private static IReadOnlyList<AngleSharp.Dom.IElement> Wrappers(IRenderedComponent<VariableView> cut) =>
        [.. cut.FindAll("section.munin-explorer-page__section")];

    /// <summary>A variable with every one of this view's eight blocks filled in.</summary>
    /// <remarks>
    /// The plain fixture fills three, so a list read off it would say nothing about the five that
    /// are drawn only when the catalogue has something to put in them.
    /// </remarks>
    private static VariableDetail Whole() => Detail() with
    {
        DataFrom = new DateTimeOffset(2022, 9, 20, 0, 0, 0, TimeSpan.Zero),
        DataTo = new DateTimeOffset(2022, 11, 9, 0, 0, 0, TimeSpan.Zero),
        Versions = [Version(Guid.NewGuid())],
        DatasamlingStatisticsType = "yearly",
        Statistics = [new() { AdditionalProperties = new Dictionary<string, string?> { ["SisteOppdaterteAarssett"] = "2022" } }],
        AllVariabelgrupper = [new() { Id = Guid.NewGuid(), Name = "Funksjonsmål" }],
        AllDatasamlinger = [new() { Id = Guid.NewGuid(), Name = "Inklusjon" }],
    };

    [Fact]
    public void Sections_Always_ThenEveryBlockIsWrappedAndTheNameAboveThemIsNot()
    {
        var cut = Render(Whole());

        Assert.Equal(
            [DetailSectionIds.Metadata, DetailSectionIds.Versions, DetailSectionIds.Statistics,
             DetailSectionIds.Source, DetailSectionIds.DataPeriod, DetailSectionIds.DataType,
             DetailSectionIds.VariableGroups, DetailSectionIds.DataCollections],
            Wrappers(cut).Select(section => section.Id!));

        Assert.All(Wrappers(cut), section =>
        {
            // helsedata's own attribute, which their register pages already carry, rather than one
            // invented here — and the block's heading opens the section rather than sitting above it.
            Assert.True(section.HasAttribute("data-nav-section"));
            Assert.Contains(section.FirstElementChild!.TagName, (string[])["H3", "H4", "H5", "H6"]);
        });

        // The name is the view's own title, not a section of it.
        Assert.Null(cut.Find("h2").Closest("[data-nav-section]"));
    }

    [Fact]
    public void Sections_WhenTheSameVariableIsReadInBothLanguages_ThenOnlyTheHeadingsDiffer()
    {
        // THE TRAP. An id slugged from the heading passes every test that runs in one language and
        // breaks every deep link the moment the other reader opens it.
        var norwegian = Render(Whole(), "no");
        var english = Render(Whole(), "en");

        Assert.Equal(Wrappers(norwegian).Select(s => s.Id!), Wrappers(english).Select(s => s.Id!));

        // Worth nothing unless the headings really do differ. Metadata is the same word in both,
        // which is exactly why the ids cannot be read off them: six of these eight are not.
        Assert.Equal(
            ["Metadata", "Versjonshistorikk", "Kildeinformasjon", "Dataperiode", "Datatype",
             "Variabelgrupper", "Datasamlinger"],
            HeadingsExceptStatistics(norwegian));
        Assert.Equal(
            ["Metadata", "Version history", "Source information", "Data period", "Data type",
             "Variable groups", "Data collections"],
            HeadingsExceptStatistics(english));
    }

    /// <summary>
    /// The section headings, less the statistics one, which carries the catalogue's own
    /// statistikktype inside it and is asserted where that translation is.
    /// </summary>
    private static IEnumerable<string> HeadingsExceptStatistics(IRenderedComponent<VariableView> cut) =>
        Wrappers(cut)
            .Where(section => section.Id != DetailSectionIds.Statistics)
            .Select(section => section.FirstElementChild!.TextContent);

    [Fact]
    public void Sections_WhenABlockDrawsNothing_ThenNoEmptyWrapperIsLeftBehind()
    {
        // The wrapper goes INSIDE each emptiness check. Outside one it would draw a section holding
        // a heading and nothing else, which is worse than the bare heading it replaced. The plain
        // fixture carries no versions, no statistics, no data period and neither list, so five of
        // the eight are suppressed here at once — including the statistics block, whose emptiness
        // check lives in StatisticsBlock rather than in this view.
        var cut = Render(Detail());

        Assert.Equal([DetailSectionIds.Metadata, DetailSectionIds.Source, DetailSectionIds.DataType],
                     Wrappers(cut).Select(section => section.Id!));
        Assert.All(Wrappers(cut), section => Assert.True(
            section.Children.Length > 1, $"Section '{section.Id}' holds its heading and nothing else."));
    }

    /// <summary>
    /// A variable the catalogue has filled in nothing for, which is seven of the eight blocks gone.
    /// Shared with the contents tests below so both ask about the same payload.
    /// </summary>
    private static VariableDetail Sparse() => new()
    {
        Id = Guid.NewGuid(),
        Code = "V",
        PreferredTerm = "V",
    };

    [Fact]
    public void Sections_WhenTheCatalogueHasFilledInNothing_ThenTheSourceBoxStillDrawsARow()
    {
        // Why the source box survives its emptiness check on a payload this bare: KildeTypeLabel
        // answers "Ikke oppgitt" for a variable naming no source, so the list keeps a row even
        // with all three of its fields blank.
        var facts = Render(Sparse())
            .Find($"#{DetailSectionIds.Source} dl.munin-explorer-page__fields");

        // The whole list rather than its first row: a row added above this one is a new row
        // appearing, not the fallback going, and the two should not fail alike.
        Assert.Equal(["Type datakilde"], facts.QuerySelectorAll("dt").Select(dt => dt.TextContent.Trim()));
        Assert.Equal("Ikke oppgitt", facts.QuerySelector("dd")!.TextContent.Trim());
    }

    [Fact]
    public void Sections_Always_ThenNoTwoOfThemShareAnId()
    {
        // Plain ids are only safe because an explorer renders at most one detail view: VariableSearch
        // picks between the three arms of one if/else, KildeSearch between two. This view writes the
        // most of the three - eight - so it is where a second use of one would first go unnoticed.
        var ids = Wrappers(Render(Whole())).Select(section => section.Id!).ToList();

        Assert.Equal(ids.Distinct(StringComparer.Ordinal), ids);
    }

    // ---------------------------------------------------------------------------------
    // The contents nav in the column beside them.
    // ---------------------------------------------------------------------------------

    /// <summary>Where the nav's entries point, in document order.</summary>
    private static IReadOnlyList<string> Targets(IRenderedComponent<VariableView> cut) =>
        [.. cut.FindAll(".munin-explorer-page__toc a").Select(link => link.GetAttribute("href")!)];

    /// <summary>What the nav's entries say, in document order.</summary>
    private static IReadOnlyList<string> Entries(IRenderedComponent<VariableView> cut) =>
        [.. cut.FindAll(".munin-explorer-page__toc a").Select(link => link.TextContent)];

    [Fact]
    public void Contents_Always_ThenEveryEntryPointsAtASectionThatIsReallyThere()
    {
        // All eight blocks, read against the sections themselves rather than against a list written
        // here: an entry taken off the sections a view COULD draw is the dead anchor this nav is
        // likeliest to produce, and the two cannot drift apart while this compares them.
        var cut = Render(Whole());

        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));
        Assert.Equal(Wrappers(cut).Select(section => section.FirstElementChild!.TextContent), Entries(cut));
    }

    [Fact]
    public void Contents_WhenAnExplorerHandsTheViewNamedSections_ThenEachIsDrawnAndListedAfterTheMetadata()
    {
        // Where Runa's kodeverk goes, and off one list for both halves (Fhi.Metadata-fkiz9).
        var cut = Render<VariableView>(b => b
            .Add(c => c.Variable, Whole())
            .Add(c => c.NamedSections, NamedSectionsFixture.Two));

        Assert.Equal(["#" + DetailSectionIds.Metadata, "#first", "#second", "#" + DetailSectionIds.Versions],
                     Targets(cut).Take(4));
        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));
        Assert.Equal(Wrappers(cut).Select(section => section.FirstElementChild!.TextContent), Entries(cut));
        Assert.Single(Wrappers(cut).Select(section => section.FirstElementChild!.TagName).Distinct());
    }

    [Fact]
    public void Sections_WhenAHostPassesThem_ThenTheyComeAfterTheNamedSections()
    {
        var cut = Render<VariableView>(b => b
            .Add(c => c.Variable, Whole())
            .Add(c => c.NamedSections, NamedSectionsFixture.Two)
            .Add(c => c.Sections, (RenderFragment)(host => host.AddMarkupContent(0, "<p id=\"host-sections\">Fra verten</p>"))));

        var ids = cut.Find(".munin-explorer-whole__main").Children.Select(e => e.Id ?? "").ToArray();

        Assert.Equal(["first", "second", "host-sections", DetailSectionIds.Versions],
                     ids.SkipWhile(id => id != "first").Take(4));
    }

    [Fact]
    public void Contents_WhenANamedSectionReusesAnIdOfTheViewsOwn_ThenTheViewsEmptyBlockStaysOff()
    {
        // The view's predicates read its own entries, not the named ones: otherwise a named
        // "versions" would switch on an empty version history under the same id.
        var cut = Render<VariableView>(b => b
            .Add(c => c.Variable, Detail())
            .Add(c => c.NamedSections,
                 [new DetailNamedSection(DetailSectionIds.Versions, "Mine versjoner",
                                         body => body.AddContent(0, "x"))]));

        var versions = Assert.Single(Wrappers(cut), section => section.Id == DetailSectionIds.Versions);

        Assert.Equal("Mine versjoner", versions.FirstElementChild!.TextContent);
    }

    [Fact]
    public void Contents_WhenABlockDrawsNothing_ThenItGetsNoEntryEither()
    {
        // The plain fixture, which suppresses five of the eight — the statistics block among them,
        // whose emptiness check lives in StatisticsBlock rather than in this view. A nav offering
        // any of the five would be offering a link to an anchor that is not in the document.
        var cut = Render(Detail());

        Assert.Equal(
            ["#" + DetailSectionIds.Metadata, "#" + DetailSectionIds.Source, "#" + DetailSectionIds.DataType],
            Targets(cut));
    }

    [Fact]
    public void Contents_WhenTheSameVariableIsReadInBothLanguages_ThenOnlyTheWordsDiffer()
    {
        // THE TRAP once more, one column over from the sections: the words translate and the hrefs
        // must not, or a link one reader sends lands nowhere for the other.
        var norwegian = Render(Whole(), "no");
        var english = Render(Whole(), "en");

        Assert.Equal(Targets(norwegian), Targets(english));

        // The statistics entry is left out of both, for the reason HeadingsExceptStatistics gives:
        // the catalogue's own statistikktype is inside that heading.
        Assert.Equal(
            ["Metadata", "Versjonshistorikk", "Kildeinformasjon", "Dataperiode", "Datatype",
             "Variabelgrupper", "Datasamlinger"],
            Entries(norwegian).Where((_, index) => index != 2));
        Assert.Equal(
            ["Metadata", "Version history", "Source information", "Data period", "Data type",
             "Variable groups", "Data collections"],
            Entries(english).Where((_, index) => index != 2));
    }

    [Fact]
    public void Contents_Always_ThenTheNavIsNamedInTheReadersLanguage()
    {
        // A landmark among the host page's own, so it says which navigation it is.
        Assert.Equal("Innhold",
                     Render(Whole(), "no").Find(".munin-explorer-page__toc nav").GetAttribute("aria-label"));
        Assert.Equal("Contents",
                     Render(Whole(), "en").Find(".munin-explorer-page__toc nav").GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData("whole")]
    [InlineData("plain")]
    [InlineData("sparse")]
    public void Contents_WhateverTheCatalogueFilledIn_ThenEveryLinkResolvesToASectionInTheDocument(string fixture)
    {
        // The one assertion that catches a predicate in BuildToc drifting from the condition on its
        // block, which is a dead in-page link no compiler and no markup test sees. Asked of a full
        // payload, a plain one and one with nothing in it at all, because a list and a set of
        // predicates agree most easily when everything is present.
        var cut = Render(fixture switch { "whole" => Whole(), "plain" => Detail(), _ => Sparse() });

        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));

        // Resolved through the DOM rather than compared as strings: `#metadata` is also the CSS
        // selector for the element it has to land on, so this is the browser's own question.
        Assert.All(Targets(cut), href => Assert.NotNull(cut.Find(href)));
    }

    [Fact]
    public void Contents_WhenTheCatalogueFilledInNothing_ThenOneEntrySurvivesAndTheColumnIsStillDrawn()
    {
        // Sparse() suppresses seven of the eight and the source box keeps the last one, so the
        // emptiest nav this view can reach is one entry and the column is drawn. The no-entries
        // path — a null Column, no rail at all — is unreachable here and pinned in DetailTocTest.
        var cut = Render(Sparse());

        Assert.Equal(["#" + DetailSectionIds.Source], Targets(cut));
        Assert.Single(cut.FindAll(".munin-explorer-page__toc"));
    }

    [Fact]
    public void Contents_WhenTheVariableIsReplacedAfterTheFirstRender_ThenTheNavIsRebuiltWithIt()
    {
        // Toc is cached and rebuilt only in OnParametersSet, so every predicate it reads has to be
        // a parameter or something derived from one. They are — Groups, Versions, SourceInformation
        // and both lists all hang off Variable — and this is what says so if one stops being.
        var cut = Render(Sparse());

        Assert.Equal(["#" + DetailSectionIds.Source], Targets(cut));

        cut.Render(p => p.Add(c => c.Variable, Whole()));

        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));
        Assert.Contains("#" + DetailSectionIds.Versions, Targets(cut));
    }

    [Theory]
    [InlineData("yearly", "Statistikk (Årsbasert)")]
    [InlineData("accumulated", "Statistikk (Akkumulert)")]
    [InlineData("akkumulert", "Statistikk (Akkumulert)")]
    [InlineData("kvartalsvis", "Statistikk (kvartalsvis)")]
    [InlineData(null, "Statistikk")]
    public void Contents_WhateverTheStatisticsTypeIs_ThenTheNavEntrySaysWhatTheHeadingSays(
        string? statisticsType, string expected)
    {
        // The nav has to name this block without drawing it, and the block names itself from a
        // field. Two spellings of that would be two spellings of one fact — a reader sees one
        // wording in the nav and another over what it jumps to, and nothing fails. Every type the
        // catalogue has been seen to send, plus one it has not, plus none at all.
        var cut = Render(Whole() with { DatasamlingStatisticsType = statisticsType });

        var heading = cut.Find($"#{DetailSectionIds.Statistics}").FirstElementChild!.TextContent;
        var entry = cut.Find($".munin-explorer-page__toc a[href='#{DetailSectionIds.Statistics}']").TextContent;

        Assert.Equal(expected, heading);
        Assert.Equal(heading, entry);
    }

    [Fact]
    public void DataType_WhenTheCatalogueHoldsOnlyWhitespace_ThenNeitherTheBlockNorItsNavEntryIsDrawn()
    {
        // Present but blank is its own case, and the branch that tells it from null is the one no
        // rich fixture reaches: a section reading "Datatype" over an empty paragraph, and a nav
        // entry pointing at it.
        var cut = Render(Detail() with { DataType = "   " });

        Assert.DoesNotContain(DetailSectionIds.DataType, Wrappers(cut).Select(section => section.Id!));
        Assert.DoesNotContain("#" + DetailSectionIds.DataType, Targets(cut));
    }

    [Fact]
    public void Sections_WhenTwoOfThisViewAreDrawn_ThenBothWriteTheSameIdsRatherThanIdsOfTheirOwn()
    {
        // The deep-link promise and its price in one assertion. Nothing per-instance goes in these
        // ids, so a link one reader sends another lands in the same place — and TWO of these views
        // in one document would therefore carry every id twice, with the browser resolving each
        // nav link to the first. That is why a page mounts one; DetailSectionIds has the reasoning.
        Assert.Equal(Wrappers(Render(Whole())).Select(section => section.Id!),
                     Wrappers(Render(Whole())).Select(section => section.Id!));
    }

    // ---------------------------------------------------------------------------------
    // The hero row: the six facts a variable leads with, under the name block.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// A variable with all six of the facts the row leads with, which no other fixture here has.
    /// </summary>
    /// <remarks>
    /// Three of the six are curated properties rather than typed fields, so the metadata has to
    /// carry their vocabularies as well as their keys: a value resolved off the bag instead of
    /// through the catalogue's own options is the code — <c>5</c> — rather than the word.
    /// </remarks>
    private static VariableDetail Leading() => Whole() with
    {
        KodeverkLinks =
        [
            new() { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", DisplayName = "ALSFRS-R tale" },
        ],
        PropertyMetadata =
        [
            Entry("Kommentar", 50, "Beskrivelse"),
            Entry("DataType", 20, "Datatype", """[{"value":"2","label":"Integer","labelEn":"Integer"}]"""),
            Named("Opprinnelse", 30, "Beskrivelse", "Opprinnelse", "Origin",
                  """[{"value":"5","label":"Direkte fra skjema","labelEn":"Directly from form"}]"""),
            Named("Identifiseringsgrad", 160, "Personvern", "Identifiseringsgrad", "Identification level",
                  """[{"value":"1","label":"Ikke vurdert","labelEn":"Not assessed"}]"""),
            Named("DatabaseReferanse", 240, "Teknisk", "Databasereferanse", "Database reference"),
        ],
        AdditionalProperties = new Dictionary<string, string?>
        {
            ["Kommentar"] = "Gyldig fra 2019.",
            ["DataType"] = "2",
            ["Opprinnelse"] = "5",
            ["Identifiseringsgrad"] = "1",
            ["DatabaseReferanse"] = "ALSFRSR1Tale",
        },
    };

    /// <summary>
    /// A curated entry the catalogue names in both languages, which the captured payload does and
    /// <see cref="Entry"/> does not.
    /// </summary>
    /// <remarks>
    /// The label is the hero cell's label as well as the group row's, so a fixture naming it in one
    /// language only would test the fallback rather than the pairing.
    /// </remarks>
    private static PropertyMetadataEntry Named(string key, int sortOrder, string group,
                                               string label, string labelEnglish,
                                               string? optionsJson = null) =>
        new()
        {
            Key = key,
            SortOrder = sortOrder,
            GroupTranslations = new Dictionary<string, string> { ["no"] = group, ["en"] = group },
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = label, ["en"] = labelEnglish },
            OptionsJson = optionsJson,
        };

    private static AngleSharp.Dom.IElement Hero(IRenderedComponent<VariableView> cut) =>
        cut.Find("dl.munin-explorer-page__facts");

    private static AngleSharp.Dom.IElement Cell(AngleSharp.Dom.IElement hero, string label) =>
        hero.QuerySelectorAll("div").FirstOrDefault(cell => cell.QuerySelector("dt")?.TextContent == label)
        ?? throw new InvalidOperationException(
            $"No '{label}' cell in the hero row, only: "
            + $"{string.Join(", ", hero.QuerySelectorAll("dt").Select(dt => dt.TextContent))}.");

    /// <summary>One hero cell's value, without the note under it.</summary>
    private static string HeroValue(AngleSharp.Dom.IElement hero, string label)
    {
        var cell = Cell(hero, label).QuerySelector("dd")!;
        var note = cell.QuerySelector("small");

        return note is null ? cell.TextContent : cell.TextContent[..^note.TextContent.Length];
    }

    /// <summary>One row of a metadata group, found by the label the catalogue gave it.</summary>
    private static string GroupValue(IRenderedComponent<VariableView> cut, string label) =>
        cut.Find($"#{DetailSectionIds.Metadata}")
           .QuerySelectorAll("div")
           .FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
           ?.QuerySelector("dd")?.TextContent
        ?? throw new InvalidOperationException($"No '{label}' row among the metadata groups.");

    [Fact]
    public void HeroFacts_Always_ThenTheyAreTheFiveProposedPlusTheDataPeriod()
    {
        // Kilde and Datasamling are deliberately absent although the strip this replaces led with
        // both: the breadcrumb directly above names them, and a strip that repeats the chrome
        // spends two of six slots on facts the reader has just read (Fhi.Metadata-l9l2n.92). The
        // sixth is Dataperiode, which passes the same test — no crumb carries it.
        Assert.Equal(
            ["Kodeverk", "Statistikk", "Opprinnelse", "Identifiseringsgrad", "Databasereferanse",
             "Dataperiode"],
            Hero(Render(Leading())).QuerySelectorAll("dt").Select(dt => dt.TextContent));
    }

    [Fact]
    public void HeroFacts_Always_ThenEachReadsTheSameWordsAsTheSectionThatDrawsItBelow()
    {
        // The comparison this bead turns on. The three curated facts resolve through the same call
        // their groups are built from, so a vocabulary edited in Munin moves both at once — which
        // is precisely what DataType's old duplication did not do.
        var cut = Render(Leading());
        var hero = Hero(cut);

        Assert.Equal(GroupValue(cut, "Opprinnelse"), HeroValue(hero, "Opprinnelse"));
        Assert.Equal(GroupValue(cut, "Identifiseringsgrad"), HeroValue(hero, "Identifiseringsgrad"));
        Assert.Equal(GroupValue(cut, "Databasereferanse"), HeroValue(hero, "Databasereferanse"));
        Assert.Equal(DataPeriod(cut), HeroValue(hero, "Dataperiode"));

        // The word the vocabulary resolves to rather than the code the bag holds, which is the
        // whole reason these go through CatalogueProperties.
        Assert.Equal("Direkte fra skjema", HeroValue(hero, "Opprinnelse"));

        // Statistikk is the two halves the heading below joins, read off the same two members.
        Assert.Equal(cut.Find($"#{DetailSectionIds.Statistics}").FirstElementChild!.TextContent,
                     $"Statistikk ({HeroValue(hero, "Statistikk")})");
    }

    [Fact]
    public void HeroFacts_Always_ThenNoCuratedKeyIsDroppedFromTheGroupsBelow()
    {
        // The collision this bead had to resolve deliberately. DrawnElsewhere suppresses a key from
        // the metadata groups, and three of the six facts are curated keys — added to that set they
        // would vanish from Beskrivelse, Personvern and Teknisk, which is a relocation rather than
        // a summary. Only DataType is in it, and DataType is not a hero fact.
        var cut = Render(Leading());

        Assert.Equal(["Opprinnelse", "Kommentar", "Identifiseringsgrad", "Databasereferanse"],
                     cut.Find($"#{DetailSectionIds.Metadata}")
                        .QuerySelectorAll("dl dt").Select(dt => dt.TextContent));
        Assert.Empty(cut.Find(".munin-explorer-page__body").QuerySelectorAll("dl.munin-explorer-page__facts"));
    }

    [Fact]
    public void HeroFacts_WhenTheVariableHasNoneOfThem_ThenNoRowIsDrawnRatherThanAnEmptyOne()
    {
        // Unlike a source, a variable can lead with nothing at all: not one of the six falls back
        // to a word. The plain fixture is that variable — no kodeverk, no statistics, no data
        // period and none of the three curated keys.
        Assert.Empty(Render(Detail()).FindAll("dl.munin-explorer-page__facts"));
    }

    [Fact]
    public void HeroFacts_WhenTheKindOfStatisticsHasNoStatisticsUnderIt_ThenNeitherIsDrawn()
    {
        // Statistikktype belongs to the owning datasamling while the rows are the variable's own,
        // so a newly pinned variable in a yearly collection has a kind and nothing of that kind —
        // which is the hero-over-an-absent-section disagreement this row exists to rule out.
        var cut = Render(Leading() with { Statistics = [] });

        Assert.DoesNotContain("Statistikk",
                              Hero(cut).QuerySelectorAll("dt").Select(dt => dt.TextContent));
        Assert.Empty(cut.FindAll($"#{DetailSectionIds.Statistics}"));
    }

    [Fact]
    public void HeroFacts_WhenTheCatalogueNamesNoKodeverk_ThenTheRowIsFiveRatherThanPaddedToSix()
    {
        // Six is the shape the grid is ruled for, not a quota to fill: a fact the catalogue has not
        // filled in is dropped, and the tracks beside it stay empty.
        var hero = Hero(Render(Leading() with { KodeverkLinks = [] }));

        Assert.Equal(["Statistikk", "Opprinnelse", "Identifiseringsgrad", "Databasereferanse",
                      "Dataperiode"],
                     hero.QuerySelectorAll("dt").Select(dt => dt.TextContent));
    }

    [Fact]
    public void HeroFacts_WhenAKodeverkHasNoName_ThenTheCellSaysSoRatherThanShowingItsReference()
    {
        // The fallback the panel's own kodeverk block makes: a reference standing in for a name
        // reads as the kodeverk being called 2336. The kind is the note either way, because a bare
        // reference is what it is missing.
        var hero = Hero(Render(Leading() with
        {
            KodeverkLinks = [new() { KodeverkType = "Kildekodeverk", KodeverkReference = "2336" }],
        }));

        Assert.Equal("Ukjent navn", HeroValue(hero, "Kodeverk"));
        Assert.Equal("Kildekodeverk", Cell(hero, "Kodeverk").QuerySelector("small")!.TextContent);
    }

    [Fact]
    public void HeroFacts_WhenTheReaderIsEnglish_ThenACuratedValueIsMarkedByTheLanguageItIsReallyIn()
    {
        // Curation is uneven, so an English page carries some Norwegian — and a hero cell has to
        // say which, for the reason every value on these pages does: a screen reader switches voice
        // on the mark and reads Norwegian with English phonetics without it.
        var hero = Hero(Render(Leading() with
        {
            AdditionalProperties = new Dictionary<string, string?>
            {
                ["DatabaseReferanse"] = "ALSFRSR1Tale",
                ["Opprinnelse"] = "5",
            },
        }, language: "en"));

        // The catalogue resolves this one to an English word, so it is the reader's own language.
        Assert.Empty(Cell(hero, "Origin").QuerySelectorAll("span"));

        // And the free-text one is stored once, in Norwegian, however the reader is reading.
        Assert.Equal("no", Cell(hero, "Database reference").QuerySelector("span")!.GetAttribute("lang"));
    }
}
