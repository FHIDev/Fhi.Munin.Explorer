using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The contents nav, rendered on its own rather than through one of the three detail views.
/// </summary>
/// <remarks>
/// The view tests ask the question that matters most — whether an entry points at a section that is
/// really there — because only a view knows which of its blocks drew anything. What is left for
/// here is the markup itself: the names it borrows, the shape of the list, and the two states no
/// view reaches, an entry list that is empty and one whose labels say nothing about the hrefs.
/// <para>
/// No JavaScript anywhere in it, and no <c>form-menu__list__item--active</c>: with nothing tracking
/// the scroll position an active item is permanently wrong on every section but one, so the
/// highlight waits for the scroll-spy rather than being faked here.
/// </para>
/// </remarks>
public class DetailTocTest : BunitContext
{
    private static readonly DetailTocEntry[] Three =
    [
        new(DetailSectionIds.Metadata, "Metadata"),
        new(DetailSectionIds.Source, "Kildeinformasjon"),
        new(DetailSectionIds.Statistics, "Statistikk"),
    ];

    private IRenderedComponent<DetailToc> RenderToc(IReadOnlyList<DetailTocEntry> entries) =>
        Render<DetailToc>(parameters => parameters
            .Add(p => p.Entries, entries)
            .Add(p => p.Label, "Innhold"));

    [Fact]
    public void Render_WhenThereAreEntries_ThenEachIsAPlainFragmentLinkInTheOrderGiven()
    {
        // Plain #fragment anchors are the whole of this bead: the browser scrolls, the section's
        // own scroll-margin-top keeps the heading clear of a sticky header, and no script runs.
        var cut = RenderToc(Three);

        Assert.Equal(["#metadata", "#source", "#statistics"],
                     cut.FindAll("a").Select(a => a.GetAttribute("href")));

        Assert.Equal(["Metadata", "Kildeinformasjon", "Statistikk"],
                     cut.FindAll("a").Select(a => a.TextContent));
    }

    [Fact]
    public void Render_WhenThereAreEntries_ThenTheListWearsHelsedatasOwnNamesAndNoneOfOurs()
    {
        // form-menu__list and form-menu__list__item are helsedata's own, so the nav takes that
        // site's link colour and padding with no rule of ours. Where those rules actually live, and
        // why that is not settled, is on DetailToc and in the sample stylesheets.
        var cut = RenderToc(Three);

        var nav = cut.Find("nav");

        Assert.Equal("Innhold", nav.GetAttribute("aria-label"));
        Assert.Null(nav.GetAttribute("class"));
        Assert.Equal("form-menu__list", Assert.Single(nav.Children).ClassName);

        // Exactly the base name on every item, which is also what says no item was marked active:
        // --active and --active-child are the modifiers the scroll-spy will add, in chain B.
        Assert.All(cut.FindAll("li"), item => Assert.Equal("form-menu__list__item", item.ClassName));
    }

    [Fact]
    public void Render_WhenThereAreNoEntries_ThenNothingIsDrawnAtAll()
    {
        // An empty <nav> in the contents column is worse than no nav: the column is drawn for any
        // fragment at all, so it would cost a 250px rail beside the content and say nothing in it.
        Assert.Equal(string.Empty, RenderToc([]).Markup.Trim());
    }

    [Fact]
    public void Column_WhenThereAreNoEntries_ThenItIsNullRatherThanAnEmptyFragment()
    {
        // What the three views hand DetailPage.Contents. Null is the answer that costs no column,
        // and an empty fragment is not the same answer — see DetailPageTest.
        Assert.Null(DetailToc.Column([], "Innhold"));
        Assert.NotNull(DetailToc.Column(Three, "Innhold"));
    }

    [Fact]
    public void Render_Always_ThenEveryNameItEmitsHasARuleSomeStylesheetKeeps()
    {
        // Both names are helsedata's own, captured in test/host-class-names.txt. That capture is
        // what this reads, and it cannot tell Stiler's rules from the live page's — see DetailToc.
        var cut = RenderToc(Three);

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
