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
public class DatasamlingViewTest : BunitContext
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
        RenderFragment? sections = null) =>
        Render<DatasamlingView>(b =>
        {
            b.Add(c => c.Datasamling, datasamling)
             .Add(c => c.Language, language)
             .Add(c => c.HeadingLevel, headingLevel)
             .Add(c => c.HeadingId, headingId);

            // Left unset rather than set to null when no explorer passes any, which is the state a
            // host actually renders this view in.
            if (sections is not null)
            {
                b.Add(c => c.Sections, sections);
            }
        });

    /// <summary>Markup an explorer might hang after the metadata, carrying no class of its own.</summary>
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
    /// A blank value draws no row, so an index names a row that an upstream field silently moves.
    /// Asking by label makes the failure say which row went missing.
    /// </remarks>
    private static string Value(IElement list, string label) =>
        list.QuerySelectorAll("div").FirstOrDefault(row => row.QuerySelector("dt")?.TextContent == label)
            ?.QuerySelector("dd")?.TextContent
        ?? throw new InvalidOperationException(
            $"No '{label}' row in this box, only: {string.Join(", ", Labels(list))}.");

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
            "munin-explorer-page__main",
            // The wrapper each block below the name sits in, so the contents nav can anchor on it.
            "munin-explorer-page__section",
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
        // this view's suppressible blocks are taken away at once — the criteria and the
        // statistics.
        var cut = Render(Datasamling() with
        {
            InclusionAndExclusionCriteria = null,
            Frequency = null,
            CountingUnit = null,
            VariableCount = 0,
        });

        Assert.Equal([DetailSectionIds.Metadata, DetailSectionIds.Source],
                     Wrappers(cut).Select(section => section.Id!));
        Assert.All(Wrappers(cut), section => Assert.True(
            section.Children.Length > 1, $"Section '{section.Id}' holds its heading and nothing else."));
    }

    [Fact]
    public void Sections_WhenTheCatalogueHasFilledInNothing_ThenTheSourceBoxStillDrawsARow()
    {
        // Why the source box survives its emptiness check on a payload this bare while the
        // statistics box beside it does not: the kildetype and identification rows both answer
        // "Ikke oppgitt" rather than nothing, and Statistics has no row with a fallback.
        var cut = Render(new DatasamlingDetail { Id = Guid.NewGuid(), Code = "D", PreferredTerm = "D" });

        // The whole list rather than the first row, and by label: a row added above this one is a
        // new row appearing, not the fallback going, and the two should not fail alike.
        var box = SourceInformation(cut);

        Assert.Equal(["Type datakilde", "Grad av personidentifikasjon"], Labels(box));
        Assert.Equal("Ikke oppgitt", Value(box, "Type datakilde"));
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

    /// <summary>Where the nav's entries point, in document order.</summary>
    private static IReadOnlyList<string> Targets(IRenderedComponent<DatasamlingView> cut) =>
        [.. cut.FindAll(".munin-explorer-page__toc a").Select(link => link.GetAttribute("href")!)];

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
    public void Contents_WhenABlockDrawsNothing_ThenItGetsNoEntryEither()
    {
        // The payload Sections_WhenABlockDrawsNothing uses, asked one column over: the criteria and
        // the statistics both go, so the nav is down to the two blocks that are left.
        var cut = Render(Datasamling() with
        {
            InclusionAndExclusionCriteria = null,
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

    /// <summary>A datasamling the catalogue has filled in nothing for beyond what it is called.</summary>
    private static DatasamlingDetail Sparse() => new()
    {
        Id = Guid.NewGuid(),
        Code = "D",
        PreferredTerm = "D",
    };

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
    public void Contents_WhenTheCatalogueFilledInNothing_ThenOnlyTheSourceBoxIsNamed()
    {
        // Three of the four go. The source box survives because its kildetype row falls back to
        // "Ikke oppgitt", which is a row and therefore content — the same reason the variable and
        // kilde views keep theirs on a payload this bare.
        Assert.Equal(["#" + DetailSectionIds.Source], Targets(Render(Sparse())));
    }

    [Fact]
    public void Contents_WhenTheDatasamlingIsReplacedAfterTheFirstRender_ThenTheNavIsRebuiltWithIt()
    {
        // Toc is cached and rebuilt only in OnParametersSet, so every predicate it reads has to be
        // a parameter or derived from one. They all hang off Datasamling, and this is what says so
        // if one stops doing.
        var cut = Render(Sparse());

        Assert.Equal(["#" + DetailSectionIds.Source], Targets(cut));

        cut.Render(p => p.Add(c => c.Datasamling, Datasamling()));

        Assert.Equal(Wrappers(cut).Select(section => "#" + section.Id), Targets(cut));
        Assert.Contains("#" + DetailSectionIds.Metadata, Targets(cut));
    }

    [Theory]
    [InlineData("yearly", "Statistikk (Årsbasert)")]
    [InlineData("accumulated", "Statistikk (Akkumulert)")]
    [InlineData(null, "Statistikk")]
    public void Contents_WhateverTheStatisticsTypeIs_ThenTheNavEntrySaysWhatTheHeadingSays(
        string? statisticsType, string expected)
    {
        // The one entry whose words are not a fixed text: the catalogue's own statistikktype sits
        // inside this heading. The nav names the block without drawing it, so this is where the two
        // could say different things with nothing failing.
        var cut = Render(Datasamling() with { StatisticsType = statisticsType });

        var heading = cut.Find($"#{DetailSectionIds.Statistics}").FirstElementChild!.TextContent;
        var entry = cut.Find($".munin-explorer-page__toc a[href='#{DetailSectionIds.Statistics}']").TextContent;

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
             "Grad av personidentifikasjon", "Gyldighet", "Sist oppdatert i Munin"],
            Labels(facts));

        Assert.Equal("Als registeret", Value(facts, "Kilde"));
        Assert.Equal("Nasjonalt medisinsk kvalitetsregister", Value(facts, "Type datakilde"));
        Assert.Equal("St. Olavs hospital HF", Value(facts, "Dataansvarlig"));
        Assert.Equal("Indirekte identifiserbar", Value(facts, "Grad av personidentifikasjon"));

        // An open end says so rather than sitting blank or guessing a date.
        Assert.Equal("1. januar 2010 – Pågående", Value(facts, "Gyldighet"));
    }

    [Fact]
    public void SourceInformation_WhenNothingWasInheritedEither_ThenNoBlankRowIsDrawn()
    {
        // A dt with an empty dd reads as a value that failed to draw. The two that stay are the two
        // this package writes itself — a kildetype and an identification level always resolve to a
        // word, "Ikke oppgitt" included.
        var cut = Render(Datasamling() with
        {
            EffectiveLegalBasis = null,
            EffectiveDataController = "",
            EffectiveDataProcessor = "   ",
            EffectiveValidFrom = null,
            EffectiveValidTo = null,
        });

        Assert.Equal(
            ["Kilde", "Type datakilde", "Grad av personidentifikasjon", "Sist oppdatert i Munin"],
            Labels(SourceInformation(cut)));
    }

    [Fact]
    public void SourceInformation_WhenTheOwningKildeHasNoKildetype_ThenTheRowSaysSoRatherThanStandingBlank()
    {
        // effectiveKildetype is the owning kilde's, and the API sends null when it has none. The
        // compiler could not flag this reading site, because the helper it goes through has always
        // taken a null — a blank row is all the change would have shown. (Fhi.Metadata-l9l2n.61)
        var cut = Render(Datasamling() with { EffectiveKildetype = null });

        Assert.Equal("Ikke oppgitt", Value(SourceInformation(cut), "Type datakilde"));
    }

    [Fact]
    public void SourceInformation_WhenThePayloadCarriesNoTimestamp_ThenTheRowIsAbsentRatherThanYearOne()
    {
        // An absent sistOppdatert reads as null (Fhi.Metadata-se0by) and drew "1. januar 0001"
        // before that. The kilde view had the same line, and the kilder table's Importert column
        // the same shape. (Fhi.Metadata-6r6rf)
        var cut = Render(Datasamling() with { LastUpdated = default });

        // The whole list, for the reason the kilde view test gives: this row is last, so dropping
        // it and everything after it would pass an assertion that only asks for its absence.
        Assert.Equal(
            ["Kilde", "Type datakilde", "Lovverk", "Dataansvarlig", "Databehandler",
             "Grad av personidentifikasjon", "Gyldighet"],
            Labels(SourceInformation(cut)));
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

        Assert.Contains("Statistikk (Årsbasert)", BlockHeadings(cut));
        Assert.Equal(["Antall variabler"], Labels(Box(cut, "Statistikk (Årsbasert)")));
        Assert.Equal("99", Value(Box(cut, "Statistikk (Årsbasert)"), "Antall variabler"));
    }

    [Fact]
    public void Statistics_WhenTelleenhetIsFilledIn_ThenItIsARowRatherThanBeingLeftToTheMetadata()
    {
        // Twenty of the 85 datasamlinger measured carry one, and it says what a row of the data
        // actually is — the Kreftregister's is "Tilfelle" rather than a person.
        var cut = Render(Datasamling() with { CountingUnit = "Tilfelle" });

        Assert.Equal(["Telleenhet", "Antall variabler"], Labels(Box(cut, "Statistikk (Årsbasert)")));
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
    public void Statistics_WhenOnlyTheTypeIsKnown_ThenTheHeadingDoesNotStandOverAnEmptyList()
    {
        // The type alone is not a number. Heading and list are answered by one question so they
        // cannot disagree, which is the half that is easy to get wrong.
        var cut = Render(Datasamling() with { Frequency = null, CountingUnit = null, VariableCount = 0 });

        Assert.DoesNotContain("Statistikk (Årsbasert)", BlockHeadings(cut));
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
        Assert.All(ids, id => Assert.Contains(id, DetailSectionIdsUnderTest));
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
    public void Sections_WhenAnExplorerPassesThem_ThenTheyComeLastAfterTheViewsOwnBlocks()
    {
        // The slot is the reason this is a core with composition points rather than a view with a
        // flag per explorer. Nothing here learns which explorer is calling.
        var cut = Render(Datasamling(), sections: ExplorerSections);

        var main = cut.Find(".munin-explorer-datasamling__main");

        Assert.Equal("explorer-sections", main.LastElementChild!.Id);
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
             "Level of personal identification", "Validity", "Last updated in Munin"],
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
        Assert.NotEmpty(main.QuerySelectorAll("dl.munin-explorer-meta__grid"));
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
