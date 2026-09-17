using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

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
/// <para>
/// <b>What the href tests here cannot prove.</b> They read the <c>href</c> ATTRIBUTE, which is a
/// string in a render tree. What broke on helsedata is what the browser RESOLVES that string to
/// against the page's <c>&lt;base href&gt;</c>, and bUnit has no base element and no URL resolver,
/// so it would report <c>#metadata</c> as correct in the broken build and the fixed one alike. The
/// resolved value is measured in a browser, by the contents-nav assertion in
/// <c>scripts/state-assertions.mjs</c>. These tests pin the string that assertion depends on.
/// </para>
/// </remarks>
public class DetailTocTest : ExplorerTestContext
{
    private static readonly DetailTocEntry[] Three =
    [
        new(DetailSectionIds.Metadata, "Metadata"),
        new(DetailSectionIds.Source, "Kildeinformasjon"),
        new(DetailSectionIds.Statistics, "Statistikk"),
    ];

    private IRenderedComponent<DetailToc> RenderToc(
        IReadOnlyList<DetailTocEntry> entries, string? cascaded = null) =>
        Render<DetailToc>(parameters =>
        {
            parameters.Add(p => p.Entries, entries).Add(p => p.Label, "Innhold");

            // Only when there is one: bUnit refuses a null cascading value, and a host that
            // cascades nothing is the case every other test here is about.
            if (cascaded is not null)
            {
                parameters.AddCascadingValue(DetailToc.PageAddressName, cascaded);
            }
        });

    /// <summary>Put the circuit at <paramref name="url"/>, as a host mounting the nav would.</summary>
    private void Arrive(string url) =>
        Services.GetRequiredService<NavigationManager>().NavigateTo(url);

    [Fact]
    public void Render_WhenThereAreEntries_ThenEachLinksToItsSectionInTheOrderGiven()
    {
        // Fragment anchors are the whole of this bead: the browser scrolls, the section's own
        // scroll-margin-top keeps the heading clear of a sticky header, and no script runs.
        var cut = RenderToc(Three);

        Assert.Equal(["/#metadata", "/#source", "/#statistics"],
                     cut.FindAll("a").Select(a => a.GetAttribute("href")));

        Assert.Equal(["Metadata", "Kildeinformasjon", "Statistikk"],
                     cut.FindAll("a").Select(a => a.TextContent));
    }

    [Fact]
    public void Render_WhenTheCircuitCarriesAPathAndQuery_ThenEveryHrefNamesThemBeforeTheFragment()
    {
        // The bare "#id" this replaces resolved against the host's <base href="/"> rather than
        // against the page, so every entry left the kilde for the site root. Both halves are load
        // bearing: without the query the jump is a different document and the browser reloads,
        // which fixes the scroll and loses the ?kilde= (Fhi.Metadata-l9l2n.114).
        Arrive("http://localhost/MuninKelda/?kilde=8dee8189-0e2d-4e6d-9362-e0bf8ede8d95");

        var cut = RenderToc(Three);

        Assert.All(cut.FindAll("a"), link => Assert.StartsWith(
            "/MuninKelda/?kilde=8dee8189-0e2d-4e6d-9362-e0bf8ede8d95#",
            link.GetAttribute("href"),
            StringComparison.Ordinal));
    }

    [Fact]
    public void Render_WhenTheCircuitsAddressCarriesAFragment_ThenItIsNotCarriedIntoTheHrefs()
    {
        // A reader who arrived on a deep link, or pressed an entry in a host that told Blazor
        // about it. Two fragments in one href point at nothing at all.
        Arrive("http://localhost/kilder?kilde=abc#source");

        var cut = RenderToc(Three);

        Assert.Equal("/kilder?kilde=abc#metadata", cut.FindAll("a")[0].GetAttribute("href"));
    }

    [Fact]
    public void Render_WhenAWrapperCascadesItsMirroredAddress_ThenTheHrefsUseItRatherThanTheCircuits()
    {
        // UrlMirror moves the address bar with history.replaceState, which Blazor is never told
        // about, so NavigationManager.Uri is the address the circuit LOADED. A kilde opened from
        // the list is in the cascaded value and in no other reachable place.
        Arrive("http://localhost/kilder");

        var cut = RenderToc(Three, "/kilder?kilde=abc");

        Assert.Equal("/kilder?kilde=abc#metadata", cut.FindAll("a")[0].GetAttribute("href"));
    }

    [Fact]
    public void Moved_WhenTheHostRewritesTheQueryUnderAStandingComponent_ThenTheHrefsFollow()
    {
        // Why the LocationChanged subscription is there, and the half every other test here misses
        // by arriving BEFORE it renders. A host owning its own query moves it under a component
        // nothing above re-renders, and the links would go on naming the address it arrived on.
        Arrive("http://localhost/kilder?kilde=one");

        var cut = RenderToc(Three);

        Assert.Equal("/kilder?kilde=one#metadata", cut.FindAll("a")[0].GetAttribute("href"));

        Arrive("http://localhost/kilder?kilde=two");

        cut.WaitForAssertion(() => Assert.Equal(
            "/kilder?kilde=two#metadata", cut.FindAll("a")[0].GetAttribute("href")));
    }

    [Fact]
    public void Moved_WhenTheComponentIsGone_ThenItIsNotStillListeningForNavigations()
    {
        // LocationChanged belongs to the host and outlives every component that touches it, and a
        // nav is built afresh on every drill-in: a lost unsubscribe accumulates one handler per
        // detail view the reader opens. Silent, because rendering a disposed component is a no-op.
        var cut = RenderToc(Three);
        var toc = cut.Instance;

        Assert.Contains(toc, NavigationListeners.Of(Services.GetRequiredService<NavigationManager>()));

        // The call the renderer makes when a host's Router leaves the page, made directly: the
        // renderer's own DisposeComponents queues it and returns, which is a race from here.
        ((IDisposable)toc).Dispose();

        Assert.DoesNotContain(toc, NavigationListeners.Of(Services.GetRequiredService<NavigationManager>()));
        Assert.Null(Record.Exception(() => Arrive("http://localhost/kilder?kilde=three")));
    }

    [Fact]
    public void Render_WhenThereAreEntries_ThenTheListWearsHelsedatasOwnNamesAndNoneOfOurs()
    {
        // form-menu__list and form-menu__list__item are helsedata's own, so the nav takes that
        // site's link colour and padding with no rule of ours. Where Stiler declares them, and
        // where the stand-in for a host without it lives, is on DetailToc and in the samples.
        var cut = RenderToc(Three);

        var nav = cut.Find("nav");

        Assert.Equal("Innhold", nav.GetAttribute("aria-label"));
        Assert.Null(nav.GetAttribute("class"));

        Assert.Equal(2, nav.Children.Length);
        Assert.Equal("h2", nav.Children[0].LocalName);
        Assert.Equal("Innhold", nav.Children[0].TextContent);
        Assert.Null(nav.Children[0].GetAttribute("class"));

        Assert.Equal("form-menu__list", nav.Children[1].ClassName);

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
