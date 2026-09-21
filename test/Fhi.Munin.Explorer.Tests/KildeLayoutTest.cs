using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The kilde page's section order is the payload's, one pass over both kinds of section, and no
/// fact the old fixed run of four headings stated is lost in the rearranging
/// (Fhi.Metadata-35w0p.22).
/// </summary>
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
    /// <c>0014_SeedBuiltInSectionPlacements.sql</c>, read on Munin <c>main</c> 2026-09-18. Seven
    /// sections where the mockup draws eight, because 0013 seeds no <c>formaal</c> section and
    /// places the mockup's Formål rows inside <c>om-registeret</c>. The catch-all's own band is
    /// 9000 + rank, so it is last whatever sections a page has.
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

    /// <summary>The same payload with one curated group answering to another key.</summary>
    /// <remarks>
    /// Both the property metadata and the placement row move, since a group the placements do not
    /// name is drawn under Metadata rather than as a section of its own.
    /// </remarks>
    private static KildeDetail ReKeyed(KildeDetail kilde, string from, string to) =>
        kilde with
        {
            PropertyMetadata =
            [
                .. kilde.PropertyMetadata.Select(entry =>
                    entry.GroupKey == from ? entry with { GroupKey = to } : entry),
            ],
            Sections =
            [
                .. kilde.Sections.Select(section =>
                    section.Key == from ? section with { Key = to } : section),
            ],
        };

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

    /// <summary>The anchor a placed group's section carries, which is its key and not its heading.</summary>
    private static string Anchor(string groupKey) =>
        DetailSectionIds.ReserveGroupId(groupKey, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Every label and value the page states, outside the hero strip.
    /// </summary>
    /// <remarks>
    /// The hero and the sticky bar repeat the sections below them on purpose, so counting them in
    /// would say nothing about what the sections hold. <see cref="SeededPlacementRenderingTest"/>
    /// makes the same cut for the same reason.
    /// </remarks>
    private static (IReadOnlyList<string> Terms, IReadOnlyList<string> Values) Facts(
        IRenderedComponent<KildeView> cut)
    {
        var page = (IElement)cut.Find(".munin-explorer-page").Clone(true);

        foreach (var hero in page
                     .QuerySelectorAll("dl.munin-explorer-page__facts, .munin-explorer-page__stuckbar")
                     .ToList())
        {
            hero.Remove();
        }

        // Gathered apart rather than zipped: the catalogue holds some values in more than one
        // language, and those draw one dt over a dd per language.
        return (Text(page, "dt"), Text(page, "dd"));
    }

    private static IReadOnlyList<string> Text(IElement page, string selector) =>
        [.. page.QuerySelectorAll(selector).Select(cell => cell.TextContent.Trim())];

    /// <summary>
    /// Whatever <paramref name="before"/> states that <paramref name="after"/> does not, counting
    /// repeats.
    /// </summary>
    /// <remarks>
    /// A multiset rather than two totals: the catch-all section repeats the whole record on
    /// purpose, so one row duplicated there can hold a total up while another row is gone.
    /// </remarks>
    private static IReadOnlyList<string> Missing(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var remaining = after.ToList();

        return [.. before.Where(item => !remaining.Remove(item))];
    }

    /// <summary>Asserts the rearranged page still states every fact the page before it did.</summary>
    private static void NoFactLost(
        (IReadOnlyList<string> Terms, IReadOnlyList<string> Values) before,
        (IReadOnlyList<string> Terms, IReadOnlyList<string> Values) after,
        string what)
    {
        var terms = Missing(before.Terms, after.Terms);
        var values = Missing(before.Values, after.Values);

        Assert.True(
            terms.Count == 0 && values.Count == 0,
            $"{what} lost labels [{string.Join(" | ", terms)}] and values [{string.Join(" | ", values)}] "
            + "the page it replaces states. A kildedetalj with fewer facts on it than the page it "
            + "replaced is the failure this bead was opened for.");
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
    public void Facts_WhenThePayloadPlacesTheSections_ThenThePageStatesEveryOneItStatedBefore()
    {
        // THE ONE THAT MATTERS. Splitting Metadata into the sections the placement names is a
        // rearrangement, and a rearrangement that quietly drops a row would look like a success:
        // the page gets longer, not shorter, so nobody reading it would miss the row.
        NoFactLost(Facts(Page(Unplaced())), Facts(Page(Placed())), "The placed page");
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
        Assert.Equal("Innhold", cut.Find("#" + Anchor("innhold")).FirstElementChild!.TextContent.Trim());
        Assert.Contains(("#" + Anchor("innhold"), "Innhold"), Nav(cut));
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

    [Theory]
    [InlineData(DetailSectionIds.Metadata)]
    [InlineData(DetailSectionIds.Source)]
    [InlineData(DetailSectionIds.Statistics)]
    [InlineData(DetailSectionIds.DataCollections)]
    public void Section_WhenACuratorMintsAGroupKeySpeltLikeOneOfOurIds_ThenTheTwoStillAnchorApart(string reserved)
    {
        // The keys are an open set a curator types and the ids above a closed one this package
        // ships, and nothing reconciles them — "metadata" is a plausible Munin slug. Without the
        // prefix the nav's second link to that id scrolls the reader into the first one wearing it.

        // kontakt stays unplaced so the Metadata block draws as well.
        var minted = ReKeyed(
            Unplaced() with { Sections = [.. Seeded.Where(section => section.Key != "kontakt")] },
            "innhold",
            reserved);

        var cut = Page(minted);
        var ids = Sections(cut).Select(section => section.Id).ToList();

        Assert.Equal(ids.Distinct(StringComparer.Ordinal), ids);
        Assert.Contains(reserved, ids);
        Assert.Contains(Anchor(reserved), ids);
        Assert.Equal("Innhold", cut.Find("#" + Anchor(reserved)).FirstElementChild!.TextContent.Trim());
    }

    [Theory]
    [InlineData("om registeret")]
    [InlineData("om#registeret")]
    [InlineData("om/registeret?x=1")]
    public void Section_WhenAGroupKeyIsNotAFragmentTheBrowserCanAddress_ThenTheIdAndTheNavStillResolve(string key)
    {
        // The first values a curator types that reach an id and an href unfiltered. Nothing
        // constrains a key at the source, and a nav entry pointing at a fragment the browser cannot
        // resolve is a control that scrolls nowhere, with nothing failing anywhere to say so.
        // The first two strip to the id "om-registeret" is already anchored at, so the one the
        // stripping rewrote carries a digest of its own key rather than sharing that anchor — and
        // carries it whatever else the page holds, which is what a shared deep link needs.
        var cut = Page(ReKeyed(Placed(), "innhold", key));
        var id = Nav(cut).Single(entry => entry.Label == "Innhold").Href[1..];

        Assert.Matches("^[A-Za-z0-9_-]+$", id);
        Assert.StartsWith(Anchor(key), id, StringComparison.Ordinal);
        Assert.Contains(id, Sections(cut).Select(section => section.Id));

        var ids = Sections(cut).Select(section => section.Id).ToList();

        Assert.Equal(ids.Distinct(StringComparer.Ordinal), ids);
        Assert.All(Nav(cut), entry => Assert.NotNull(cut.Find(entry.Href)));
    }

    [Fact]
    public void Section_WhenACuratorMintsAGroupKeyOneOfOurBlocksAnswersTo_ThenBothSectionsAndTheirFieldsSurvive()
    {
        // The two kinds share one key namespace and a curator can spell "statistikk". The pools are
        // kept apart so the group cannot swallow the block, and the placed-key bookkeeping is kept
        // apart with them so placing the group cannot take the block out of its own fallback tail.
        var cut = Page(ReKeyed(Placed(), "innhold", SectionKeys.Statistics));

        Assert.Contains("Innhold", Headings(cut));
        Assert.Contains("Statistikk", Headings(cut));

        var statistics = cut.Find("#" + DetailSectionIds.Statistics);

        Assert.Contains("Totalt antall variabler", statistics.TextContent, StringComparison.Ordinal);
        Assert.Contains("Dataperiode", statistics.TextContent, StringComparison.Ordinal);

        NoFactLost(Facts(Page(Unplaced())), Facts(cut), "A curated group keyed statistikk");
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

    /// <summary>The datasamling disclosure, or null on a source whose tree is empty.</summary>
    private static IElement? Disclosure(IRenderedComponent<KildeView> cut) =>
        cut.Find("#" + DetailSectionIds.DataCollections)
           .QuerySelector("details.munin-explorer-hierarchy__metadata");

    [Fact]
    public void DataCollections_WhenTheSourceHasNoTreeAtAll_ThenTheDisclosureIsAbsentAndTheViewStillDraws()
    {
        // The block moved from markup to builder calls with this change, so the branch the compiler
        // used to check is now an if a reader has to trust. An inverted condition would draw a
        // disclosure over an empty table, which nothing else here would notice.
        var bare = Unplaced() with { Datasamlinger = [], Delkilder = [] };
        var cut = Page(bare);

        Assert.Null(Disclosure(cut));
        Assert.NotNull(cut.Find("#" + DetailSectionIds.DataCollections).QuerySelector(".munin-explorer-hierarchy"));
        Assert.Equal("Datasamlinger", Headings(cut)[1]);
    }

    [Fact]
    public void DataCollections_WhenTheSourceHasATree_ThenTheDisclosureIsDrawnWithItsSummaryAndTheTableInside()
    {
        // The other half of the same branch, written out rather than left to the shape check above:
        // a lost CloseElement or a dropped class reads as valid markup and renders unstyled.
        var disclosure = Disclosure(Page(Placed()));

        Assert.NotNull(disclosure);
        Assert.Equal("Beskrivelser og gyldighetsperioder",
                     disclosure.QuerySelector("summary")!.TextContent.Trim());
        Assert.NotEmpty(Rows(disclosure));
    }

    [Fact]
    public void DataCollections_WhenTheReaderOpensOneSourcesDisclosureAndThenAnother_ThenItIsClosedAgain()
    {
        // The @key became SetKey(kilde.Id) with this change, and it is a behavioural guarantee
        // nothing else can see: without it the browser keeps the element the reader opened, and the
        // next source's tree arrives already expanded under a disclosure they never pressed.
        var cut = Page(Placed());

        // The browser's own doing, which is why no render can produce it: a press on <summary>
        // writes this attribute before any handler runs.
        Disclosure(cut)!.SetAttribute("open", "");

        cut.Render(p => p.Add(c => c.Kilde, Placed() with { Id = Guid.NewGuid() }));

        Assert.False(Disclosure(cut)!.HasAttribute("open"));
    }

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
            NoFactLost(before, Facts(Page(crossed)), "A row in the wrong pool");
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

    [Fact]
    public void Nav_WhenASectionIsHeadedInTheCataloguesNorwegianOnAnEnglishPage_ThenTheLinkIsMarkedAsTheHeadingIs()
    {
        // The nav label is the heading byte for byte, so an unmarked link has a screen reader
        // reading the heading in a Norwegian voice and the entry for that same section in English
        // phonetics — WCAG 3.1.2, and something axe cannot see.
        var untranslated = Placed() with
        {
            PropertyMetadata =
            [
                .. Placed().PropertyMetadata.Select(entry =>
                    entry.GroupKey == "om-registeret"
                        ? entry with { GroupTranslations = new Dictionary<string, string> { ["no"] = "Om registeret" } }
                        : entry),
            ],
        };

        var cut = Render<KildeView>(b => b.Add(c => c.Kilde, untranslated).Add(c => c.Language, "en"));
        var anchor = "#" + Anchor("om-registeret");

        Assert.Equal("no", cut.Find("section" + anchor).FirstElementChild!.GetAttribute("lang"));
        Assert.Equal("no", Link(cut, anchor).GetAttribute("lang"));

        // The sibling, so the assertion above says this entry is marked rather than all of them.
        Assert.Null(Link(cut, "#" + DetailSectionIds.Source).GetAttribute("lang"));
    }

    /// <summary>The contents-nav link pointing at <paramref name="fragment"/>.</summary>
    private static IElement Link(IRenderedComponent<KildeView> cut, string fragment) =>
        cut.FindAll(".munin-explorer-page__toc a")
           .First(link => link.GetAttribute("href")!.EndsWith(fragment, StringComparison.Ordinal));

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
        // satisfies every ordering assertion above on the seeded payload and none on the next
        // payload a curator edits. Innhold, Datasamlinger and Kontakt are off the list because this
        // view legitimately owns those words and reaches them through Texts.
        var source = Code(File.ReadAllText(Repo.In("src", "Fhi.Munin.Explorer", "Blazor", file)));

        foreach (var name in MockupSectionNames)
        {
            Assert.DoesNotContain(name, source, StringComparison.Ordinal);
        }
    }

    /// <summary>What <paramref name="file"/> renders, rather than what it explains.</summary>
    /// <remarks>
    /// Both files discuss the mockup's sections by name in prose, so a check a comment can break is
    /// a check prose can switch off — <see cref="RazorSource.WithoutComments"/> has the rest.
    /// </remarks>
    private static string Code(string file) =>
        Regex.Replace(RazorSource.WithoutComments(file), @"^\s*///?.*$", " ", RegexOptions.Multiline);
}
