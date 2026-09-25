using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The kilde tree, the kilde page's delkilde structure and the filter panel's tree put to one
/// structure, as each of their payloads carries it (Fhi.Metadata-fuzw0).
/// </summary>
/// <remarks>
/// Each surface has order tests of its own; this is what fails when one of them keeps its own rule
/// and a reader sees one kilde arranged two ways on one page — which is how K_KK read before.
/// <see cref="KildeSearchTest"/> puts its expanded row to the same fixture.
/// </remarks>
public sealed class SiblingOrderSurfacesTest : ExplorerTestContext
{
    private sealed class Client(KildeHierarchy hierarchy) : EmptyMuninExplorerClient
    {
        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(hierarchy);
    }

    private static string Href(Guid datasamling) => $"/kilder?datasamling={datasamling}";

    private IRenderedComponent<KildeView> Page(bool ranked)
    {
        Services.AddSingleton<IMuninExplorerClient>(new Client(InterleavedKilde.Hierarchy(ranked)));

        var cut = Render<KildeView>(b => b
            .Add(c => c.Kilde, InterleavedKilde.Detail(ranked))
            .Add(c => c.Language, "nb")
            .Add(c => c.DatasamlingHref, (Func<Guid, string>)Href));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".munin-explorer-hierarchy__nodes")));

        return cut;
    }

    /// <summary>The tree's rows, parents before children, as the reader meets them.</summary>
    private static IReadOnlyList<string> Tree(IRenderedComponent<KildeView> cut) =>
    [
        .. cut.FindAll(".munin-explorer-hierarchy summary, .munin-explorer-hierarchy .munin-explorer-hierarchy__leaf")
            .Select(row => row.QuerySelector(":scope > span:not([aria-hidden]):not(.screenreader-only)")!.TextContent.Trim())
    ];

    /// <summary>The metadata disclosure's delkilde headings and datasamling rows, in document order.</summary>
    private static IReadOnlyList<string> Structure(IRenderedComponent<KildeView> cut) =>
    [
        .. cut.Find(".munin-explorer-hierarchy__metadata")
            .QuerySelectorAll(".munin-explorer-kilde__delkilde-name, table.munin-explorer-kilde__datasamlinger tbody th")
            .Select(e => e.TextContent.Trim())
    ];

    private static IEnumerable<string> Filters(bool ranked)
    {
        static IEnumerable<HierarchyNode> Flatten(IEnumerable<HierarchyNode> nodes) =>
            nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

        return Flatten(Assert.Single(FilterHierarchy.Build(InterleavedKilde.Filters(ranked))).Children)
            .Select(node => node.Name);
    }

    private static IEnumerable<string> Nodes(bool ranked)
    {
        static IEnumerable<KildeHierarchyNode> Flatten(IEnumerable<KildeHierarchyNode> nodes) =>
            nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

        return Flatten(KildeHierarchyNode.From(InterleavedKilde.Hierarchy(ranked))).Select(node => node.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Order_WhenEverySurfaceReadsTheSameKilde_ThenTheyAllAgreeWithTheFilterTree(bool ranked)
    {
        // Unranked as well: the legacy fallback is where two surfaces would most easily each keep a
        // plausible order of their own — name order in one, datasamlinger first in another.
        var expected = ranked ? InterleavedKilde.RankedPreorder : InterleavedKilde.UnrankedPreorder;
        var cut = Page(ranked);

        Assert.Equal(expected, Filters(ranked));
        Assert.Equal(expected, Nodes(ranked));
        Assert.Equal(expected, Tree(cut));
        Assert.Equal(expected, Structure(cut));
    }

    [Fact]
    public void Links_WhenTheTreeIsReordered_ThenEachStillOpensItsOwnDatasamling()
    {
        // Reordering moves nodes, never their payload: a link that followed the position instead of
        // the node would open a sibling's datasamling with every order assertion still green.
        var cut = Page(ranked: true);

        Assert.Equal(
            [
                Href(InterleavedKilde.Registrering), Href(InterleavedKilde.Kontroll), Href(InterleavedKilde.Ultralyd),
                Href(InterleavedKilde.Utskriving), Href(InterleavedKilde.Oppfolging)
            ],
            cut.FindAll("a.munin-explorer-hierarchy__open").Select(link => link.GetAttribute("href")));
        Assert.All(cut.FindAll("a.munin-explorer-hierarchy__open"), link =>
            Assert.Equal("li", link.ParentElement?.LocalName));
    }

    [Fact]
    public void Disclosure_WhenTheStructureIsInterleaved_ThenBothDisclosuresStayNativeAndStartShut()
    {
        // The metadata stays behind its own <details> and each branch keeps its native one; the
        // merge changes what is inside them and must not open or flatten either.
        var cut = Page(ranked: true);

        var metadata = cut.Find("details.munin-explorer-hierarchy__metadata");
        Assert.False(metadata.HasAttribute("open"));

        var branches = cut.FindAll("details.munin-explorer-hierarchy__branch");
        Assert.Equal(["Fødsel", "Svangerskap"], branches.Select(Summary));
        Assert.All(branches, branch => Assert.False(branch.HasAttribute("open")));
    }

    private static string Summary(IElement details) =>
        details.QuerySelector("summary > span:not([aria-hidden]):not(.screenreader-only)")!.TextContent.Trim();
}
