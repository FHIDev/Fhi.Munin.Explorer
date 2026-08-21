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
/// Written because nothing rendered this component at all. The suite had a class for the explorer,
/// one for the variable view and one for the filter panel, and the kilde view was only ever reached
/// sideways, through the explorer's drill-in — so the parameters it exists for had no coverage at
/// all, and the class-name check that catches a name no stylesheet defines had nowhere to hang for
/// this view. The parameters are the point: <see cref="KildeView.Sections"/>,
/// <see cref="KildeView.HeadingLevel"/>, <see cref="KildeView.HeadingId"/> and
/// <see cref="KildeView.DataCollectionsHeading"/> are the whole reason this is a shared component
/// rather than two views, and none of them is exercised by rendering the explorer.
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
        DataController = "St. Olavs hospital HF",
        DataProcessor = "St. Olavs hospital HF",
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
        int variables = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ShortName = shortName,
            Description = description,
            PresentationOrder = order,
            EffectiveValidFrom = from,
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
        cut.FindAll(".variable-explorer-kilde__aside dl")[0];

    /// <summary>The sidebar's second box — the counts and dates.</summary>
    private static IElement Statistics(IRenderedComponent<KildeView> cut) =>
        cut.FindAll(".variable-explorer-kilde__aside dl")[1];

    private static IReadOnlyList<string> Labels(IElement list) =>
        [.. list.QuerySelectorAll("dt").Select(e => e.TextContent)];

    private static IReadOnlyList<string> Values(IElement list) =>
        [.. list.QuerySelectorAll("dd").Select(e => e.TextContent)];

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
        var cut = Render(Kilde());

        var invented = cut.FindAll("[class]")
            .SelectMany(e => e.ClassList)
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
        Assert.Equal("Ikke oppgitt", Values(SourceInformation(cut))[0]);
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

        Assert.DoesNotContain("Metadata", cut.Markup, StringComparison.Ordinal);
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
        var kilde = Kilde() with
        {
            Datasamlinger =
            [
                Collection("Ålesund"),
                Collection("Bergen", order: 2),
                Collection("Alta"),
                Collection("Oslo", order: 1),
            ],
        };

        Assert.Equal(["Oslo", "Bergen", "Alta", "Ålesund"], CollectionNames(Render(kilde, language: "en")));
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

        var values = Values(SourceInformation(cut));

        Assert.Equal("Nasjonalt medisinsk kvalitetsregister", values[0]);
        Assert.Equal("St. Olavs hospital HF", values[2]);
        Assert.Equal("Indirekte identifiserbar", values[4]);
        Assert.Equal("4. mars 2026", values[6]);
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
        Assert.Equal("312", Values(Statistics(cut))[0]);
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
        var values = Values(SourceInformation(Render(Kilde())));

        Assert.Equal("1. januar 2023 – Pågående", values[5]);
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
        Assert.Equal("Indirectly identifiable", Values(SourceInformation(cut))[4]);
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

        var facts = SourceInformation(cut).QuerySelectorAll("dd");

        Assert.False(facts[0].HasAttribute("lang"));      // type of data source — ours
        Assert.Equal("no", facts[1].GetAttribute("lang"));  // legal basis — the catalogue's
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
        var values = Values(SourceInformation(Render(Kilde(), language: "en")));

        Assert.Equal("1 January 2023 – Ongoing", values[5]);
    }
}
