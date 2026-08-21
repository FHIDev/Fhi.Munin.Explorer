using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The whole kilde: the view both explorers render a source with — its name block, the catalogue's
/// own metadata, the datasamlinger it holds and the two sidebar boxes.
/// </summary>
/// <remarks>
/// Written because this component had no test class of its own. The suite had one for the explorer,
/// one for the variable view and one for the filter panel, and the kilde view was only ever reached
/// sideways, through the explorer's drill-in — so the parameters it exists for had no coverage.
/// The parameters are the point, in two different ways. <see cref="KildeView.Sections"/> and
/// <see cref="KildeView.DataCollectionsHeading"/> are the whole reason this is a shared core rather
/// than two views, and no explorer in this repository wires either: Kelda is what will.
/// <see cref="KildeView.HeadingLevel"/> and <see cref="KildeView.HeadingId"/> are wired — the
/// explorer passes both at VariableExplorer.razor:104-107 — and the explorer's tests already follow
/// two of the things that come out: the title's level, mounted at h1 and asserted h2
/// (<c>Source_WhenThePanelIsOpen_ThenItsHeadingSitsBelowTheCardsInTheOutline</c>,
/// VariableExplorerTest.cs:5763), and the id the landmark is named by, resolved back to this view's
/// name heading (<c>Source_WhenOpened_ThenTheToggleAndTheRegionAreWiredToEachOther</c>, :5478).
/// What no test covered is narrower: the levels the blocks under the title land on, and either
/// parameter with the view rendered whole rather than reached through the drill-in.
/// <para>
/// The class-name check is the one thing here that was already hanging somewhere: the explorer's
/// own <c>Source_WhenAPanelIsOpen_ThenItIsBuiltFromShapesRatherThanFromANewStyleName</c>
/// (VariableExplorerTest.cs:5701) opens the drill-in and pins nine of the ten names this view
/// emits. Both lists are worth keeping — that one guards the path a reader actually takes, this one
/// guards the view rendered whole — but they are two hand-maintained lists in two files, so a name
/// added or renamed here has to be answered in both.
/// </para>
/// </remarks>
public class KildeViewTest : BunitContext
{
    private static PropertyMetadataEntry Entry(string key, int sortOrder, string group, string? displayName = null) =>
        new()
        {
            Key = key,
            SortOrder = sortOrder,
            GroupTranslations = new Dictionary<string, string> { ["no"] = group },
            DisplayNameTranslations = new Dictionary<string, string> { ["no"] = displayName ?? key },
        };

    /// <summary>A source shaped like the ALS register, which is the one the captured payloads hold.</summary>
    private static KildeDetail Kilde() => new()
    {
        Id = Guid.NewGuid(),
        Code = "K_ALS",
        ShortName = "ALS",
        PreferredTerm = "Als registeret",
        Description = "Norsk register for ALS og andre motonevronsykdommer.",
        Kildetype = "nasjonaltMedisinskKvalitetsregister",
        LegalBasis = "Forskrift om medisinske kvalitetsregistre § 2-3.",
        // Two organisations rather than one twice: with the same string in both, a component
        // reading DataProcessor into the dataansvarlig row would satisfy every assertion here while
        // attributing the data to the wrong body.
        DataController = "St. Olavs hospital HF",
        DataProcessor = "Hemit HF",
        PersonIdentificationLevel = "indirectlyIdentifiable",
        ValidFrom = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
        LastUpdated = new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
        DataFrom = new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TotalVariables = 312,
        PropertyMetadata =
        [
            Entry("Formaal", 10, "Formål", "Formål med registeret"),
            Entry("Kommentar", 50, "Beskrivelse"),
            // Curated but never filled in, which is the common case: five groups of a typical
            // source's thirteen go this way, so a group that draws anyway draws blank rows.
            Entry("Datakvalitet", 20, "Datakvalitet"),
        ],
        AdditionalProperties = new Dictionary<string, string?>
        {
            ["Formaal"] = "Kvalitetssikring av behandlingen.",
            ["Kommentar"] = "Gyldig fra 2019.",
        },
        Datasamlinger = [Collection("Inklusjon")],
        Delkilder = [],
    };

    private static KildeDatasamling Collection(
        string name,
        int? order = null,
        string? shortName = null,
        string description = "",
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int variables = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ShortName = shortName,
            Description = description,
            PresentationOrder = order,
            EffectiveValidFrom = from,
            EffectiveValidTo = to,
            VariableCount = variables,
        };

    private static KildeDelkilde Delkilde(
        string name,
        IReadOnlyList<KildeDatasamling> datasamlinger,
        IReadOnlyList<KildeDelkilde>? children = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Datasamlinger = datasamlinger,
            Children = children ?? [],
        };

    /// <summary>Markup a host might hang after the metadata, carrying no class of its own.</summary>
    private static readonly RenderFragment KeldaSections = builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "id", "kelda-sections");
        builder.AddContent(2, "Tilgangskriterier");
        builder.CloseElement();
    };

    private IRenderedComponent<KildeView> Render(
        KildeDetail? kilde,
        string? language = null,
        int headingLevel = 2,
        string? headingId = null,
        string? dataCollectionsHeading = null,
        RenderFragment? sections = null) =>
        Render<KildeView>(b =>
        {
            b.Add(c => c.Kilde, kilde)
             .Add(c => c.Language, language)
             .Add(c => c.HeadingLevel, headingLevel)
             .Add(c => c.HeadingId, headingId)
             .Add(c => c.DataCollectionsHeading, dataCollectionsHeading);

            // Left unset rather than set to null when no explorer passes any, which is the state a
            // host actually renders this view in.
            if (sections is not null)
            {
                b.Add(c => c.Sections, sections);
            }
        });

    /// <summary>The sidebar's first box — the facts every source has.</summary>
    private static IElement SourceInformation(IRenderedComponent<KildeView> cut) =>
        Box(cut, T => T.HeadingSourceInformation);

    /// <summary>The sidebar's second box — the counts and dates.</summary>
    private static IElement Statistics(IRenderedComponent<KildeView> cut) =>
        Box(cut, T => T.HeadingStatistics);

    /// <summary>
    /// One sidebar box, found by the heading over it rather than by its position.
    /// </summary>
    /// <remarks>
    /// A box whose every fact is blank draws no <c>dl</c> at all, while its heading is drawn
    /// unconditionally — so the boxes slide up under headings that stay put, and a position would
    /// hand back the statistics for a call asking after the source information without being able
    /// to say it had. The heading can say it: the lookup starts there, and a box that stopped
    /// drawing is reported as itself rather than as whatever the next call misreads.
    /// </remarks>
    private static IElement Box(IRenderedComponent<KildeView> cut, Func<Texts, string> boxHeading)
    {
        var aside = cut.Find(".variable-explorer-kilde__aside");
        var name = boxHeading(Texts.For(cut.Instance.Language));

        var heading = aside.Children.FirstOrDefault(e => e.TextContent == name)
                      ?? throw new InvalidOperationException(
                          $"No '{name}' heading in the sidebar, only: "
                          + $"{string.Join(", ", aside.Children.Select(e => e.TextContent))}.");

        return heading.NextElementSibling is { TagName: "DL" } box
            ? box
            : throw new InvalidOperationException(
                $"The '{name}' heading is followed by {heading.NextElementSibling?.TagName ?? "nothing"} "
                + "rather than by its own box, so that box drew no facts at all.");
    }

    private static IReadOnlyList<string> Labels(IElement list) =>
        [.. list.QuerySelectorAll("dt").Select(e => e.TextContent)];

    private static IReadOnlyList<string> Values(IElement list) =>
        [.. list.QuerySelectorAll("dd").Select(e => e.TextContent)];

    /// <summary>One row's value cell, found by the label beside it rather than by its position.</summary>
    /// <remarks>
    /// A row's index is a function of both the order the component lists its fields in and which of
    /// them the fixture filled in, since a blank value draws no row — so an index in a test that
    /// does not also assert the labels names a row that a field inserted upstream silently moves.
    /// Asking by label makes the assertion self-locating, and makes the failure say which row went
    /// missing rather than reading the wrong one's text back.
    /// </remarks>
    private static IElement Fact(IElement list, string label) =>
        list.QuerySelectorAll("div").FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
            ?.QuerySelector("dd")
        ?? throw new InvalidOperationException(
            $"No '{label}' row in this box, only: {string.Join(", ", Labels(list))}.");

    /// <inheritdoc cref="Fact"/>
    private static string Value(IElement list, string label) => Fact(list, label).TextContent;

    /// <summary>The datasamling rows, by the name each is headed with.</summary>
    private static IReadOnlyList<string> CollectionNames(IRenderedComponent<KildeView> cut) =>
        [.. cut.FindAll("table.variable-explorer-kilde__datasamlinger tbody th").Select(e => e.TextContent)];

    private static IReadOnlyList<string> BlockHeadings(IRenderedComponent<KildeView> cut) =>
        [.. cut.FindAll(".variable-explorer-kilde__body .headline-s").Select(e => e.TextContent)];

    // ---------------------------------------------------------------------------------
    // Styling contract. The package ships no CSS, so every class name this view emits is
    // a promise that some stylesheet — helsedata's, or the sample one a host copies —
    // already defines it. A name in neither renders as a raw browser default inside an
    // otherwise styled page, which is the failure this package exists to avoid.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Always_ThenEveryClassNameIsOneSomeStylesheetActuallyDefines()
    {
        // The check that had nowhere to hang. This view is where `headline-sm` lived longest — a
        // typo for `headline-s` on all four block headings, defined nowhere, so every one of them
        // rendered at the browser's own <h*> size on helsedata.no. It reaches the DOM as an argument
        // to @Heading rather than as a class attribute, which puts it out of reach of grep and of
        // the CSS checks in scripts/; rendering the component is the only way to see it.
        var cut = Render(Kilde());

        // Compared against an empty list rather than asserted empty, so a failure names the classes
        // instead of saying only that there were some.
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles()
    {
        // The exact list, for the reason the explorer's own version of this is exact: a tenth name
        // appearing here is news, and news that has to be answered in both sample stylesheets before
        // it ships. None of these is helsedata's — the six of theirs in the variable-explorer prefix
        // are all on the explorer, none on this view — so every one is a promise only the sample
        // stylesheet keeps.
        //
        // It is the second such list: VariableExplorerTest.cs:5719 pins nine of these ten down the
        // drill-in path, all but variable-explorer-group, which that fixture's kilde has no metadata
        // groups to produce. Renaming a handle means editing both, and the other one fails with a
        // message about the explorer rather than about this view.
        var cut = Render(Kilde());

        var invented = HostClassNames.Of(cut.FindAll("[class]"))
            .Where(k => k.StartsWith("variable-explorer", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            "variable-explorer-group",                  // shared with the variable view
            "variable-explorer-kilde",
            "variable-explorer-kilde__aside",
            "variable-explorer-kilde__body",
            "variable-explorer-kilde__datasamlinger",
            "variable-explorer-kilde__description",
            "variable-explorer-kilde__header",
            "variable-explorer-kilde__identifiers",
            "variable-explorer-kilde__kildetype",
            "variable-explorer-kilde__main",
        ], invented);
    }

    // ---------------------------------------------------------------------------------
    // The name block.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenNoKildeHasArrived_ThenNothingIsDrawnAtAll()
    {
        // The parameter is EditorRequired but the caller sets it from a fetch, so null is the state
        // between opening the view and the payload landing. An empty shell — a header rule, a
        // sidebar box with a heading and no facts — reads as a source with nothing in it.
        Assert.Equal(string.Empty, Render(kilde: null).Markup.Trim());
    }

    [Fact]
    public void Identifiers_WhenTheKildeHasAShortName_ThenItSitsBesideTheCode()
    {
        Assert.Equal("K_ALS (ALS)", Render(Kilde()).Find(".variable-explorer-kilde__identifiers").TextContent);
    }

    [Fact]
    public void Identifiers_WhenTheKildeHasNoShortName_ThenTheCodeStandsAloneWithoutEmptyBrackets()
    {
        var cut = Render(Kilde() with { ShortName = "" });

        Assert.Equal("K_ALS", cut.Find(".variable-explorer-kilde__identifiers").TextContent);
    }

    [Fact]
    public void Identifiers_WhenTheKildeHasNoCode_ThenNoEmptyLineIsDrawnUnderTheName()
    {
        // A line holding only " (ALS)" is worse than no line: it reads as a rendering fault rather
        // than as a catalogue that has not been given a code.
        var cut = Render(Kilde() with { Code = "" });

        Assert.Empty(cut.FindAll(".variable-explorer-kilde__identifiers"));
    }

    [Fact]
    public void Kildetype_WhenTheCatalogueSendsItsEnumName_ThenTheBadgeSaysItInProse()
    {
        Assert.Equal("Nasjonalt medisinsk kvalitetsregister",
                     Render(Kilde()).Find(".variable-explorer-kilde__kildetype").TextContent);
    }

    [Fact]
    public void Kildetype_WhenItIsOneWeHaveNeverSeen_ThenItIsShownRatherThanHidden()
    {
        // Munin's kildetype enum is master data, so a new member is a catalogue change rather than a
        // bug here. "Pasientregister" on a badge is poor prose and true, where dropping it would
        // take the source's category off the screen entirely.
        var cut = Render(Kilde() with { Kildetype = "pasientregister" });

        Assert.Equal("pasientregister", cut.Find(".variable-explorer-kilde__kildetype").TextContent);
    }

    [Fact]
    public void Kildetype_WhenTheKildeHasNone_ThenNoEmptyBadgeIsDrawnButTheSidebarStillSaysSo()
    {
        // A badge is a shape as much as a word, so an empty one is a stray coloured box. The
        // sidebar is a record and answers the question either way — "Ikke oppgitt" is the answer
        // there, and a missing row would leave a reader wondering whether it was asked.
        var cut = Render(Kilde() with { Kildetype = "" });

        Assert.Empty(cut.FindAll(".variable-explorer-kilde__kildetype"));
        Assert.Equal("Ikke oppgitt", Value(SourceInformation(cut), "Type datakilde"));
    }

    [Fact]
    public void Description_WhenTheKildeHasNone_ThenNoEmptyIngressIsDrawn()
    {
        Assert.Empty(Render(Kilde() with { Description = null }).FindAll(".variable-explorer-kilde__description"));
    }

    // ---------------------------------------------------------------------------------
    // Heading level and the id a landmark is named by — the two things that let one view
    // sit both on a page of its own and inside a result the explorer opened.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Headings_WhenTheViewSitsDeeperInThePage_ThenEveryLevelUnderTheNameFollowsIt()
    {
        // Heading order is how a screen reader user navigates, not decoration. A view that always
        // emitted h2 would break the order wherever it opened inside a result row, and one that
        // moved only its own name would leave the blocks under it at a level above their heading.
        var cut = Render(Kilde(), headingLevel: 3);

        Assert.Equal("h3", cut.Find(".variable-explorer-kilde__header .headline-s").TagName, ignoreCase: true);

        // Counted as well as checked: Assert.All passes over an empty collection, so a selector
        // that stopped matching would leave this test green while checking nothing.
        var blocks = cut.FindAll(".variable-explorer-kilde__body .headline-s");
        var groups = cut.FindAll(".variable-explorer-group");

        Assert.Equal(4, blocks.Count);
        Assert.Equal(2, groups.Count);
        Assert.All(blocks, h => Assert.Equal("h4", h.TagName, ignoreCase: true));
        Assert.All(groups, h => Assert.Equal("h5", h.TagName, ignoreCase: true));
    }

    [Fact]
    public void Headings_WhenTheViewSitsAsDeepAsHeadingsGo_ThenTheLevelsStopAtSixRatherThanRunningPastIt()
    {
        // h7 is not a heading, it is an unknown element: a screen reader lists none of them, so the
        // deepest mount would lose the whole structure rather than flatten it.
        var cut = Render(Kilde(), headingLevel: 6);

        var headings = cut.FindAll(".headline-s");
        var groups = cut.FindAll(".variable-explorer-group");

        Assert.Equal(5, headings.Count);
        Assert.Equal(2, groups.Count);
        Assert.All(headings, h => Assert.Equal("h6", h.TagName, ignoreCase: true));
        Assert.All(groups, h => Assert.Equal("h6", h.TagName, ignoreCase: true));
        Assert.Empty(cut.FindAll("h7"));
    }

    [Fact]
    public void HeadingId_WhenTheHostNamesARegionByTheName_ThenTheIdIsOnTheNameAndNowhereElse()
    {
        // The drill-in is a landmark, and a landmark is only useful if a screen reader can say which
        // source it just entered. That means the id has to be on this component's own name — a
        // second heading outside it saying the same thing is a different heading.
        var cut = Render(Kilde(), headingId: "kilde-heading");

        var named = cut.FindAll("#kilde-heading");

        Assert.Single(named);
        Assert.Equal("Als registeret", named[0].TextContent);
    }

    [Fact]
    public void HeadingId_WhenTheHostNamesNothing_ThenNoEmptyIdIsEmitted()
    {
        // An id="" is not nothing: it is an id no aria-labelledby can point at, and two of them on
        // one page are duplicates.
        Assert.Empty(Render(Kilde()).FindAll("[id]"));
    }

    // ---------------------------------------------------------------------------------
    // The metadata the catalogue arranges itself.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Metadata_WhenTheCatalogueGroupsTheProperties_ThenEachGroupIsAHeadingOverItsOwnList()
    {
        // Which groups exist, what they are called and what order they come in all arrive with the
        // payload. The empty one is dropped rather than drawn: a heading promising something over
        // blank rows is the failure this rule was measured against on Runa.
        var cut = Render(Kilde());

        Assert.Equal(["Formål", "Beskrivelse"],
                     cut.FindAll(".variable-explorer-group").Select(e => e.TextContent));
        Assert.DoesNotContain("Datakvalitet", cut.Markup, StringComparison.Ordinal);

        var first = cut.FindAll(".variable-explorer-kilde__main dl")[0];

        Assert.Equal(["Formål med registeret"], Labels(first));
        Assert.Equal(["Kvalitetssikring av behandlingen."], Values(first));
    }

    [Fact]
    public void Metadata_WhenTheCatalogueHasFilledInNothing_ThenNoHeadingPromisesAny()
    {
        var cut = Render(Kilde() with { AdditionalProperties = new Dictionary<string, string?>() });

        // Asked of the block headings rather than of the whole markup, which anything satisfies: a
        // data-metadata handle or a PropertyMetadata key in an attribute would fail a substring
        // check for a reason that has nothing to do with a heading promising a block.
        Assert.DoesNotContain("Metadata", BlockHeadings(cut));
        Assert.Empty(cut.FindAll(".variable-explorer-group"));
    }

    // ---------------------------------------------------------------------------------
    // The datasamlinger the source holds.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void DataCollections_WhenSomeHangUnderADelkilde_ThenTheyAreListedBesideTheKildesOwn()
    {
        // A study series keeps its datasamlinger one per wave, under delkilder nested to any depth.
        // Listing only the ones hanging off the kilde itself would show one row where the reader can
        // reach three — a delkilde is how the catalogue is organised, not a reason to hide what is
        // inside it.
        var kilde = Kilde() with
        {
            Datasamlinger = [Collection("Inklusjon")],
            Delkilder =
            [
                Delkilde("Tromsø 4",
                         [Collection("Spørreskjema")],
                         [Delkilde("Første besøk", [Collection("Blodprøver")])]),
            ],
        };

        Assert.Equal(["Blodprøver", "Inklusjon", "Spørreskjema"], CollectionNames(Render(kilde)));
    }

    [Fact]
    public void DataCollections_WhenTheCatalogueHasOrderedSome_ThenThoseComeFirstAndTheRestAlphabetically()
    {
        // Two rules in one list. A curated order is what Munin's own views follow, so it wins; the
        // ones nobody has ordered fall back to the alphabet — the Norwegian one, because the names
        // are the catalogue's and stored once in Norwegian, so å sorts last whoever is reading.
        //
        // Two pairs, because "Norwegian" is two claims and Ålesund only carries one of them. Å above
        // Alta is what an English reader's collation would get wrong, since English folds Å to A —
        // but it is also what a plain byte comparison gets right by accident, U+00C5 being above
        // every ASCII letter, so a comparer with no collation at all would pass on that pair alone.
        // Élan before Fana is the other half: Norwegian sorts É with E, ordinal puts U+00C9 after F.
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Ålesund"),
                Collection("Bergen", order: 2),
                Collection("Fana"),
                Collection("Alta"),
                Collection("Élan"),
                Collection("Oslo", order: 1),
            ],
        };

        Assert.Equal(["Oslo", "Bergen", "Alta", "Élan", "Fana", "Ålesund"],
                     CollectionNames(Render(kilde, language: "en")));
    }

    [Fact]
    public void DataCollections_Always_ThenEachRowIsHeadedByItsNameAndSaysWhatRunaShows()
    {
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Inklusjon", shortName: "INK", description: "Alle pasienter ved inklusjon.",
                           from: new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero), variables: 12),
            ],
        };

        var cut = Render(kilde);

        Assert.Equal(["Navn", "Beskrivelse", "Gyldighet", "Totalt antall variabler"],
                     cut.FindAll("table.variable-explorer-kilde__datasamlinger thead th").Select(e => e.TextContent));

        // A th rather than a td, and scoped to its row: a screen reader reading a cell out of
        // context has to be able to hear which datasamling the number belongs to.
        var name = cut.Find("table.variable-explorer-kilde__datasamlinger tbody th");

        Assert.Equal("row", name.GetAttribute("scope"));
        Assert.Equal("Inklusjon (INK)", name.TextContent);

        Assert.Equal(["Alle pasienter ved inklusjon.", "1. januar 2010 – Pågående", "12 variabler"],
                     cut.FindAll("table.variable-explorer-kilde__datasamlinger tbody td").Select(e => e.TextContent));
    }

    [Fact]
    public void DataCollections_WhenARowStoppedCollecting_ThenTheRowSaysWhenRatherThanOngoing()
    {
        // The same open/closed pair as the sidebar's, asked of the table, because the row's period
        // is the cell a reader uses to tell a wave that has closed from one still running.
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Inklusjon", description: "Alle pasienter ved inklusjon.",
                           from: new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero),
                           to: new DateTimeOffset(2019, 9, 30, 0, 0, 0, TimeSpan.Zero), variables: 12),
            ],
        };

        // The whole row rather than one cell of it, so the assertion says which column it is reading.
        Assert.Equal(["Alle pasienter ved inklusjon.", "1. januar 2010 – 30. september 2019", "12 variabler"],
                     Render(kilde).FindAll("table.variable-explorer-kilde__datasamlinger tbody td")
                                  .Select(e => e.TextContent));
    }

    [Fact]
    public void DataCollections_WhenTheKildeHasNone_ThenNoHeadingPromisesAny()
    {
        var cut = Render(Kilde() with { Datasamlinger = [], Delkilder = [] });

        Assert.Empty(cut.FindAll("table.variable-explorer-kilde__datasamlinger"));
        Assert.DoesNotContain("Datasamlinger", BlockHeadings(cut));
    }

    [Fact]
    public void DataCollections_WhenTheExplorerCallsThemSomethingElse_ThenItsOwnHeadingIsUsed()
    {
        // Runa says "Datasamlinger" over this table; Kelda says "Delkilder og datasamlinger" over
        // the same data. One word of difference is not worth a second table, so the caller supplies
        // the word — and the default has to survive the caller supplying nothing.
        Assert.Contains("Delkilder og datasamlinger",
                        BlockHeadings(Render(Kilde(), dataCollectionsHeading: "Delkilder og datasamlinger")));

        Assert.Contains("Datasamlinger", BlockHeadings(Render(Kilde())));
    }

    // ---------------------------------------------------------------------------------
    // The slot each explorer puts its own sections in.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Sections_WhenAnExplorerPassesThem_ThenTheyComeLastInTheMainColumnRatherThanInTheSidebar()
    {
        // The whole reason this is a core with a slot instead of one view with a flag per Kelda
        // section. They go after the metadata — the source's own record reads first — and in the
        // main column, because the sidebar is the facts every source has and nothing else.
        var cut = Render(Kilde(), sections: KeldaSections);

        var main = cut.Find(".variable-explorer-kilde__main");

        Assert.Equal("kelda-sections", main.Children.Last().Id);
        Assert.Empty(cut.FindAll(".variable-explorer-kilde__aside #kelda-sections"));
    }

    [Fact]
    public void Sections_WhenNoExplorerPassesAny_ThenNothingIsDrawnWhereTheyWouldHaveGone()
    {
        // The datasamling table is the last thing in the column when the slot is empty — no empty
        // wrapper, which would be a stray margin under every source Runa shows.
        var cut = Render(Kilde());

        Assert.Equal("table", cut.Find(".variable-explorer-kilde__main").Children.Last().TagName,
                     ignoreCase: true);
    }

    // ---------------------------------------------------------------------------------
    // The sidebar: the facts every source has, which is why they are typed fields rather
    // than curated properties.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void SourceInformation_Always_ThenItIsTheFieldsRunaShowsInRunasOrder()
    {
        var cut = Render(Kilde());

        Assert.Equal(
            ["Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin"],
            Labels(SourceInformation(cut)));

        var facts = SourceInformation(cut);

        Assert.Equal("Nasjonalt medisinsk kvalitetsregister", Value(facts, "Type datakilde"));

        // Separately, and from two different fields in the fixture: the dataansvarlig and the
        // databehandler are adjacent tuples reading adjacent properties, which is where a
        // copy-paste slip attributes a register's data to the wrong organisation.
        Assert.Equal("St. Olavs hospital HF", Value(facts, "Dataansvarlig"));
        Assert.Equal("Hemit HF", Value(facts, "Databehandler"));

        Assert.Equal("Indirekte identifiserbar", Value(facts, "Grad av personidentifikasjon"));
        Assert.Equal("4. mars 2026", Value(facts, "Sist oppdatert i Munin"));
    }

    [Fact]
    public void SourceInformation_WhenTheCatalogueHasNotFilledInAField_ThenNoBlankRowIsDrawnForIt()
    {
        // A dt with an empty dd reads as a value that failed to draw. The two that stay are the two
        // this package writes itself — a kildetype and an identification level always resolve to a
        // word, "Ikke oppgitt" included.
        var kilde = Kilde() with
        {
            LegalBasis = null,
            DataController = "",
            DataProcessor = "   ",
            ValidFrom = null,
            ValidTo = null,
        };

        Assert.Equal(["Type datakilde", "Grad av personidentifikasjon", "Sist oppdatert i Munin"],
                     Labels(SourceInformation(Render(kilde))));
    }

    [Fact]
    public void Statistics_Always_ThenItIsTheVariableCountAndThePeriodTheDataCovers()
    {
        var cut = Render(Kilde());

        Assert.Equal(["Totalt antall variabler", "Dataperiode"], Labels(Statistics(cut)));
        Assert.Equal("312", Value(Statistics(cut), "Totalt antall variabler"));
    }

    [Fact]
    public void Statistics_WhenTheCatalogueKnowsNoDataDates_ThenNoEmptyPeriodRowIsDrawn()
    {
        var cut = Render(Kilde() with { DataFrom = null, DataTo = null });

        Assert.Equal(["Totalt antall variabler"], Labels(Statistics(cut)));
    }

    [Fact]
    public void Period_WhenTheEndIsOpen_ThenItSaysSoRatherThanSittingBlankOrGuessingADate()
    {
        // An open end is the normal case for a register that is still collecting. A blank half reads
        // as a missing value, and a guessed date would be a claim the catalogue never made.
        Assert.Equal("1. januar 2023 – Pågående",
                     Value(SourceInformation(Render(Kilde())), "Gyldighet"));
    }

    [Fact]
    public void Period_WhenTheEndIsClosed_ThenBothDatesAreShownTheWayTheReadersLanguageWritesThem()
    {
        // A register that stopped collecting is a normal catalogue state, and it was the untested
        // half of this view's own Period: every other fixture here leaves the end open, so the end
        // date, the en-dash that joins the two and the English form of the closing date never ran.
        var closed = Kilde() with
        {
            ValidTo = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            DataTo = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
        };

        var norwegian = Render(closed);

        Assert.Equal("1. januar 2023 – 31. desember 2024", Value(SourceInformation(norwegian), "Gyldighet"));
        Assert.Equal("1. januar 2010 – 30. juni 2024", Value(Statistics(norwegian), "Dataperiode"));

        var english = Render(closed, language: "en");

        Assert.Equal("1 January 2023 – 31 December 2024", Value(SourceInformation(english), "Validity"));
        Assert.Equal("1 January 2010 – 30 June 2024", Value(Statistics(english), "Data period"));
    }

    [Fact]
    public void Period_WhenOnlyTheEndIsKnown_ThenTheDateStandsAloneRatherThanBesideABlankHalf()
    {
        // The third shape, and the one with no good answer: an en-dash with nothing before it reads
        // as a value that failed to draw, and a start date the catalogue never gave would be an
        // invention. So the end stands alone — which does read as a start, and is pinned here
        // because it is a decision rather than an accident. VariableView's own copy of Period does
        // the same, so changing it is a change to both.
        var kilde = Kilde() with
        {
            ValidFrom = null,
            ValidTo = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
        };

        Assert.Equal("31. desember 2024", Value(SourceInformation(Render(kilde)), "Gyldighet"));
    }

    // ---------------------------------------------------------------------------------
    // Language. The catalogue stores one name, one description and one set of free-text
    // values, all in Norwegian, whatever language the reader is reading in.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenOurOwnWordsFollowThem()
    {
        var cut = Render(Kilde(), language: "en");

        Assert.Equal(["Metadata", "Data collections", "Source information", "Statistics"],
                     BlockHeadings(cut));

        // The kildetype and the identification level are vocabularies this package translates, so
        // they follow the reader too — unlike everything the catalogue wrote, which does not.
        Assert.Equal("National medical quality registry",
                     cut.Find(".variable-explorer-kilde__kildetype").TextContent);
        Assert.Equal("Indirectly identifiable",
                     Value(SourceInformation(cut), "Level of personal identification"));
        Assert.Equal(["Total number of variables", "Data period"], Labels(Statistics(cut)));
    }

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenTheCataloguesOwnWordsAreMarkedNorwegianAndOursAreNot()
    {
        // The lang attribute's only job is to pick the voice. Marking our own English prose as
        // Norwegian tells a screen reader to pronounce English by Norwegian rules, which is worse
        // than leaving it unmarked — and leaving the catalogue's Norwegian unmarked on an English
        // page is the same fault the other way round.
        var kilde = Kilde() with
        {
            Datasamlinger = [Collection("Inklusjon", description: "Alle pasienter ved inklusjon.")],
        };

        var cut = Render(kilde, language: "en");

        Assert.Equal("no", cut.Find(".variable-explorer-kilde__header .headline-s").GetAttribute("lang"));
        Assert.Equal("no", cut.Find(".variable-explorer-kilde__description").GetAttribute("lang"));
        Assert.Equal("no", cut.Find("table.variable-explorer-kilde__datasamlinger tbody th").GetAttribute("lang"));

        // Ours: the kildetype badge is this package's translation of an enum, not the catalogue's
        // prose, and so is the identification level beside it in the sidebar.
        Assert.False(cut.Find(".variable-explorer-kilde__kildetype").HasAttribute("lang"));

        var facts = SourceInformation(cut);

        Assert.False(Fact(facts, "Type of data source").HasAttribute("lang"));   // ours
        Assert.Equal("no", Fact(facts, "Legal basis").GetAttribute("lang"));     // the catalogue's
    }

    [Fact]
    public void Language_WhenTheReaderIsNorwegian_ThenNothingIsMarkedAtAll()
    {
        // Text already in the reader's language is left unmarked so it inherits from the host. A
        // lang="no" on every element would be noise, and noise a host cannot override.
        var cut = Render(Kilde());

        Assert.Empty(cut.FindAll("[lang]"));
    }

    [Fact]
    public void Dates_WhenTheReaderIsEnglish_ThenTheyAreWrittenTheWayEnglishWritesThem()
    {
        // The dot after the day is not punctuation, it is what makes the number an ordinal in
        // Norwegian. English does not use it, so an English page carrying the Norwegian skeleton
        // with English month names — "1. January 2023" — is neither language.
        Assert.Equal("1 January 2023 – Ongoing",
                     Value(SourceInformation(Render(Kilde(), language: "en")), "Validity"));
    }
}
