using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The kilde page's section order is the payload's, one pass over both kinds of section.
/// </summary>
/// <remarks>
/// <para>
/// The view used to draw four headings of its own: every property group under one "Metadata", then
/// Datasamlinger, Kildeinformasjon and Statistikk in that fixed run. The mockup has eight sections
/// with the datasamlinger BETWEEN two property ones, which no view ordering the two kinds
/// separately can reproduce, and every later reordering would have been a release of this package
/// rather than an edit a curator makes (Fhi.Metadata-35w0p.22).
/// </para>
/// <para>
/// Two sections of that old run are named by no mockup and reserved by no seed: Kildeinformasjon
/// and Statistikk. They are PLACED rather than deleted — they carry a key, they draw at the end of
/// the page until a seed names one, and the count below is what holds that honest. A kildedetalj
/// with fewer facts on it than the page it replaces is the failure this whole change is most
/// likely to produce, and it is the one a reader would notice last, because the page gets longer.
/// </para>
/// </remarks>
public class KildeLayoutTest : ExplorerTestContext
{
    public KildeLayoutTest() => Services.AddSingleton<IMuninExplorerClient>(new HierarchyClient());

    /// <summary>The kilde view fetches its tree on its own; an empty one is enough to render.</summary>
    private sealed class HierarchyClient : EmptyMuninExplorerClient
    {
        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new() { KildeId = id });
    }

    /// <summary>Tromsø, whose capture predates the sections collection and so carries none.</summary>
    /// <remarks>
    /// The before-state of every count here, and the API this package still has to render for: the
    /// field reached Munin on 2026-09-16 and the capture is older. Re-taking it is a devbox job —
    /// see <see cref="FixtureDriftTest"/> — so the seeded payload below is assembled rather than
    /// captured, and says where each of its numbers comes from.
    /// </remarks>
    private static KildeDetail Unplaced() =>
        JsonSerializer.Deserialize<KildeDetail>(
            TestData.Read("kilde-med-delkilder.json"), MuninExplorerClient.Json)
        ?? throw new InvalidOperationException("kilde-med-delkilder.json no longer reads as a KildeDetail.");

    private static SectionPlacement Section(string key, string no, string en, int? order, bool builtIn = false) =>
        new()
        {
            Key = key,
            SortOrder = order,
            IsBuiltIn = builtIn,
            Translations = new Dictionary<string, string> { ["no"] = no, ["en"] = en },
        };

    /// <summary>
    /// KildeDetalj as Munin's own seed places it: the mockup's bands, with the datasamlinger
    /// filling the gap 0013 left between innhold and tilgang-og-ansvar.
    /// </summary>
    /// <remarks>
    /// Transcribed from <c>0013_SeedPropertyPlacements.sql</c> and
    /// <c>0014_SeedBuiltInSectionPlacements.sql</c>, read on Munin <c>main</c> 2026-09-18. The
    /// catch-all's own band is 9000 + rank, so it is last whatever sections a page has.
    /// <para>
    /// Seven sections where the mockup draws eight, and the missing one is deliberate on Munin's
    /// side rather than a gap here: 0013 seeds no <c>formaal</c> section and places the mockup's
    /// Formål rows — <c>Formaal</c> and <c>FormaalFlerspraklig</c>, at 1002 and 1003 — inside
    /// <c>om-registeret</c>. So Innhold is the seed's second section where the mockup calls it the
    /// third, and both of the bead's senses of that word are asserted below either way.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<SectionPlacement> Seeded =>
    [
        Section("om-registeret", "Om registeret", "About the registry", 1000),
        Section("innhold", "Innhold", "Content", 2000),
        Section(SectionKeys.DataCollections, "Datasamlinger", "Data collections", 3000, builtIn: true),
        Section("tilgang-og-ansvar", "Tilgang og ansvar", "Access and responsibility", 4000),
        Section("rettslig-grunnlag", "Rettslig grunnlag", "Legal basis", 5000),
        Section("kontakt", "Kontakt", "Contact", 6000),
        Section("alle-metadatafelt", "Alle metadatafelt", "All metadata fields", 9001),
    ];

    private static KildeDetail Placed() => Unplaced() with { Sections = Seeded };

    private IRenderedComponent<KildeView> Page(KildeDetail kilde) =>
        Render<KildeView>(b => b.Add(c => c.Kilde, kilde));

    /// <summary>Every section the page drew, as its anchor and its heading, in document order.</summary>
    private static IReadOnlyList<(string Id, string Heading)> Sections(IRenderedComponent<KildeView> cut) =>
        [.. cut.FindAll("section[data-nav-section]")
               .Select(section => (section.Id!, section.FirstElementChild!.TextContent.Trim()))];

    private static IReadOnlyList<string> Headings(IRenderedComponent<KildeView> cut) =>
        [.. Sections(cut).Select(section => section.Heading)];

    /// <summary>The contents nav's entries, as the section each names and its words.</summary>
    private static IReadOnlyList<(string Href, string Label)> Nav(IRenderedComponent<KildeView> cut) =>
        [.. cut.FindAll(".munin-explorer-page__toc a")
               .Select(link =>
               {
                   var href = link.GetAttribute("href")!;
                   return (href[href.IndexOf('#', StringComparison.Ordinal)..], link.TextContent.Trim());
               })];

    /// <summary>
    /// Every label and value the page states, outside the hero strip.
    /// </summary>
    /// <remarks>
    /// The hero and the sticky bar repeat the sections below them on purpose, so counting them in
    /// would make every number here larger by a constant and say nothing about what the sections
    /// hold. <see cref="SeededPlacementRenderingTest"/> makes the same cut for the same reason.
    /// </remarks>
    private static (int Terms, int Values) Facts(IRenderedComponent<KildeView> cut)
    {
        var page = (IElement)cut.Find(".munin-explorer-page").Clone(true);

        foreach (var hero in page
                     .QuerySelectorAll("dl.munin-explorer-page__facts, .munin-explorer-page__stuckbar")
                     .ToList())
        {
            hero.Remove();
        }

        // Counted apart rather than asserted equal: the catalogue holds some values in more than
        // one language, and those draw one dt over a dd per language.
        return (page.QuerySelectorAll("dt").Length, page.QuerySelectorAll("dd").Length);
    }

    [Fact]
    public void Sections_WhenThePayloadPlacesThem_ThenTheyDrawInTheOrderItPlacedThemAndNotTheViewsOwn()
    {
        // The interleave is the whole point: Datasamlinger sits between two property sections, in
        // the band the seed put it in, which the old fixed run could not express at all.
        Assert.Equal(
        [
            "Om registeret",
            "Innhold",
            "Delkilder og datasamlinger",
            "Tilgang og ansvar",
            "Rettslig grunnlag",
            "Kontakt",
            "Alle metadatafelt",
            "Kildeinformasjon",
            "Statistikk",
        ], Headings(Page(Placed())));
    }

    [Fact]
    public void Sections_WhenThePayloadPlacesNone_ThenThePageDrawsExactlyAsItDidBeforePlacements()
    {
        // The capture in Testdata is that API, and so is every environment Munin has not migrated.
        // A release of this package must never be the reason one of them has to deploy.
        Assert.Equal(
            ["Metadata", "Delkilder og datasamlinger", "Kildeinformasjon", "Statistikk"],
            Headings(Page(Unplaced())));
    }

    [Fact]
    public void Facts_WhenThePayloadPlacesTheSections_ThenThePageStatesNoFewerOfThemThanBefore()
    {
        // THE ONE THAT MATTERS. Splitting Metadata into the sections the placement names is a
        // rearrangement, and a rearrangement that quietly drops a row would look like a success:
        // the page gets longer, not shorter, so nobody reading it would miss the row.
        var before = Facts(Page(Unplaced()));
        var after = Facts(Page(Placed()));

        Assert.True(after.Terms >= before.Terms && after.Values >= before.Values,
                    $"The placed page states {after} where the unplaced one states {before}. "
                    + "A kildedetalj with fewer facts on it than the page it replaced is the failure "
                    + "this bead was opened for.");
    }

    [Fact]
    public void Statistics_WhenThePayloadNamesNoSectionForIt_ThenItKeepsItsOwnSectionAndItsFields()
    {
        // The mockup omits Statistikk, and a designer not thinking about a section is not a decision
        // to delete one: no field is dropped. It falls to the end of the page, keyed so that the
        // first seed naming it moves it with no release of this package.
        var cut = Page(Placed());
        var statistics = cut.Find("#" + DetailSectionIds.Statistics);

        Assert.Equal("Statistikk", statistics.FirstElementChild!.TextContent.Trim());
        Assert.Contains("Totalt antall variabler", statistics.TextContent, StringComparison.Ordinal);
        Assert.Contains("Dataperiode", statistics.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceInformation_WhenThePayloadNamesNoSectionForIt_ThenItKeepsItsOwnSectionAndItsFields()
    {
        // Kildeinformasjon is the other one the mockup does not name. Its kildetype is the fact
        // worth naming here: no property definition carries it, so no placed section can draw it
        // and this box is the only place on the page a reader reads it as a labelled field.
        var cut = Page(Placed());
        var source = cut.Find("#" + DetailSectionIds.Source);

        Assert.Equal("Kildeinformasjon", source.FirstElementChild!.TextContent.Trim());
        Assert.Contains("Type datakilde", source.TextContent, StringComparison.Ordinal);
        Assert.Contains("Sist oppdatert i Munin", source.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Section_WhenACuratorMovesAGroupToAnotherBand_ThenItMovesWithNoRebuildOfThisPackage()
    {
        // What "data-driven" has to mean, asked the only way this repository can ask it: the same
        // build, the same component, two payloads differing in one section's band. A switch in the
        // view emitting eight named sections would pass every assertion above and fail this one.

        // At the head, because the API sorts this collection and DetailLayout re-sorts nothing.
        var moved = Unplaced() with
        {
            Sections =
            [
                Section("kontakt", "Kontakt", "Contact", 500),
                .. Seeded.Where(section => section.Key != "kontakt"),
            ],
        };

        Assert.Equal("Kontakt", Headings(Page(moved))[0]);
        Assert.Equal("Kontakt", Headings(Page(Placed()))[5]);
    }

    [Fact]
    public void Contents_WhenTheSectionsAreDrawn_ThenTheNavIsHeadedInnholdAndOneSectionIsAlsoCalledInnhold()
    {
        // Two different things sharing a word: the nav that names what is on the page, and the
        // catalogue's own section of that name. Neither is a duplicate of the other and neither is
        // to be deduplicated away.
        var cut = Page(Placed());

        Assert.Equal("Innhold", cut.Find(".munin-explorer-page__toc nav > h2").TextContent.Trim());
        Assert.Equal("Innhold", cut.Find("#innhold").FirstElementChild!.TextContent.Trim());
        Assert.Contains(("#innhold", "Innhold"), Nav(cut));
    }

    [Fact]
    public void Nav_WhenTheSectionsAreDrawn_ThenItNamesEverySectionOnThePageInPageOrder()
    {
        // Read against the drawn sections rather than against a list written here, so the two
        // cannot drift apart: an entry pointing at a section the layout left out is a link that
        // scrolls a reader nowhere.
        var cut = Page(Placed());
        var drawn = Sections(cut).Select(section => ("#" + section.Id, section.Heading)).ToList();

        Assert.Equal(drawn, Nav(cut));
        Assert.All(Nav(cut), entry => Assert.NotNull(cut.Find(entry.Href)));
    }

    [Fact]
    public void Sections_WhenTheSectionsAreDrawn_ThenNoTwoOfThemShareAnId()
    {
        // The catalogue's group keys became section ids with this change, so they now share one
        // document with the view's own four — and a repeated id sends the nav's second link to the
        // first section of that name.
        var ids = Sections(Page(Placed())).Select(section => section.Id).ToList();

        Assert.Equal(ids.Distinct(StringComparer.Ordinal), ids);
    }

    [Fact]
    public void DataCollections_WhenItIsPlacedBetweenTwoPropertySections_ThenItStillDrawsItsTableOfRelatedEntities()
    {
        // Moving the block is not rewriting it. The tree, the disclosure and the table under it are
        // the same markup at a different place on the page.
        var cut = Page(Placed());
        var collections = cut.Find("#" + DetailSectionIds.DataCollections);

        Assert.NotNull(collections.QuerySelector(".munin-explorer-hierarchy__metadata"));

        // The rows themselves, against the same block on the unplaced page: a table that still has
        // a <table> in it but has lost its datasamlinger would pass a shape check and fail this.
        Assert.Equal(
            Rows(Page(Unplaced()).Find("#" + DetailSectionIds.DataCollections)),
            Rows(collections));

        Assert.NotEmpty(Rows(collections));
    }

    /// <summary>A related-entity table's rows, as the text each states.</summary>
    private static IReadOnlyList<string> Rows(IElement section) =>
        [.. section.QuerySelectorAll("table tbody tr").Select(row => row.TextContent.Trim())];

    [Fact]
    public void Group_WhenThePayloadNamesNoSectionForIt_ThenItIsStillDrawnUnderMetadata()
    {
        // A group the placement data does not name has no stable id to be a section under: its
        // heading is the only thing identifying it, and a heading is bilingual and a curator's to
        // reword. So it keeps the block it has always been drawn in rather than being dropped.
        var half = Unplaced() with { Sections = [.. Seeded.Where(section => section.Key != "kontakt")] };

        var headings = Headings(Page(half));

        Assert.Contains("Metadata", headings);
        Assert.Contains("Kontakt", Page(half).Find("#" + DetailSectionIds.Metadata).TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Section_WhenAPlacementRowNamesTheWrongKindOfSection_ThenItFallsToTheTailRatherThanOffThePage()
    {
        // A row whose isBuiltIn contradicts what its key names finds nothing in the pool it is sent
        // to, and counting that key as placed on the row alone would leave the section out of the
        // other pool's fallback as well — off the page, with every field it holds.
        var builtInGroup = Unplaced() with
        {
            Sections =
            [
                .. Seeded.Select(section =>
                    section.Key == "innhold" ? section with { IsBuiltIn = true } : section),
            ],
        };

        var curatedBlock = Unplaced() with
        {
            Sections =
            [
                .. Seeded.Select(section =>
                    section.Key == SectionKeys.DataCollections ? section with { IsBuiltIn = false } : section),
            ],
        };

        Assert.Contains("Innhold", Headings(Page(builtInGroup)));
        Assert.Contains("Delkilder og datasamlinger", Headings(Page(curatedBlock)));
        Assert.NotEmpty(Rows(Page(curatedBlock).Find("#" + DetailSectionIds.DataCollections)));

        var before = Facts(Page(Unplaced()));

        foreach (var crossed in new[] { builtInGroup, curatedBlock })
        {
            var after = Facts(Page(crossed));

            Assert.True(after.Terms >= before.Terms && after.Values >= before.Values,
                        $"A row in the wrong pool left the page stating {after} where the unplaced "
                        + $"one states {before}.");
        }
    }

    [Fact]
    public void Sections_WhenTheirBandsDisagreeWithTheOrderTheyArriveIn_ThenThePageDrawsTheArrivalOrder()
    {
        // The contract is the order the API sent and not groupSortOrder, which is why nothing here
        // re-sorts on it: an entry no placement names carries no band to sort by. Pinned because
        // nothing else on this side would notice the API's own ordering changing.
        var scrambled = Unplaced() with
        {
            Sections =
            [
                Section("kontakt", "Kontakt", "Contact", 6000),
                Section("innhold", "Innhold", "Content", 2000),
                Section("om-registeret", "Om registeret", "About the registry", null),
            ],
        };

        Assert.Equal(["Kontakt", "Innhold", "Om registeret"], Headings(Page(scrambled)).Take(3));
    }

    /// <summary>The mockup's own section names, which must reach the page as data and never as markup.</summary>
    /// <remarks>
    /// The eight headings of <c>kilde-detalj-d2-loddrett.html</c>. A view that emits them is a view
    /// that works on the day it merges and puts every later change to the page into a release of
    /// this package — which is the thing chain C exists to stop.
    /// </remarks>
    private static readonly string[] MockupSectionNames =
    [
        "Om registeret", "Formål", "Tilgang og ansvar", "Rettslig grunnlag", "Alle metadatafelt",
    ];

    [Theory]
    [InlineData("KildeView.razor")]
    [InlineData("KildeView.razor.cs")]
    public void View_WhenItsSourceIsRead_ThenItSpellsNoneOfTheMockupsSectionNames(string file)
    {
        // The other half, caught from the side a render cannot see: a hard-coded run of the eight
        // would satisfy every ordering assertion above on the seeded payload and none of them on
        // the next payload a curator edits.
        //
        // Innhold, Datasamlinger and Kontakt are deliberately not on the list. The first two are
        // words this view legitimately owns — the nav's own heading, and the block heading that
        // follows the source rather than the catalogue — and all three are in Texts either way, so
        // banning them here would ban a string this file has to be able to reach.
        var source = File.ReadAllText(Repo.In("src", "Fhi.Munin.Explorer", "Blazor", file));

        foreach (var name in MockupSectionNames)
        {
            Assert.DoesNotContain(name, source, StringComparison.Ordinal);
        }
    }
}
