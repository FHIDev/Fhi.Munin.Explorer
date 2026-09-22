using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The section a reader jumped to, and whether the address bar still names it afterwards
/// (Fhi.Metadata-7np6k).
/// </summary>
/// <remarks>
/// Every assertion here is on what reached <c>history.replaceState</c>, because that is the whole of
/// the defect: the scroll and the focus both happen before the rewrite and survive it, so the page
/// looks right in a browser while the address in it names no section at all — and a reader who
/// copies the link sends another reader to the top of the page.
/// <para>
/// The other half is the one that has no symptom until much later: a fragment naming a section of
/// the view being left must never reach a link out of it, so <c>UrlMirror.Address</c> stays
/// fragment-free and the mirror spends what it was given at the first different state.
/// </para>
/// </remarks>
public class UrlMirrorFragmentTest : ExplorerTestContext
{
    private const string ReplaceState = "history.replaceState";

    private static readonly Guid Als = Guid.NewGuid();

    private static readonly Guid Resept = Guid.NewGuid();

    /// <summary>Two kilder, because the drop rule is about leaving one view for another.</summary>
    private sealed class TwoKilderClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
            [
                new KildeSummary { Id = Als, Name = "Als registeret", Code = "K_ALS" },
                new KildeSummary { Id = Resept, Name = "Reseptregisteret", Code = "K_RESEPT" },
            ]);

        public override Task<KildeDetail?> GetKildeAsync(
            Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(new KildeDetail
            {
                Id = id,
                PreferredTerm = id == Als ? "Als registeret" : "Reseptregisteret",
            });
    }

    private static readonly Guid Speech = Guid.NewGuid();

    /// <summary>One variable, found by the search and by id, so a link naming it opens a panel.</summary>
    private sealed class OneVariableClient : EmptyMuninExplorerClient
    {
        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items =
                [
                    new VariableSummary
                    {
                        Id = Speech,
                        Code = "V_ALS.F1.Tale",
                        PreferredTerm = "1. Tale",
                        KildeName = "Als registeret",
                    },
                ],
                TotalCount = 1,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = id,
                PreferredTerm = "1. Tale",
                KildeName = "Als registeret",
            });
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>The last URL a component mirrored, or null when none ever wrote one.</summary>
    private string? Mirrored() =>
        JSInterop.Invocations[ReplaceState] is { Count: > 0 } calls
            ? calls[^1].Arguments[2] as string
            : null;

    /// <remarks>
    /// The client is registered and the renderer info set before the navigation: bUnit seals its
    /// service collection the first time anything is resolved from it, and reaching for the
    /// NavigationManager is a resolve.
    /// </remarks>
    private void Arrive(string url, IMuninExplorerClient client)
    {
        Services.AddSingleton(client);
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        JSInterop.Mode = JSRuntimeMode.Loose;
        Navigation.NavigateTo(url);
    }

    private IRenderedComponent<KildeExplorer> RenderKilder(string url)
    {
        Arrive(url, new TwoKilderClient());

        return Render<KildeExplorer>();
    }

    private IRenderedComponent<VariableExplorer> RenderVariables(string url)
    {
        Arrive(url, new OneVariableClient());

        return Render<VariableExplorer>();
    }

    private static void OpenKilde(IRenderedComponent<KildeExplorer> cut, string name) =>
        cut.FindAll(".munin-explorer-kilder tbody th button")
           .First(button => button.TextContent.Contains(name, StringComparison.Ordinal))
           .Click();

    /// <summary>The way out of a kilde view, which is a button and moves no address of its own.</summary>
    private static void CloseTheDrillIn(IRenderedComponent<KildeExplorer> cut) =>
        cut.Find(".munin-explorer-drilldown > button").Click();

    [Fact]
    public void Mirror_WhenTheReaderJumpsToASectionOfTheOpenKilde_ThenTheAddressStillNamesIt()
    {
        var cut = RenderKilder($"http://localhost/kilder?kilde={Als}");

        cut.WaitForAssertion(() => Assert.Equal($"/kilder?kilde={Als}", Mirrored()));

        // What a contents-nav press is once the hrefs are same-document: the browser scrolls, sets
        // the fragment and tells Blazor. The rewrite that follows is what used to take it back off.
        Navigation.NavigateTo($"/kilder?kilde={Als}#{DetailSectionIds.Metadata}");

        cut.WaitForAssertion(() => Assert.Equal($"/kilder?kilde={Als}#metadata", Mirrored()));
    }

    [Fact]
    public void Mirror_WhenADeepLinkArrivesNamingASection_ThenTheFirstRewriteKeepsIt()
    {
        // The reader who was sent the link rather than the one who made it. Nothing has moved here,
        // so the very first mirror is the one that would erase it.
        var cut = RenderKilder($"http://localhost/kilder?kilde={Als}#{DetailSectionIds.Source}");

        cut.WaitForAssertion(() => Assert.Equal($"/kilder?kilde={Als}#source", Mirrored()));
    }

    [Fact]
    public void Mirror_WhenTheReaderLeavesTheViewTheSectionWasIn_ThenTheFragmentIsGoneForGood()
    {
        var cut = RenderKilder($"http://localhost/kilder?kilde={Als}#{DetailSectionIds.Metadata}");

        cut.WaitForAssertion(() => Assert.Equal($"/kilder?kilde={Als}#metadata", Mirrored()));

        // Back to the list is a button, not a link: this component's own state moves and no
        // navigation builds a fresh mirror, so it is the press the drop rule exists for.
        CloseTheDrillIn(cut);

        cut.WaitForAssertion(() => Assert.Equal("/kilder", Mirrored()));

        OpenKilde(cut, "Reseptregisteret");

        cut.WaitForAssertion(() => Assert.Equal($"/kilder?kilde={Resept}", Mirrored()));

        // And not handed back on the way in again: "metadata" named a section of a view the reader
        // has since left, and the one they return to has drawn its own sections from scratch.
        CloseTheDrillIn(cut);
        OpenKilde(cut, "Als registeret");

        cut.WaitForAssertion(() => Assert.Equal($"/kilder?kilde={Als}", Mirrored()));
    }

    [Fact]
    public void Address_WhenTheAddressNamesASection_ThenNoLinkTheExplorerBuildsInheritsIt()
    {
        var cut = RenderKilder($"http://localhost/kilder?kilde={Als}#{DetailSectionIds.Metadata}");

        cut.WaitForAssertion(() => Assert.Equal($"/kilder?kilde={Als}#metadata", Mirrored()));

        var search = cut.FindComponent<KildeSearch>().Instance;

        Assert.DoesNotContain("#", search.KilderHref!(), StringComparison.Ordinal);
        Assert.DoesNotContain("#", search.KildeDetailHref!(Resept), StringComparison.Ordinal);
        Assert.DoesNotContain("#", search.DatasamlingHref!(Guid.NewGuid()), StringComparison.Ordinal);

        // The contents nav is the one place a '#' belongs, and every entry appends its own id to a
        // fragment-free page address — so a second '#' in any href is the incoming one riding along.
        Assert.All(cut.FindAll("a[href]"), link =>
        {
            var href = link.GetAttribute("href")!;

            Assert.True(href.Count(character => character == '#') <= 1, href);
        });
    }

    [Fact]
    public void Mirror_WhenAVariableDeepLinkNamesASection_ThenTheFirstRewriteKeepsIt()
    {
        // VariableExplorer shares the mirror and so inherits the fix. It has no LocationChanged
        // handler, so the jump this file's first test makes has no equivalent here.
        var cut = RenderVariables(
            $"http://localhost/variabler?variabelId={Speech}#{DetailSectionIds.Statistics}");

        cut.WaitForAssertion(
            () => Assert.Equal($"/variabler?variabelId={Speech}#statistics", Mirrored()));
    }
}
