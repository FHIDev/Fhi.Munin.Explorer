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
/// the sidebar of who owns it and how much of it there is.
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
    /// One sidebar box, found by the heading over it rather than by its position.
    /// </summary>
    /// <remarks>
    /// The statistics box is drawn only when it has a row, so the boxes slide up under headings
    /// that move with them and a position would hand back the wrong one without being able to say
    /// it had.
    /// </remarks>
    private static IElement Box(IRenderedComponent<DatasamlingView> cut, string heading)
    {
        var aside = cut.Find(".munin-explorer-datasamling__aside");

        var found = aside.Children.FirstOrDefault(e => e.TextContent == heading)
                    ?? throw new InvalidOperationException(
                        $"No '{heading}' heading in the sidebar, only: "
                        + $"{string.Join(", ", aside.Children.Select(e => e.TextContent))}.");

        return found.NextElementSibling is { TagName: "DL" } box
            ? box
            : throw new InvalidOperationException(
                $"The '{heading}' heading is followed by {found.NextElementSibling?.TagName ?? "nothing"} "
                + "rather than by its own box, so that box drew no facts at all.");
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
        [.. cut.FindAll(".munin-explorer-datasamling__body .headline-s").Select(e => e.TextContent)];

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
        // The exact list, for the reason the kilde view's version of it is exact: a ninth name here
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
            "munin-explorer-datasamling__aside",
            "munin-explorer-datasamling__body",
            "munin-explorer-datasamling__criteria",
            "munin-explorer-datasamling__description",
            "munin-explorer-datasamling__header",
            "munin-explorer-datasamling__identifiers",
            "munin-explorer-datasamling__main",
            "munin-explorer-group",                   // shared with the kilde and variable views
        ], invented);
    }

    // ---------------------------------------------------------------------------------
    // The name block.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Render_WhenNoDatasamlingHasArrived_ThenNothingIsDrawnAtAll()
    {
        // The parameter is EditorRequired but the caller sets it from a fetch, so null is the state
        // between opening the view and the payload landing. An empty shell would be a header rule
        // and a sidebar box drawn around nothing.
        Assert.Empty(Render(datasamling: null).Markup.Trim());
    }

    [Fact]
    public void Identifiers_Always_ThenTheCodeStandsAloneUnderTheName()
    {
        // The code, and not the kortNavn beside it the way the kilde view puts a source's: on a
        // datasamling that field holds the owning kilde's abbreviation — "ALS" on every one of the
        // ALS register's — so it names the kilde rather than the datasamling, and the sidebar
        // already says which kilde this is.
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
    public void Description_WhenTheCatalogueHasOne_ThenItIsProseUnderTheNameRatherThanARowInTheSidebar()
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
    // Inclusion and exclusion criteria.
    // ---------------------------------------------------------------------------------

    [Fact]
    public void Criteria_WhenTheCatalogueHasThem_ThenTheySitInTheMainColumnRatherThanInTheSidebar()
    {
        // Prose, and often several paragraphs of it — the answer to the first question a researcher
        // asks about a datasamling. A sidebar row is the one place it cannot be read.
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
    // The sidebar.
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
    public void SourceInformation_WhenThePayloadCarriesNoTimestamp_ThenTheRowIsAbsentRatherThanYearOne()
    {
        // DatasamlingDetail.LastUpdated is not nullable, so an absent sistOppdatert leaves it at
        // default and the field drew "1. januar 1". The kilde view had the same line, and the
        // kilder table's Importert column the same shape. (Fhi.Metadata-6r6rf)
        var cut = Render(Datasamling() with { LastUpdated = default });

        Assert.DoesNotContain("Sist oppdatert i Munin", Labels(SourceInformation(cut)));
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

        Assert.DoesNotContain("Statistikk", BlockHeadings(cut));
        Assert.Equal(["Kildeinformasjon"],
                     cut.FindAll(".munin-explorer-datasamling__aside .headline-s").Select(e => e.TextContent));
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
        Assert.Equal("H5", cut.Find(".munin-explorer-datasamling__aside h5").TagName);
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
        // pointing at it resolves to whichever came first.
        Assert.Empty(Render(Datasamling()).FindAll("[id]"));
    }

    [Fact]
    public void Sections_WhenAnExplorerPassesThem_ThenTheyComeLastInTheMainColumnRatherThanInTheSidebar()
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
}
