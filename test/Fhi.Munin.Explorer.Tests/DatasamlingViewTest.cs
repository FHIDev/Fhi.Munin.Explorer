using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The whole datasamling: its name block, the catalogue's own metadata, who the data includes and
/// the blocks saying who owns it and how much of it there is.
/// </summary>
/// <remarks>
/// Written with the view itself, which replaced a flat list of eleven fields inside the variable
/// explorer's drill-in — a list that drew none of the curated metadata the payload carries
/// (Fhi.Metadata-jgfum). Nearly every fixture here is the captured payload rather than a
/// hand-written record, mutated where a case needs it: the hand-written one carried only the simple
/// shape, which is how the missing metadata stayed invisible while the panel's own tests passed.
/// <para>
/// The class-name check is the one the bead names as the trap. The sample hosts style the names
/// themselves, so opening the view in one shows nothing wrong however the names are spelled; the
/// guard is the only thing that answers whether a stylesheet anywhere draws them, and a new
/// component is exactly where new names appear.
/// </para>
/// </remarks>
public class DatasamlingViewTest : ExplorerTestContext
{
    /// <summary>
    /// The live payload, captured: six curated keys, two of the four groups filled in, every
    /// inherited field null on the datasamling itself and set on its <c>Effective…</c> twin.
    /// </summary>
    private static DatasamlingDetail Datasamling() =>
        JsonSerializer.Deserialize<DatasamlingDetail>(
            TestData.Read("datasamling.json"), MuninExplorerClient.Json)
        ?? throw new InvalidOperationException("datasamling.json no longer reads as a DatasamlingDetail.");

    private IRenderedComponent<DatasamlingView> Render(
        DatasamlingDetail? datasamling,
        string? language = null,
        int headingLevel = 2,
        string? headingId = null,
        IReadOnlyList<DetailTrailStep>? trail = null,
        RenderFragment? sections = null,
        Func<Guid, string>? kildeHref = null) =>
        Render<DatasamlingView>(b =>
        {
            b.Add(c => c.Datasamling, datasamling)
             .Add(c => c.Language, language)
             .Add(c => c.HeadingLevel, headingLevel)
             .Add(c => c.HeadingId, headingId)
             .Add(c => c.KildeHref, kildeHref)
             .Add(c => c.Trail, trail);

            // Left unset rather than set to null when no explorer passes any, which is the state a
            // host actually renders this view in.
            if (sections is not null)
            {
                b.Add(c => c.Sections, sections);
            }
        });

    [Fact]
    public void Eyebrow_Always_ThenItNamesTheKindOfPageAndIsNotAHeading()
    {
        // The eyebrow is above the title and is a <p>: rendered as an <h*> it would be a second
        // title in the outline, naming a category rather than the thing on screen. Both languages,
        // because a word that only translates in one of them reads as the component's own noise.
        var eyebrow = Render(Datasamling()).Find(".munin-explorer-page__eyebrow");

        Assert.Equal("P", eyebrow.TagName);
        Assert.Equal("Datasamling", eyebrow.TextContent.Trim());
        Assert.Equal("Data collection", Render(Datasamling(), language: "en").Find(".munin-explorer-page__eyebrow").TextContent.Trim());
    }

    [Fact]
    public void Trail_WhenACallerSuppliesTheStepsAbove_ThenThisPageIsAppendedAsTheCurrentStep()
    {
        // The view appends its own name rather than the caller repeating it, so the last step is
        // the page by construction — and the one step a reader can never be sent to a dead link by.
        var cut = Render(Datasamling(), trail: [new DetailTrailStep("Kildeutforsker", "/kilder")]);

        var steps = cut.FindAll("nav.breadcrumbs li");

        Assert.Equal("Kildeutforsker", steps[0].TextContent.Trim());
        Assert.Equal("/kilder", Assert.Single(steps[0].QuerySelectorAll("a")).GetAttribute("href"));

        var last = steps[^1];

        Assert.Equal("page", last.GetAttribute("aria-current"));
        Assert.Empty(last.QuerySelectorAll("a"));
        Assert.Equal(
            cut.Find(".munin-explorer-datasamling__header .headline-s").TextContent.Trim(),
            last.TextContent.Trim());
    }

    [Fact]
    public void Trail_WhenNoCallerSuppliesSteps_ThenNoBreadcrumbIsDrawn()
    {
        // The state every mount of this view is in today outside the kildeutforsker: no addresses
        // to offer, so no trail rather than one step that goes nowhere.
        Assert.Empty(Render(Datasamling()).FindAll("nav.breadcrumbs"));
    }

    /// <summary>Markup a host might hang after the view's sections, carrying no class of its own.</summary>
    private static readonly RenderFragment ExplorerSections = builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "id", "explorer-sections");
        builder.AddContent(2, "Tilgangskriterier");
        builder.CloseElement();
    };

    /// <summary>
    /// One fact box, found by the heading over it rather than by its position.
    /// </summary>
    /// <remarks>
    /// Either box is drawn only when it has a row, so a position would hand back the wrong one
    /// without being able to say it had. The heading can say it.
    /// </remarks>
    private static IElement Box(IRenderedComponent<DatasamlingView> cut, string heading)
    {
        var main = cut.Find(".munin-explorer-datasamling__main");

        var found = main.Children.FirstOrDefault(
                        e => e.QuerySelector("h3, h4, h5, h6")?.TextContent == heading)
                    ?? throw new InvalidOperationException(
                        $"No '{heading}' section in the main column, only: "
                        + $"{string.Join(", ", main.QuerySelectorAll("h3, h4, h5, h6").Select(e => e.TextContent))}.");

        return found.QuerySelector("dl")
               ?? throw new InvalidOperationException(
                   $"The '{heading}' section holds no box, so it drew no facts at all.");
    }

    private static IElement SourceInformation(IRenderedComponent<DatasamlingView> cut) =>
        Box(cut, Texts.For(cut.Instance.Language).HeadingSourceInformation);

    private static IReadOnlyList<string> Labels(IElement list) =>
        [.. list.QuerySelectorAll("dt").Select(e => e.TextContent)];

    /// <summary>One row's value cell, found by the label beside it rather than by its position.</summary>
    /// <remarks>
    /// A placement can take a row out of the box, so an index names a row that silently moves.
    /// Asking by label makes the failure say which row went missing.
    /// </remarks>
    private static string Value(IElement list, string label) =>
        Row(list, label).QuerySelector("dd")?.TextContent
        ?? throw new InvalidOperationException(
            $"No value cell in the '{label}' row, only: {string.Join(", ", Labels(list))}.");

    /// <summary>All three marks of an absent fact in one check, so no test asserts the word without the muting.</summary>
    private static void AssertAbsent(IElement list, string label)
    {
        var value = Row(list, label).QuerySelector("dd")!;

        Assert.Equal("Ingen", value.TextContent);
        Assert.Equal(DetailBlocks.Absent, value.ClassName);
        Assert.Null(value.GetAttribute("lang"));
    }

    /// <summary>The whole row, for a test asking what the value cell is made of.</summary>
    private static IElement Row(IElement list, string label) =>
        list.QuerySelectorAll("div").FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
        ?? throw new InvalidOperationException(
            $"No '{label}' row in this box, only: {string.Join(", ", Labels(list))}.");

    /// <summary>The hero row, for a datasamling that has at least one of its six.</summary>
    private static IElement Hero(IRenderedComponent<DatasamlingView> cut) =>
        cut.Find("dl.munin-explorer-page__facts");

    /// <summary>One hero cell, found by the label beside it.</summary>
    private static IElement Cell(IElement hero, string label) =>
        hero.QuerySelectorAll("div").FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
        ?? throw new InvalidOperationException(
            $"No '{label}' cell in the hero row, only: {string.Join(", ", Labels(hero))}.");

    [Fact]
    public void HeroFacts_Always_ThenTheyAreTheSourcePagesSixOverThisCollectionsOwnValues()
    {
        // The same six as a source, deliberately: a reader moving between a source and one of its
        // collections is comparing them, and a row that reorders itself between the two pages is a
        // row they have to read twice. Gyldighet stands where a source has Dataperiode, which is
        // the period a datasamling actually carries.
        //
        // Kilde is not among them although the fact box below shows it: the breadcrumb directly
        // above already names the source, and a strip that repeats the chrome spends a slot on
        // something the reader has just read.
        Assert.Equal(
            ["Type datakilde", "Dataansvarlig", "Grad av personidentifikasjon", "Gyldighet",
             "Antall variabler", "Lovverk"],
            Labels(Hero(Render(Datasamling()))));
    }

    [Fact]
    public void HeroFacts_Always_ThenEachReadsTheSameWordsAsTheSectionThatDrawsItBelow()
    {
        // The repetition is deliberate and the disagreement is the bug — every value here is the
        // member the section reads, and every one of them is the inherited Effective… twin.
        var cut = Render(Datasamling());
        var hero = Hero(cut);
        var source = SourceInformation(cut);

        Assert.Equal(Value(source, "Type datakilde"), Value(hero, "Type datakilde"));
        Assert.Equal(Value(source, "Dataansvarlig"), Value(hero, "Dataansvarlig"));
        Assert.Equal(Value(source, "Grad av personidentifikasjon"),
                     Value(hero, "Grad av personidentifikasjon"));
        Assert.Equal(Value(source, "Gyldighet"), Value(hero, "Gyldighet"));
        Assert.Equal(Value(source, "Lovverk"), Value(hero, "Lovverk"));
        Assert.Equal(Value(Box(cut, "Statistikk (årsbasert)"), "Antall variabler"),
                     Value(hero, "Antall variabler"));
    }

    [Fact]
    public void HeroFacts_WhenKildetypeAndIdentificationLevelAreNull_ThenBothDropOutRatherThanReadNotSpecified()
    {
        // The hero reads the same member as the box below, which now answers nothing rather than
        // "Ikke oppgitt". (Fhi.Metadata-35w0p.24)
        var hero = Hero(Render(Datasamling() with
        {
            EffectiveKildetype = null,
            EffectivePersonIdentificationLevel = null,
        }));

        Assert.Equal(["Dataansvarlig", "Gyldighet", "Antall variabler", "Lovverk"], Labels(hero));
        Assert.DoesNotContain("Ikke oppgitt", hero.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void HeroFacts_Always_ThenNothingIsTakenOutOfTheSectionsBelow()
    {
        // A summary, not a relocation. The fact box still draws all nine fields, Kilde and
        // Databehandler included, and no key joins the drawnElsewhere set on account of the row —
        // that set names Beskrivelse, which the ingress draws, and nothing else.
        var cut = Render(Datasamling());

        Assert.Equal(
            ["Kilde", "Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin",
             "Opprettet i Munin"],
            Labels(SourceInformation(cut)));
        Assert.Equal(["Statistikktype", "Frekvens", "Telleenhet", "Antall variabler"],
                     Labels(Box(cut, "Statistikk (årsbasert)")));
        Assert.Empty(cut.Find(".munin-explorer-page__body").QuerySelectorAll("dl.munin-explorer-page__facts"));
    }

    [Fact]
    public void HeroFacts_WhenTheCatalogueNamesNoCountingUnit_ThenTheCountCarriesNoEmptyNote()
    {
        // This payload's telleEnhet is the empty string, which is the common case — so the captured
        // datasamling is the render that would ship an empty <small> under every count.
        Assert.Empty(Hero(Render(Datasamling())).QuerySelectorAll("small"));
    }

    [Fact]
    public void HeroFacts_WhenTheCatalogueNamesACountingUnit_ThenItQualifiesTheCountBeneathIt()
    {
        // 99 variabler counted per what is a different claim from 99 variabler, which is the whole
        // job of the second line — and the unit stays in Statistikk below, where it already was.
        var cut = Render(Datasamling() with { CountingUnit = "Pasient" });

        Assert.Equal("Telleenhet: Pasient",
                     Cell(Hero(cut), "Antall variabler").QuerySelector("small")!.TextContent);
        Assert.Equal("Pasient", Value(Box(cut, "Statistikk (årsbasert)"), "Telleenhet"));
    }

    [Fact]
    public void HeroFacts_WhenTheReaderIsEnglish_ThenTheCataloguesOwnWordsAreMarkedAndOursAreNot()
    {
        // The same mix as the source page, on the same two fields: the kildetype and the
        // identification level are vocabularies this package translates, the controller and the
        // legal basis are inherited free text stored once, in Norwegian, however the reader reads.
        var hero = Hero(Render(Datasamling(), language: "en"));

        Assert.Equal("no", Cell(hero, "Data controller").QuerySelector("span")!.GetAttribute("lang"));
        Assert.Equal("no", Cell(hero, "Legal basis").QuerySelector("span")!.GetAttribute("lang"));
        Assert.Empty(Cell(hero, "Type of data source").QuerySelectorAll("span"));
        Assert.Empty(Cell(hero, "Level of personal identification").QuerySelectorAll("span"));
    }

    [Fact]
    public void HeroFacts_WhenTheReaderIsEnglish_ThenTheCountingUnitIsMarkedAsTheSectionMarksIt()
    {
        // Telleenhet is catalogue free text held only in Norwegian, and the note is the one place
        // this page writes it outside the Statistikk row that marks it — unmarked here, the same
        // word would be read to an English reader with English phonetics four cells above.
        var english = Render(Datasamling() with { CountingUnit = "Pasient" }, language: "en");
        var note = Cell(Hero(english), "Number of variables").QuerySelector("small")!;

        // The unit alone, not the line: "Counting unit" is this package's word and is translated,
        // so a mark on the <small> would announce an English label in a Norwegian voice — which is
        // how the Statistikk row below marks the same field, label unmarked and value marked.
        Assert.Equal("Counting unit: Pasient", note.TextContent);
        Assert.False(note.HasAttribute("lang"));
        Assert.Equal("no", note.QuerySelector("span")!.GetAttribute("lang"));
        Assert.Equal("Pasient", note.QuerySelector("span")!.TextContent);

        // And a Norwegian reader is told nothing, because Norwegian is the page they are reading.
        Assert.Empty(Cell(Hero(Render(Datasamling() with { CountingUnit = "Pasient" })), "Antall variabler")
                         .QuerySelector("small")!.QuerySelectorAll("span"));
    }

    /// <summary>The headings of the blocks under the name, in the order they are drawn.</summary>
    private static IReadOnlyList<string> BlockHeadings(IRenderedComponent<DatasamlingView> cut) =>
        [.. cut.FindAll(".munin-explorer-page__body .headline-s").Select(e => e.TextContent)];

    // ---------------------------------------------------------------------------------
    // Styling contract. The package ships no CSS, so every class name this view emits is
    // a promise that some stylesheet — helsedata's, or the sample one a host copies —
    // already defines it.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_Always_ThenEveryClassNameIsOneSomeStylesheetActuallyDefines()
    {
        // The trap the bead names. The sample hosts style these names themselves, so opening the
        // view in one shows nothing wrong however they are spelled — this is the only check that
        // asks whether a stylesheet anywhere draws them.
        var cut = Render(Datasamling());

        // Compared against an empty list rather than asserted empty, so a failure names the classes
        // instead of saying only that there were some.
        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }

    [Fact]
    public void Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles()
    {
        // The exact list, for the reason the kilde view's version of it is exact: one more name here
        // is news, and news that has to be answered in both sample stylesheets before it ships.
        // None of these was ever helsedata's, so every one is a promise only the samples keep.
        var cut = Render(Datasamling());

        var invented = HostClassNames.Of(cut.FindAll("[class]"))
            .Where(HostClassNames.IsOwnStructureName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            // A fact the catalogue holds nothing for; the fixture carries no Frekvens.
            "munin-explorer-absent",
            "munin-explorer-datasamling",
            "munin-explorer-datasamling__criteria",
            "munin-explorer-datasamling__description",
            "munin-explorer-datasamling__header",
            "munin-explorer-datasamling__identifiers",
            "munin-explorer-datasamling__main",
            "munin-explorer-group",                   // shared with the kilde and variable views
            // The chassis the three detail views share, worn beside this view's own names above.
            "munin-explorer-page",
            "munin-explorer-page__body",
            // The word above the name block saying what kind of thing this page is about. A <p>,
            // so the outline a screen reader navigates by is the one the view already had.
            "munin-explorer-page__eyebrow",
            // The hero row under the name block, the source page's six over the collection's own
            // values.
            "munin-explorer-page__facts",
            // Every fact list this view draws, the chassis's own name since
            // Fhi.Metadata-35w0p.11 rather than the result row's drill-in panel's.
            "munin-explorer-page__fields",
            "munin-explorer-page__main",
            // The wrapper each block below the name sits in, so the contents nav can anchor on it.
            "munin-explorer-page__section",
            // The sticky bar, drawn hidden on every detail page and shown by the browser module
            // alone. Its `--on` state is written only from JavaScript, so it is not in this list.
            "munin-explorer-page__stuckbar",
            "munin-explorer-page__stuckbar-inner",
            "munin-explorer-page__stuckbar-name",
            // The contents column, drawn now that the nav fills it. The nav inside wears
            // helsedata's own form-menu names, which is why it adds none of ours.
            "munin-explorer-page__toc",
        ], invented);
    }

    [Fact]
    public void Chassis_WhenTheViewIsDrawn_ThenTheSharedNamesAreWornBesideThisViewsOwn()
    {
        // Both sets on every element but the body: the chassis is added beside this view's own
        // prefix rather than replacing it, so a host rule keyed on either one still draws.
        var cut = Render(Datasamling());

        Assert.Contains("munin-explorer-datasamling", cut.Find(".munin-explorer-page").ClassList);

        var body = Assert.Single(cut.FindAll(".munin-explorer-page__body"));

        // The body, and only the body, sheds its older name: an element wearing both would carry a
        // `grid-template-columns` from each block, settled by which stylesheet the host loaded last.
        Assert.Equal("munin-explorer-page__body", body.ClassName);
        Assert.Contains("munin-explorer-datasamling__main", cut.Find(".munin-explorer-page__main").ClassList);

        // The contents nav fills the column, so the body is two children in two tracks and the nav
        // comes first — ahead in the DOM of the sections it points into, not only beside them.
        Assert.Equal(
            ["munin-explorer-page__toc", "munin-explorer-page__main munin-explorer-datasamling__main"],
            body.Children.Select(child => child.ClassName));
    }

    // ---------------------------------------------------------------------------------
    // The name block.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenNoDatasamlingHasArrived_ThenNothingIsDrawnAtAll()
    {
        // The parameter is EditorRequired but the caller sets it from a fetch, so null is the state
        // between opening the view and the payload landing. An empty shell would be a header rule
        // and a fact box drawn around nothing.
        Assert.Empty(Render(datasamling: null).Markup.Trim());
    }

    [Fact]
    public void Identifiers_Always_ThenTheCodeStandsAloneUnderTheName()
    {
        // The code, and not the kortNavn beside it the way the kilde view puts a source's: on a
        // datasamling that field holds the owning kilde's abbreviation — "ALS" on every one of the
        // ALS register's — so it names the kilde rather than the datasamling, and the source
        // information already says which kilde this is.
        var cut = Render(Datasamling());

        Assert.Equal("K_ALS.INKLUSJON",
                     cut.Find(".munin-explorer-datasamling__identifiers").TextContent);
    }

    [Fact]
    public void Description_WhenItOnlyRepeatsTheName_ThenNoIngressRestatesIt()
    {
        // A quarter of the datasamlinger in the test catalogue store the name again as the
        // beskrivelse. An ingress saying what the heading above it says reads as a rendering fault.
        var cut = Render(Datasamling() with { Description = "  Inklusjon " });

        Assert.Empty(cut.FindAll(".munin-explorer-datasamling__description"));
    }

    [Fact]
    public void Description_WhenTheCatalogueHasOne_ThenItIsProseUnderTheNameRatherThanAFactRow()
    {
        var cut = Render(Datasamling());

        Assert.StartsWith("Skjemaet inneholder opplysninger",
                          cut.Find(".munin-explorer-datasamling__description").TextContent.Trim(),
                          StringComparison.Ordinal);

        Assert.DoesNotContain("Beskrivelse", Labels(SourceInformation(cut)));
    }

    // ---------------------------------------------------------------------------------
    // The catalogue's own metadata — the half the flat list this view replaced drew none of.
    // ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("no", new[] { "Beskrivelse", "EHDS / HealthDCAT-AP" })]
    [InlineData("en", new[] { "Description", "EHDS / HealthDCAT-AP" })]
    public void Metadata_WhenARealDatasamlingIsDrawn_ThenEveryGroupItFilledInIsThereInTheReadersLanguage(
        string language, string[] expected)
    {
        // Read as a list rather than searched for, so a group that stops being drawn is a failure
        // and not merely unreported, and so the catalogue's own order is asserted with it. Two of
        // this payload's four groups are curated and empty and must not appear.
        var cut = Render(Datasamling(), language);

        Assert.Equal(expected, cut.FindAll(".munin-explorer-group").Select(e => e.TextContent));

        foreach (var heading in cut.FindAll(".munin-explorer-group"))
        {
            Assert.Equal("DL", heading.NextElementSibling?.TagName);
            Assert.NotEmpty(heading.NextElementSibling!.QuerySelectorAll("dd"));
        }
    }

    /// <summary>
    /// The merged values, the suppression set and the grouping are resolved once per (Datasamling,
    /// Language) pair rather than per read, and a cache keyed on either half alone goes stale on the
    /// other.
    /// </summary>
    /// <remarks>
    /// Worth pinning because a fact box asks about a dozen placement questions per render, each of
    /// which used to merge the values and rebuild the suppression set again (Fhi.Metadata-43jrq, on
    /// the kilde side). The same payload instance is passed back deliberately: a fresh one would
    /// miss the cache on its reference alone and say nothing about the language half.
    /// </remarks>
    [Theory]
    [InlineData("no", "Beskrivelse")]
    [InlineData("en", "Description")]
    public void Metadata_WhenOnlyTheLanguageChanges_ThenTheCachedGroupsAreResolvedAgain(
        string language, string expected)
    {
        var datasamling = Datasamling();
        var cut = Render(datasamling, language: language == "no" ? "en" : "no");

        cut.Render(b => b.Add(c => c.Datasamling, datasamling).Add(c => c.Language, language));

        Assert.Equal(expected, cut.FindAll(".munin-explorer-group")[0].TextContent);
    }

    /// <inheritdoc cref="Metadata_WhenOnlyTheLanguageChanges_ThenTheCachedGroupsAreResolvedAgain"/>
    [Fact]
    public void Metadata_WhenOnlyTheDatasamlingChanges_ThenTheCachedGroupsAreResolvedAgain()
    {
        var cut = Render(Datasamling());

        cut.Render(b => b.Add(c => c.Datasamling,
                              Datasamling() with { AdditionalProperties = new Dictionary<string, string?>() }));

        Assert.Empty(cut.FindAll(".munin-explorer-group"));
    }

    [Fact]
    public void Metadata_WhenTheCatalogueHasFilledInNothing_ThenNoHeadingPromisesAny()
    {
        var cut = Render(Datasamling() with { AdditionalProperties = new Dictionary<string, string?>() });

        // Asked of the block headings rather than of the whole markup, which anything satisfies: a
        // PropertyMetadata key in an attribute would fail a substring check for a reason that has
        // nothing to do with a heading promising a block.
        Assert.DoesNotContain("Metadata", BlockHeadings(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-group"));
    }

    // ---------------------------------------------------------------------------------
    // The section each block sits in, which is what a contents nav will anchor on.
    // ---------------------------------------------------------------------------------

    /// <summary>Every section this view emits, in document order.</summary>
    private static IReadOnlyList<IElement> Wrappers(IRenderedComponent<DatasamlingView> cut) =>
        [.. cut.FindAll("section.munin-explorer-page__section")];

    [Fact]
    public void Sections_Always_ThenEveryBlockIsWrappedAndTheNameAboveThemIsNot()
    {
        var cut = Render(Datasamling());

        Assert.Equal(
            [DetailSectionIds.Metadata, DetailSectionIds.Criteria,
             DetailSectionIds.Source, DetailSectionIds.Statistics],
            Wrappers(cut).Select(section => section.Id!));

        Assert.All(Wrappers(cut), section =>
        {
            // helsedata's own attribute, which their register pages already carry, rather than one
            // invented here — and the block's heading opens the section rather than sitting above it.
            Assert.True(section.HasAttribute("data-nav-section"));
            Assert.Contains(section.FirstElementChild!.TagName, (string[])["H3", "H4", "H5", "H6"]);

            // A fragment jump moves focus only to a focusable target, so without this the reader
            // is scrolled here and their next Tab carries on from the nav — WCAG 2.4.3.
            Assert.Equal("-1", section.GetAttribute("tabindex"));
        });

        // The name is the view's own title, not a section of it.
        Assert.Null(cut.Find("h2").Closest("[data-nav-section]"));
    }

    [Fact]
    public void Sections_WhenTheSameDatasamlingIsReadInBothLanguages_ThenOnlyTheHeadingsDiffer()
    {
        // THE TRAP. An id slugged from the heading passes every test that runs in one language and
        // breaks every deep link the moment the other reader opens it.
        var norwegian = Render(Datasamling(), language: "no");
        var english = Render(Datasamling(), language: "en");

        Assert.Equal(Wrappers(norwegian).Select(s => s.Id!), Wrappers(english).Select(s => s.Id!));

        // Worth nothing unless the headings really do differ. The statistics heading is left out
        // because the catalogue's own statistikktype is inside it; the three above it are enough.
        Assert.Equal(["Metadata", "Inklusjons- og eksklusjonskriterier", "Kildeinformasjon"],
                     BlockHeadings(norwegian).Take(3));
        Assert.Equal(["Metadata", "Inclusion and exclusion criteria", "Source information"],
                     BlockHeadings(english).Take(3));
    }

    [Fact]
    public void Sections_WhenABlockDrawsNothing_ThenNoEmptyWrapperIsLeftBehind()
    {
        // The wrapper goes INSIDE each emptiness check. Outside one it would draw a section holding
        // a heading and nothing else, which is worse than the bare heading it replaced. Both of
        // this view's suppressible blocks are taken away at once — the criteria, and all four
        // facts of the statistics, the type included since it fills a row (Fhi.Metadata-35w0p.50).
        var cut = Render(Datasamling() with
        {
            InclusionAndExclusionCriteria = null,
            StatisticsType = null,
            Frequency = null,
            CountingUnit = null,
            VariableCount = 0,
        });

        Assert.Equal([DetailSectionIds.Metadata, DetailSectionIds.Source],
                     Wrappers(cut).Select(section => section.Id!));
        Assert.All(Wrappers(cut), section => Assert.True(
            section.Children.Length > 1, $"Section '{section.Id}' holds its heading and nothing else."));
    }

    /// <summary>
    /// A datasamling the catalogue has filled in nothing for beyond what it is called. Shared with
    /// the contents tests below so both ask about the same payload.
    /// </summary>
    private static DatasamlingDetail Sparse() => new()
    {
        Id = Guid.NewGuid(),
        Code = "D",
        PreferredTerm = "D",
    };

    [Fact]
    public void Sections_WhenTheCatalogueHasFilledInNothing_ThenNeitherFactBoxIsDrawn()
    {
        // The other half of the absence rule: a missing fact reads "Ingen", but a box with every
        // fact missing is not drawn, heading included. The kildetype and identification rows used
        // to keep the source box alive by reading "Ikke oppgitt". (Fhi.Metadata-35w0p.24)
        var cut = Render(Sparse());

        Assert.Single(cut.FindAll(".munin-explorer-datasamling__main"));
        Assert.Empty(cut.FindAll($"#{DetailSectionIds.Source}"));
        Assert.Empty(cut.FindAll($"#{DetailSectionIds.Statistics}"));
    }

    [Fact]
    public void Sections_Always_ThenNoTwoOfThemShareAnId()
    {
        // Plain ids are only safe because an explorer renders at most one detail view: VariableSearch
        // picks between the three arms of one if/else, KildeSearch between two. Within a view each id
        // is written at most once, and this is what says so.
        var ids = Wrappers(Render(Datasamling())).Select(section => section.Id!).ToList();

        Assert.Equal(ids.Distinct(StringComparer.Ordinal), ids);
    }

    // ---------------------------------------------------------------------------------
    // The contents nav in the column beside them.
    // ---------------------------------------------------------------------------------

    /// <summary>Which section each entry names, in document order.</summary>
    /// <remarks>
    /// The fragment alone. In front of it every href carries this page's own path and query, which
    /// is what stops a bare <c>#id</c> resolving against a host's <c>&lt;base href&gt;</c>; that
    /// prefix says nothing about which block an entry names and is pinned in DetailTocTest.
    /// </remarks>
    private static IReadOnlyList<string> Targets(IRenderedComponent<DatasamlingView> cut) =>
        [.. cut.FindAll(".munin-explorer-page__toc a")
               .Select(link => link.GetAttribute("href")!)
               .Select(href => href[href.IndexOf('#', StringComparison.Ordinal)..])];

    /// <summary>What the nav's entries say, in document order.</summary>
    private static IReadOnlyList<string> Entries(IRenderedComponent<DatasamlingView> cut) =>
        [.. cut.FindAll(".munin-explorer-page__toc a").Select(link => link.TextContent)];

    [Fact]
    public void Contents_Always_ThenEveryEntryPointsAtASectionThatIsReallyThere()
    {
        // Read against the sections themselves rather than against a list written here: an entry
        // taken off the sections a view COULD draw is the dead anchor this nav is likeliest to
        // produce, and the two lists cannot drift apart while this compares them.
        var cut = Render(Datasamling());

        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));
        Assert.Equal(Wrappers(cut).Select(section => section.FirstElementChild!.TextContent), Entries(cut));
    }

    [Fact]
    public void Contents_WhenAnExplorerHandsTheViewNamedSections_ThenEachIsDrawnAndListedAfterTheViewsOwn()
    {
        // No explorer hands this view any yet; a host mounting it may, and off one list for both
        // halves (Fhi.Metadata-fkiz9).
        var cut = Render<DatasamlingView>(b => b
            .Add(c => c.Datasamling, Datasamling())
            .Add(c => c.NamedSections, NamedSectionsFixture.Two));

        Assert.Equal(["#" + DetailSectionIds.Statistics, "#first", "#second"], Targets(cut).TakeLast(3));
        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));
        Assert.Equal(Wrappers(cut).Select(section => section.FirstElementChild!.TextContent), Entries(cut));
        Assert.Single(Wrappers(cut).Select(section => section.FirstElementChild!.TagName).Distinct());
    }

    [Fact]
    public void Contents_WhenANamedSectionReusesAnIdOfTheViewsOwn_ThenTheViewsEmptyBlockStaysOff()
    {
        var cut = Render<DatasamlingView>(b => b
            .Add(c => c.Datasamling, Sparse())
            .Add(c => c.NamedSections,
                 [new DetailNamedSection(DetailSectionIds.Criteria, "Mine kriterier",
                                         body => body.AddContent(0, "x"))]));

        var criteria = Assert.Single(Wrappers(cut), section => section.Id == DetailSectionIds.Criteria);

        Assert.Equal("Mine kriterier", criteria.FirstElementChild!.TextContent);
    }

    [Fact]
    public void Contents_WhenABlockDrawsNothing_ThenItGetsNoEntryEither()
    {
        // The payload Sections_WhenABlockDrawsNothing uses, asked one column over: the criteria and
        // the statistics both go, so the nav is down to the two blocks that are left.
        var cut = Render(Datasamling() with
        {
            InclusionAndExclusionCriteria = null,
            StatisticsType = null,
            Frequency = null,
            CountingUnit = null,
            VariableCount = 0,
        });

        Assert.Equal(["#" + DetailSectionIds.Metadata, "#" + DetailSectionIds.Source], Targets(cut));
    }

    [Fact]
    public void Contents_WhenTheSameDatasamlingIsReadInBothLanguages_ThenOnlyTheWordsDiffer()
    {
        // THE TRAP once more, one column over from the sections: the words translate and the hrefs
        // must not, or a link one reader sends lands nowhere for the other.
        var norwegian = Render(Datasamling(), language: "no");
        var english = Render(Datasamling(), language: "en");

        Assert.Equal(Targets(norwegian), Targets(english));
        Assert.Equal(["Metadata", "Inklusjons- og eksklusjonskriterier", "Kildeinformasjon"],
                     Entries(norwegian).Take(3));
        Assert.Equal(["Metadata", "Inclusion and exclusion criteria", "Source information"],
                     Entries(english).Take(3));
    }

    [Fact]
    public void Contents_Always_ThenTheNavIsNamedInTheReadersLanguage()
    {
        // A landmark among the host page's own, so it says which navigation it is.
        Assert.Equal("Innhold",
                     Render(Datasamling(), language: "no").Find(".munin-explorer-page__toc nav").GetAttribute("aria-label"));
        Assert.Equal("Contents",
                     Render(Datasamling(), language: "en").Find(".munin-explorer-page__toc nav").GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData("full")]
    [InlineData("sparse")]
    public void Contents_WhateverTheCatalogueFilledIn_ThenEveryLinkResolvesToASectionInTheDocument(string fixture)
    {
        // The one assertion that catches a predicate in BuildToc drifting from the condition on its
        // block, which is a dead in-page link no compiler and no markup test sees.
        var cut = Render(fixture == "full" ? Datasamling() : Sparse());

        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));

        // Resolved through the DOM rather than compared as strings: `#metadata` is also the CSS
        // selector for the element it has to land on, so this is the browser's own question.
        Assert.All(Targets(cut), href => Assert.NotNull(cut.Find(href)));
    }

    [Fact]
    public void Contents_WhenTheCatalogueFilledInNothing_ThenNoBlockIsNamed()
    {
        // All four go. The source box used to survive on a kildetype row reading "Ikke oppgitt";
        // a box holding nothing at all is now no box, and so no entry. (Fhi.Metadata-35w0p.24)
        var cut = Render(Sparse());

        Assert.Single(cut.FindAll(".munin-explorer-datasamling__main"));
        Assert.Empty(Targets(cut));
    }

    [Fact]
    public void Contents_WhenTheDatasamlingIsReplacedAfterTheFirstRender_ThenTheNavIsRebuiltWithIt()
    {
        // Toc is cached and rebuilt only in OnParametersSet, so every predicate it reads has to be
        // a parameter or derived from one. They all hang off Datasamling, and this is what says so
        // if one stops doing.
        var cut = Render(Sparse());

        Assert.Empty(Targets(cut));

        cut.Render(p => p.Add(c => c.Datasamling, Datasamling()));

        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));
        Assert.Contains("#" + DetailSectionIds.Metadata, Targets(cut));
    }

    [Theory]
    [InlineData("yearly", "Statistikk (årsbasert)")]
    [InlineData("accumulated", "Statistikk (akkumulert)")]
    [InlineData(null, "Statistikk")]
    public void Contents_WhateverTheStatisticsTypeIs_ThenTheNavEntrySaysWhatTheHeadingSays(
        string? statisticsType, string expected)
    {
        // The one entry whose words are not a fixed text: the catalogue's own statistikktype sits
        // inside this heading. The nav names the block without drawing it, so this is where the two
        // could say different things with nothing failing.
        var cut = Render(Datasamling() with { StatisticsType = statisticsType });

        var heading = cut.Find($"#{DetailSectionIds.Statistics}").FirstElementChild!.TextContent;
        var entry = cut.Find($".munin-explorer-page__toc a[href$='#{DetailSectionIds.Statistics}']").TextContent;

        Assert.Equal(expected, heading);
        Assert.Equal(heading, entry);
    }

    // ---------------------------------------------------------------------------------
    // Inclusion and exclusion criteria.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Criteria_WhenTheCatalogueHasThem_ThenTheyAreProseRatherThanAFactRow()
    {
        // Prose, and often several paragraphs of it — the answer to the first question a researcher
        // asks about a datasamling. A fact row is the one place it cannot be read.
        var cut = Render(Datasamling());

        Assert.StartsWith("Alle pasienter som er 18 år eller eldre",
                          cut.Find(".munin-explorer-datasamling__criteria").TextContent.Trim(),
                          StringComparison.Ordinal);

        Assert.Contains("Inklusjons- og eksklusjonskriterier", BlockHeadings(cut));
        Assert.DoesNotContain("Inklusjons- og eksklusjonskriterier", Labels(SourceInformation(cut)));
    }

    [Fact]
    public void Criteria_WhenAuthoredWithBrAndALink_ThenTheSectionRendersThemRatherThanTheSource()
    {
        // The column-backed copy of InklusjonsOgEksklusjonskriterier, which the metadata rows'
        // key list cannot reach (Fhi.Metadata-x0etk).
        var cut = Render(Datasamling() with
        {
            InclusionAndExclusionCriteria = "Alle over 18 år.<br>Se [veilederen](https://example.org/veileder).",
        });

        var criteria = cut.Find(".munin-explorer-datasamling__criteria");

        Assert.Single(criteria.QuerySelectorAll("br"));
        Assert.Equal("https://example.org/veileder", Assert.Single(criteria.QuerySelectorAll("a")).GetAttribute("href"));
        Assert.DoesNotContain("<br>", criteria.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Criteria_WhenTheCatalogueHasNone_ThenNoHeadingPromisesAny()
    {
        // A third of the datasamlinger measured have none, so this is the ordinary case rather than
        // the edge one.
        var cut = Render(Datasamling() with { InclusionAndExclusionCriteria = null });

        Assert.DoesNotContain("Inklusjons- og eksklusjonskriterier", BlockHeadings(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-datasamling__criteria"));
    }

    // ---------------------------------------------------------------------------------
    // The fact boxes.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void SourceInformation_Always_ThenItIsTheInheritedValuesRatherThanTheOwnBlankOnes()
    {
        // The inheritance rule, put to the payload it was written for: this datasamling sets none
        // of lovverk, dataansvarlig, databehandler or identification level itself. Drawing its own
        // values would report "Ikke oppgitt" four times for a datasamling whose controller is
        // perfectly well known one level up.
        var cut = Render(Datasamling());
        var facts = SourceInformation(cut);

        Assert.Equal(
            ["Kilde", "Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin",
             "Opprettet i Munin"],
            Labels(facts));

        Assert.Equal("Als registeret", Value(facts, "Kilde"));
        Assert.Equal("Nasjonalt medisinsk kvalitetsregister", Value(facts, "Type datakilde"));
        Assert.Equal("St. Olavs hospital HF", Value(facts, "Dataansvarlig"));
        Assert.Equal("Indirekte identifiserbar", Value(facts, "Grad av personidentifikasjon"));

        // An open end says so rather than sitting blank or guessing a date.
        Assert.Equal("1. januar 2010 – Pågående", Value(facts, "Gyldighet"));
    }

    [Fact]
    public void SourceInformation_WhenNothingWasInheritedEither_ThenEachRowSaysSoMuted()
    {
        // A dt with an empty dd reads as a value that failed to draw, and a row left out reads as a
        // field that does not apply. "Ingen", muted, says the catalogue was asked and holds nothing.
        var cut = Render(Datasamling() with
        {
            EffectiveLegalBasis = null,
            EffectiveDataController = "",
            EffectiveDataProcessor = "   ",
            EffectiveValidFrom = null,
            EffectiveValidTo = null,
        });

        var box = SourceInformation(cut);

        Assert.Equal(
            ["Kilde", "Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin", "Opprettet i Munin"],
            Labels(box));
        Assert.All(["Lovverk", "Dataansvarlig", "Databehandler", "Gyldighet"], label => AssertAbsent(box, label));
        Assert.Equal("Nasjonalt medisinsk kvalitetsregister", Value(box, "Type datakilde"));
    }

    [Fact]
    public void SourceInformation_WhenTheOwningKildeHasNoKildetype_ThenTheRowSaysSoRatherThanStandingBlank()
    {
        // effectiveKildetype is the owning kilde's, and the API sends null when it has none. The
        // compiler could not flag this reading site, because the helper it goes through has always
        // taken a null — a blank row is all the change would have shown. (Fhi.Metadata-l9l2n.61)
        var cut = Render(Datasamling() with { EffectiveKildetype = null });

        AssertAbsent(SourceInformation(cut), "Type datakilde");
    }

    [Fact]
    public void SourceInformation_WhenTheSurfaceAboveSuppliedAKildeAddress_ThenTheParentIsALink()
    {
        // The return path out of a collection. parentKildeId is on the payload for exactly this, so
        // the link costs no second request — and it is the id that is handed out, not the name, so
        // a host builds the address rather than searching for one. (Fhi.Metadata-35w0p.50)
        var datasamling = Datasamling();

        var cut = Render(datasamling, kildeHref: id => $"/kilder?kilde={id}");

        var link = Assert.Single(Row(SourceInformation(cut), "Kilde").QuerySelectorAll("a"));

        Assert.Equal($"/kilder?kilde={datasamling.ParentKildeId}", link.GetAttribute("href"));
        Assert.Equal("Als registeret", link.TextContent);
    }

    [Fact]
    public void SourceInformation_WhenNoKildeAddressWasSupplied_ThenTheParentIsPlainTextRatherThanADeadLink()
    {
        // The state every mount outside the kildeutforsker is in: this package has no router, so
        // with nothing handed down the name is the fact and an <a href=""> would be a control that
        // goes nowhere. Both states, because only the pair says the target is the host's.
        var row = Row(SourceInformation(Render(Datasamling())), "Kilde");

        Assert.Empty(row.QuerySelectorAll("a"));
        Assert.Equal("Als registeret", row.QuerySelector("dd")!.TextContent);
    }

    [Fact]
    public void SourceInformation_WhenTheCatalogueRecordedWhenItWasCreated_ThenTheRowSaysSoInMuninsWords()
    {
        // "Opprettet i Munin" rather than "Opprettet": the bare word is already spent on the
        // founding year the import file states, which Kelda heads "Opprettet", and the two have
        // been confused before. The twin of Sist oppdatert i Munin, formatted the same way.
        var facts = SourceInformation(Render(Datasamling()));

        Assert.Equal("19. mai 2026", Value(facts, "Opprettet i Munin"));
        Assert.Equal("19 May 2026",
                     Value(SourceInformation(Render(Datasamling(), language: "en")), "Created in Munin"));
    }

    [Fact]
    public void SourceInformation_WhenThePayloadCarriesNoCreatedTimestamp_ThenTheRowSaysSoRatherThanYearOne()
    {
        // The same fallback sistOppdatert has, and worth its own test because the two are read off
        // different contract fields: an absent opprettet drew "1. januar 0001" before
        // Fhi.Metadata-se0by, and a blank row reads as a value that failed to draw.
        AssertAbsent(SourceInformation(Render(Datasamling() with { Created = null })), "Opprettet i Munin");
    }

    [Fact]
    public void SourceInformation_WhenThePayloadCarriesNoTimestamp_ThenTheRowSaysNoneRatherThanYearOne()
    {
        // An absent sistOppdatert reads as null (Fhi.Metadata-se0by) and drew "1. januar 0001"
        // before that. The kilde view had the same line, and the kilder table's Importert column
        // the same shape. (Fhi.Metadata-6r6rf)
        var cut = Render(Datasamling() with { LastUpdated = default, Created = default });

        // Both timestamps at once, because they are the same shape and the same fallback.
        var box = SourceInformation(cut);

        Assert.Equal(
            ["Kilde", "Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin", "Opprettet i Munin"],
            Labels(box));
        AssertAbsent(box, "Sist oppdatert i Munin");
        AssertAbsent(box, "Opprettet i Munin");
    }

    // ---------------------------------------------------------------------------------
    // The statistics block — the one Runa has and the flat list had no equivalent of.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Statistics_Always_ThenTheHeadingNamesTheKindAndTheRowsAreWhatTheCatalogueCounts()
    {
        // The kind matters to a reader deciding what the numbers mean, which is why it heads the
        // block rather than filling a row — the same wording a variable's statistics table gets,
        // off the same field.
        var cut = Render(Datasamling());

        Assert.Contains("Statistikk (årsbasert)", BlockHeadings(cut));
        Assert.Equal(["Statistikktype", "Frekvens", "Telleenhet", "Antall variabler"],
                     Labels(Box(cut, "Statistikk (årsbasert)")));
        Assert.Equal("99", Value(Box(cut, "Statistikk (årsbasert)"), "Antall variabler"));
    }

    [Fact]
    public void Statistics_WhenTheCatalogueNamesTheKind_ThenARowCarriesItAndNotOnlyTheHeading()
    {
        // The discriminating half of the pair below, and the one that fails on a page where
        // statistikkType is fetched, deserialised and thrown away: the heading's parenthesis is
        // chrome that makes the section findable, and the row is where the fact is stated.
        var cut = Render(Datasamling());

        Assert.Equal("Årsbasert", Value(Box(cut, "Statistikk (årsbasert)"), "Statistikktype"));

        // The catalogue's token resolved to this package's word, which is what the heading spells
        // too — one resolution, so a row and the heading over it cannot come out in two words.
        var unknown = Render(Datasamling() with { StatisticsType = "kvartalsvis" });

        Assert.Equal("kvartalsvis", Value(Box(unknown, "Statistikk (kvartalsvis)"), "Statistikktype"));
    }

    [Fact]
    public void Statistics_WhenTheCatalogueNamesNoKind_ThenTheRowSaysSoMuted()
    {
        // A blank row reads as a field the catalogue failed to draw, and a row left out as a field
        // that does not apply. (Fhi.Metadata-35w0p.24)
        var cut = Render(Datasamling() with { StatisticsType = null });

        AssertAbsent(Box(cut, "Statistikk"), "Statistikktype");
    }

    [Fact]
    public void Statistics_WhenTelleenhetIsFilledIn_ThenItIsARowRatherThanBeingLeftToTheMetadata()
    {
        // Twenty of the 85 datasamlinger measured carry one, and it says what a row of the data
        // actually is — the Kreftregister's is "Tilfelle" rather than a person.
        var cut = Render(Datasamling() with { CountingUnit = "Tilfelle" });

        Assert.Equal("Tilfelle", Value(Box(cut, "Statistikk (årsbasert)"), "Telleenhet"));
    }

    [Fact]
    public void Statistics_WhenTheDatasamlingCountsNothingAtAll_ThenNoEmptyBlockIsDrawn()
    {
        // The bead's third acceptance criterion, and not a hypothetical shape: sixteen of the 85
        // datasamlinger measured hold no variables, and frekvens is empty on every one of them. A
        // heading over an empty list is a section that promises numbers the catalogue does not have.
        var cut = Render(Datasamling() with
        {
            StatisticsType = null,
            Frequency = null,
            CountingUnit = "",
            VariableCount = 0,
        });

        // The whole list rather than the two headings this is about: it used to be scoped to the
        // aside, where "nothing else was drawn there" came free, and filtering the headings down to
        // what it compares against would give that half away.
        Assert.Equal(["Metadata", "Inklusjons- og eksklusjonskriterier", "Kildeinformasjon"],
                     BlockHeadings(cut));
    }

    [Fact]
    public void Statistics_WhenOnlyTheTypeIsKnown_ThenItIsTheOneValueAndTheRestSayNone()
    {
        // The degenerate case, kept rather than suppressed: the heading's parenthesis is what makes
        // the section findable and the row is where the fact is stated, so one echoing the other is
        // still the record. Heading and list are answered by one question, so they cannot disagree.
        var cut = Render(Datasamling() with { Frequency = null, CountingUnit = null, VariableCount = 0 });

        Assert.Contains("Statistikk (årsbasert)", BlockHeadings(cut));

        var box = Box(cut, "Statistikk (årsbasert)");

        Assert.Equal("Årsbasert", Value(box, "Statistikktype"));
        Assert.All(["Frekvens", "Telleenhet", "Antall variabler"], label => AssertAbsent(box, label));
    }

    // ---------------------------------------------------------------------------------
    // Placement: the parameters that let one view sit in two places.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Headings_WhenTheViewSitsDeeperInThePage_ThenEveryLevelUnderTheNameFollowsIt()
    {
        // Heading order is how a screen reader user navigates a page. A view that always emitted
        // h2 would break the outline wherever it opens inside a result row.
        var cut = Render(Datasamling(), headingLevel: 4);

        Assert.Equal("H4", cut.Find(".munin-explorer-datasamling__header h4").TagName);
        Assert.Equal("H5", cut.Find($"#{DetailSectionIds.Source} h5").TagName);
        Assert.Equal("H6", cut.Find(".munin-explorer-group").TagName);
    }

    [Fact]
    public void Headings_WhenTheViewSitsAsDeepAsHeadingsGo_ThenTheLevelsStopAtSixRatherThanRunningPastIt()
    {
        // There is no h7. Left to run, the group heading would emit one and every browser would
        // parse it as unknown markup rather than as a heading at all.
        var cut = Render(Datasamling(), headingLevel: 6);

        Assert.Equal("H6", cut.Find(".munin-explorer-datasamling__header h6").TagName);
        Assert.Equal("H6", cut.Find(".munin-explorer-group").TagName);
    }

    [Fact]
    public void HeadingId_WhenTheHostNamesARegionByTheName_ThenTheIdIsOnTheNameAndNowhereElse()
    {
        // The drill-in is a landmark, and a landmark is only useful if a screen reader can say
        // which datasamling it just entered.
        var cut = Render(Datasamling(), headingId: "panel-heading");

        Assert.Equal("Inklusjon", cut.Find("#panel-heading").TextContent);
        Assert.Single(cut.FindAll("#panel-heading"));
    }

    [Fact]
    public void HeadingId_WhenTheHostNamesNothing_ThenNoEmptyIdIsEmitted()
    {
        // An id="" is a duplicate the moment a second view is on the page, and an aria-labelledby
        // pointing at it resolves to whichever came first. The section ids below are the view's own
        // and are always written.
        var ids = Render(Datasamling()).FindAll("[id]").Select(e => e.Id!).ToList();

        Assert.DoesNotContain(ids, id => id.Length == 0);

        // The two the detail chassis writes are matched by stem rather than listed: each is
        // finished with a fresh per-instance discriminator, which is what keeps two mounts on one
        // host page from sharing a sticky bar.
        Assert.Equal(2, ids.Count(DetailPage.IsChassisId));
        Assert.All(ids.Where(id => !DetailPage.IsChassisId(id)),
                   id => Assert.Contains(id, DetailSectionIdsUnderTest));
    }

    /// <summary>Every id this view is allowed to write when the host names none.</summary>
    private static readonly string[] DetailSectionIdsUnderTest =
    [
        DetailSectionIds.Metadata,
        DetailSectionIds.Criteria,
        DetailSectionIds.Source,
        DetailSectionIds.Statistics,
    ];

    [Fact]
    public void Sections_WhenAHostPassesThem_ThenTheyComeLastAfterTheNamedSections()
    {
        // A host's markup is an addition to the page it embedded, so it follows every section the
        // view draws, the named ones included.
        var cut = Render<DatasamlingView>(b => b
            .Add(c => c.Datasamling, Datasamling())
            .Add(c => c.NamedSections, NamedSectionsFixture.Two)
            .Add(c => c.Sections, ExplorerSections));

        var ids = cut.Find(".munin-explorer-datasamling__main").Children.Select(e => e.Id ?? "").ToArray();

        Assert.Equal([DetailSectionIds.Statistics, "first", "second", "explorer-sections"], ids[^4..]);
    }

    [Fact]
    public void Sections_WhenNoExplorerPassesAny_ThenNothingIsDrawnWhereTheyWouldHaveGone()
    {
        // Runa passes none, so an empty wrapper drawn for the slot would be a gap under every
        // datasamling it opens.
        var cut = Render(Datasamling());

        Assert.Empty(cut.FindAll("#explorer-sections"));
    }

    // ---------------------------------------------------------------------------------
    // Language.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Language_WhenTheReaderIsEnglish_ThenOurOwnWordsFollowThemAndTheCataloguesStayNorwegian()
    {
        // The split this package makes everywhere: our labels and our vocabularies follow the
        // reader, and Munin's own prose is stored once, in Norwegian, and marked as such so an
        // English page's synthesiser does not read it aloud as English.
        var cut = Render(Datasamling(), language: "en");
        var facts = SourceInformation(cut);

        Assert.Equal(
            ["Source", "Type of data source", "Legal basis", "Data controller", "Data processor",
             "Level of personal identification", "Validity", "Last updated in Munin",
             "Created in Munin"],
            Labels(facts));

        Assert.Equal("National medical quality registry", Value(facts, "Type of data source"));
        Assert.Equal("1 January 2010 – Ongoing", Value(facts, "Validity"));

        Assert.Equal("no", cut.Find(".munin-explorer-datasamling__description").GetAttribute("lang"));
        Assert.Equal("no", cut.Find(".munin-explorer-datasamling__criteria").GetAttribute("lang"));
    }

    [Fact]
    public void Description_WhenTheCatalogueAuthoredMarkdown_ThenTheIngressRendersItAsElements()
    {
        // Datasamling beskrivelser carry the same authored markdown as the kilde's own
        // (FHIDev/Munin#5385); this pins that this view went through the same renderer.
        var cut = Render(Datasamling() with
        {
            Description = "Spørreskjema.<br>Se [UiT](https://uit.no/research/tromsostudy).",
        });

        var ingress = cut.Find(".munin-explorer-datasamling__description");

        Assert.Single(ingress.QuerySelectorAll("br"));
        Assert.Equal("https://uit.no/research/tromsostudy",
                     Assert.Single(ingress.QuerySelectorAll("a")).GetAttribute("href"));
    }

    [Fact]
    public void FactLists_Always_ThenTheyWearTheGridInTheOneColumn()
    {
        // The markup half — the stylesheet half is asserted in KildeViewTest. Fact lists that
        // stopped wearing this class would leave the host's rule matching nothing and say so
        // nowhere. (Fhi.Metadata-hi0po)
        var main = Render(Datasamling()).Find(".munin-explorer-datasamling__main");

        Assert.Empty(Render(Datasamling()).FindAll("aside"));
        Assert.NotEmpty(main.QuerySelectorAll("dl.munin-explorer-page__fields"));
    }

    [Fact]
    public void Heading_WhenTheCatalogueLeftTheNameEmpty_ThenTheCodeStandsInAndIsNotDrawnTwice()
    {
        // The same shape as the kilde view's heading and a different contract property, which is
        // why it is a test of its own: a fix applied to one of them compiles and passes with the
        // other left behind. (Fhi.Metadata-w13lk)
        var cut = Render(Datasamling() with { PreferredTerm = "" });

        var heading = cut.Find("h2");

        Assert.NotEqual("", heading.TextContent.Trim());
        Assert.Equal(Datasamling().Code, heading.TextContent.Trim());
        Assert.Empty(cut.FindAll("p.munin-explorer-datasamling__identifiers"));

        // The sticky bar carries its own copy of the same rule, reading its own contract property.
        // The name is asserted beside it: a bar that stopped rendering would pass the emptiness.
        Assert.Equal(
            Datasamling().Code,
            cut.Find(".munin-explorer-page__stuckbar-name > span").TextContent.Trim());
        Assert.Empty(cut.FindAll(".munin-explorer-page__stuckbar small"));
    }

    [Fact]
    public void Stuckbar_Always_ThenItSaysWhatTheHeadingSays()
    {
        // The second of three hand-written copies of the rule, over a second contract property: a
        // fix applied to one of them compiles and passes with the others left behind.
        var cut = Render(Datasamling(), language: "en");

        var heading = cut.Find("h2");
        var name = cut.Find(".munin-explorer-page__stuckbar-name > span");

        Assert.Equal(heading.TextContent.Trim(), name.TextContent.Trim());
        Assert.Equal(heading.GetAttribute("lang"), name.GetAttribute("lang"));
        Assert.Equal(Datasamling().Code, cut.Find(".munin-explorer-page__stuckbar small").TextContent.Trim());
    }

    [Fact]
    public void Heading_WhenTheCodeStandsInForTheName_ThenItIsNotMarkedAsNorwegian()
    {
        // A code is not Norwegian prose, and the identifier line that normally carries it is not
        // marked either. The named case is asserted beside it, so the marker cannot be dropped
        // wholesale and still pass.
        Assert.Null(Render(Datasamling() with { PreferredTerm = "" }, language: "en")
            .Find("h2").GetAttribute("lang"));

        Assert.Equal("no", Render(Datasamling(), language: "en").Find("h2").GetAttribute("lang"));
    }
}
