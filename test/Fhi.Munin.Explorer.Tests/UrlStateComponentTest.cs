using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The two components that put explorer state in a host's address bar, and the promise that makes
/// them worth shipping: the host writes none of this itself and loses nothing by mounting them.
/// </summary>
/// <remarks>
/// Every failure here is silent in a browser. A key that is written where it should have been left
/// alone erases a parameter nobody notices until a campaign link stops attributing; a mount at the
/// wrong render mode draws a working explorer whose URL simply never moves. So the assertions are
/// on what reached <c>history.replaceState</c>, not on what the page looks like.
/// </remarks>
public class UrlStateComponentTest : BunitContext
{
    private const string ReplaceState = "history.replaceState";

    /// <summary>Cleared by the one test that mounts a component the way a host must not.</summary>
    private bool _interactive = true;

    /// <summary>
    /// The render mode both components require, and the loose JS runtime that lets
    /// <c>history.replaceState</c> through.
    /// </summary>
    /// <remarks>
    /// Called after the client is registered rather than from the constructor: bUnit seals its
    /// service collection the first time anything is resolved from it, and setting the renderer
    /// info resolves the renderer.
    /// </remarks>
    private void Prepare()
    {
        SetRendererInfo(new RendererInfo(_interactive ? "Server" : "Static", _interactive));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>Records what the explorer asked the API for, which is what a link has to restore.</summary>
    private sealed class RecordingClient : EmptyMuninExplorerClient
    {
        public string? LastSearch { get; private set; }

        public VariableFilter? LastFilter { get; private set; }

        public int LastPage { get; private set; }

        public int LastPageSize { get; private set; }

        public SortField LastSort { get; private set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            LastSearch = search;
            LastFilter = filter;
            LastPage = page;
            LastPageSize = pageSize;
            LastSort = sort;

            return Task.FromResult(new Page<VariableSummary>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = page,
                Size = pageSize,
                TotalPages = 0,
            });
        }
    }

    /// <summary>The last URL the component wrote, or null when it never wrote one.</summary>
    private string? Mirrored() =>
        JSInterop.Invocations[ReplaceState] is { Count: > 0 } calls
            ? calls[^1].Arguments[2] as string
            : null;

    /// <summary>
    /// Registers the client, puts the browser at <paramref name="url"/>, and renders.
    /// </summary>
    /// <remarks>
    /// In that order, and not by accident: bUnit seals its service collection the first time
    /// anything is resolved from it, and reaching for the NavigationManager is a resolve.
    /// </remarks>
    private RecordingClient RenderExplorer(
        string url,
        out IRenderedComponent<VariableExplorer> cut,
        Action<ComponentParameterCollectionBuilder<VariableExplorer>>? parameters = null)
    {
        var client = new RecordingClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Prepare();
        Navigation.NavigateTo(url);
        cut = Render<VariableExplorer>(b => parameters?.Invoke(b));

        return client;
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Restore_WhenALinkCarriesASearch_ThenTheExplorerOpensOnItWithNoHostCode()
    {
        // The whole claim, in one test: a host mounts the component and nothing else, and a shared
        // link opens the search it was copied from.
        var client = RenderExplorer(
            "http://localhost/?search=svelging&page=3&pageSize=50&sort=Kilde&kildeType=biobank", out _);

        Assert.Equal("svelging", client.LastSearch);
        Assert.Equal(3, client.LastPage);
        Assert.Equal(50, client.LastPageSize);
        Assert.Equal(SortField.Kilde, client.LastSort);
        Assert.Equal("biobank", client.LastFilter?.KildeType);
    }

    [Fact]
    public void Mirror_WhenTheReaderSearches_ThenTheAddressBarFollows()
    {
        RenderExplorer("http://localhost/", out var cut);

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal("/?search=svelging", Mirrored());
    }

    [Fact]
    public void Mirror_WhenTheHostMountsTheExplorerUnderASubPath_ThenTheMirroredUrlKeepsThatPath()
    {
        // replaceState resolves a relative URL against the document's <base href>, not against the
        // page being viewed. A mirrored "?search=" would therefore land wherever that href points —
        // the app root on most hosts, its path base on others — and never on this page.
        RenderExplorer("http://localhost/MuninRuna", out var cut);

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.Equal("/MuninRuna?search=svelging", Mirrored());
    }

    [Fact]
    public void Mirror_WhenTheHostHasParametersOfItsOwn_ThenTheySurviveAChange()
    {
        // The failure the 90-line sample wrapper had and nothing caught: it wrote "?" + its own
        // query, so the first render after load dropped every parameter the host cared about.
        RenderExplorer("http://localhost/?utm_source=nyhetsbrev&search=svelging", out var cut);

        cut.Find(".searchbox__freetext").Change("diabetes");
        cut.Find("form").Submit();

        Assert.Equal("/?utm_source=nyhetsbrev&search=diabetes", Mirrored());
    }

    [Fact]
    public void Mirror_WhenAKeyIsDeclined_ThenItIsNeitherReadNorRewritten()
    {
        // A host whose page already means something else by ?search=. Declining it does not take
        // the search box away — it keeps that word out of the link, and leaves the host's own
        // meaning of the parameter exactly where it was.
        var client = RenderExplorer(
            "http://localhost/?search=hostens+egen&page=2", out var cut,
            b => b.Add(c => c.DeclinedKeys, ["search"]));

        Assert.Null(client.LastSearch);
        Assert.Equal(2, client.LastPage);

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        var mirrored = Mirrored();

        Assert.Contains("search=hostens+egen", mirrored, StringComparison.Ordinal);
        Assert.DoesNotContain("svelging", mirrored, StringComparison.Ordinal);
    }

    [Fact]
    public void DeclinedKeys_WhenItNamesAKeyThatIsNotDeclinable_ThenItSaysSoRatherThanDoingNothing()
    {
        // A facet key cannot be declined — half a filter in a URL describes a search nobody is
        // looking at — and a typo is the same mistake. Both would otherwise be silent.
        var thrown = Assert.Throws<ArgumentException>(
            () => RenderExplorer("http://localhost/", out _, b => b.Add(c => c.DeclinedKeys, ["kildeIds"])));

        Assert.Contains("kildeIds", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mount_WhenItIsPrerendered_ThenItFailsLoudlyRatherThanSilentlyNeverFollowing()
    {
        // The trap this component exists to close. Prerendered, nothing calls into the browser and
        // no callback fires: the page renders, the URL never moves, and there is nothing to search
        // for. An exception on initialisation names the render mode to change instead.
        _interactive = false;

        var thrown = Assert.Throws<InvalidOperationException>(
            () => RenderExplorer("http://localhost/", out _));

        Assert.Contains("render-mode=\"Server\"", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mirror_WhenNothingIsSet_ThenTheQueryIsClearedRatherThanLeftBehind()
    {
        // Assigning "" to replaceState leaves the previous query in place, so the path itself is
        // what clears it — and it is the mounted path, PathBase included, not "/".
        RenderExplorer("http://localhost/app/variabler?search=svelging", out var cut);

        cut.Find(".searchbox__freetext").Change("");
        cut.Find("form").Submit();

        Assert.Equal("/app/variabler", Mirrored());
    }

    [Fact]
    public void Mirror_WhenTheStateHasNotChanged_ThenTheAddressBarIsNotWrittenTwice()
    {
        RenderExplorer("http://localhost/?search=svelging", out var cut);

        var written = JSInterop.Invocations[ReplaceState].Count;

        cut.Render();

        Assert.Equal(written, JSInterop.Invocations[ReplaceState].Count);
    }

    [Fact]
    public void Mirror_WhenTheReaderChangesTheView_ThenNoHistoryEntryIsPushed()
    {
        // pushState would make every filter change a step the reader has to walk back through
        // before they can leave the site.
        RenderExplorer("http://localhost/", out var cut);

        cut.Find(".searchbox__freetext").Change("svelging");
        cut.Find("form").Submit();

        Assert.NotEmpty(JSInterop.Invocations[ReplaceState]);
        Assert.Empty(JSInterop.Invocations["history.pushState"]);
    }

    private static VariableSummary Variable(Guid id, string name) => new()
    {
        Id = id,
        Code = "V_ALS.F1." + name,
        PreferredTerm = name,
        KildeName = "Als registeret",
    };

    /// <summary>One page of two variables, so there is a row to open and one to leave closed.</summary>
    private sealed class TwoVariableClient : EmptyMuninExplorerClient
    {
        public static readonly Guid SpeechId = Guid.NewGuid();

        public static readonly Guid SalivaId = Guid.NewGuid();

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = [Variable(SpeechId, "1. Tale"), Variable(SalivaId, "2. Spyttsekresjon")],
                TotalCount = 2,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<VariableDetail?>(new VariableDetail
            {
                Id = id,
                PreferredTerm = id == SpeechId ? "1. Tale" : "2. Spyttsekresjon",
            });
    }

    /// <inheritdoc cref="RenderExplorer"/>
    private IRenderedComponent<VariableExplorer> RenderVariables(
        string url,
        Action<ComponentParameterCollectionBuilder<VariableExplorer>>? parameters = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new TwoVariableClient());
        Services.AddScoped<VariableListState>();
        Prepare();
        Navigation.NavigateTo(url);

        return Render<VariableExplorer>(b => parameters?.Invoke(b));
    }

    /// <summary>The rows, whose names are the disclosures that open a variable.</summary>
    private static IReadOnlyList<IElement> Rows(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name");

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public void Save_WhenTheHostSaysWhoTheReaderIs_ThenItReachesTheExplorerRatherThanBeingDropped(
        bool signedIn, int buttons)
    {
        // Signed out the button is absent either way, which is why this was invisible: the wrapper
        // declared no IsAuthenticated at all, so mounting it cost every host its saved lists and
        // the host could not put it back. (Fhi.Metadata-l1f2s)
        var cut = RenderVariables("http://localhost/variabler", b => b.Add(c => c.IsAuthenticated, signedIn));

        Assert.Equal(buttons, cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]").Count);
    }

    [Fact]
    public void Heading_WhenTheHostSetsTheLevel_ThenItReachesTheExplorerToo()
    {
        // The level that keeps a page outline unbroken is only knowable at the mount site, and a
        // wrapper that swallowed it would force an h2 under whatever the host's last heading was.
        var cut = RenderVariables("http://localhost/variabler", b => b.Add(c => c.HeadingLevel, 3));

        Assert.Equal("Variabelutforsker", cut.Find("h3").TextContent);
    }

    [Fact]
    public void Selection_WhenTheReaderOpensAVariable_ThenThereIsSomethingToCopy()
    {
        var cut = RenderVariables("http://localhost/variabler");

        Rows(cut)[0].Click();

        Assert.Equal($"/variabler?variabelId={TwoVariableClient.SpeechId}", Mirrored());
    }

    [Fact]
    public void Selection_WhenALinkCarriesAVariable_ThenItOpensWithTheSearchAroundItIntact()
    {
        var cut = RenderVariables(
            $"http://localhost/variabler?search=svelging&variabelId={TwoVariableClient.SalivaId}");

        Assert.Equal("true", Rows(cut)[1].GetAttribute("aria-expanded"));
        Assert.NotEmpty(cut.FindAll(".munin-explorer-detail"));
    }

    [Fact]
    public void Selection_WhenAHostDeclinesTheVariableKey_ThenItsOwnValueIsLeftWhereItIs()
    {
        // Declinable for the reason ?search= is: a host with a variable page of its own may already
        // mean something by ?variabelId=. Declining it does not close the panel, only keep it out
        // of the link.
        var cut = RenderVariables("http://localhost/variabler?variabelId=vertens-egen",
                                  b => b.Add(c => c.DeclinedKeys, ["variabelId"]));

        Rows(cut)[0].Click();

        Assert.NotEmpty(cut.FindAll(".munin-explorer-detail"));
        Assert.Equal("/variabler?variabelId=vertens-egen", Mirrored());
    }

    [Fact]
    public void Selection_WhenTheReaderClosesTheVariable_ThenTheKeyGoesRatherThanGoingStale()
    {
        // A URL still naming a closed variable sends the next reader somewhere the sender was not.
        var cut = RenderVariables(
            $"http://localhost/variabler?search=svelging&variabelId={TwoVariableClient.SpeechId}");

        Rows(cut)[0].Click();

        Assert.Equal("/variabler?search=svelging", Mirrored());
    }

    /// <summary>A navigation manager mounted under a path base, which bUnit's own cannot be.</summary>
    private sealed class BasedNavigationManager : NavigationManager
    {
        public BasedNavigationManager(string baseUri, string uri) => Initialize(baseUri, uri);

        /// <summary>Where the component asked to go, absolute, or null if it never asked.</summary>
        public string? Went { get; private set; }

        protected override void NavigateToCore(string uri, bool forceLoad) => Went = ToAbsoluteUri(uri).ToString();
    }

    private static KildeSummary Kilde(Guid id, string name) => new() { Id = id, Name = name, Code = "K" };

    /// <summary>Answers with one kilde, so there is a row to open and a selection to hand over.</summary>
    private sealed class OneKildeClient(Guid id) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>([Kilde(id, "Als registeret")]);

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(new KildeDetail { Id = id, PreferredTerm = "Als registeret" });
    }

    /// <inheritdoc cref="RenderExplorer"/>
    private IRenderedComponent<KildeExplorerWithUrlState> RenderKilder(
        Guid id, string url,
        Action<ComponentParameterCollectionBuilder<KildeExplorerWithUrlState>>? parameters = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new OneKildeClient(id));
        Prepare();
        Navigation.NavigateTo(url);

        return Render<KildeExplorerWithUrlState>(b => parameters?.Invoke(b));
    }

    [Fact]
    public void Kilder_WhenALinkCarriesAKilde_ThenThatKildeIsOpen()
    {
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, $"http://localhost/kilder?kilde={id}");

        Assert.NotEmpty(cut.FindAll(".munin-explorer-drilldown"));
    }

    [Fact]
    public void Kilder_WhenTheReaderOpensAKildeUnderASubPath_ThenOnlyTheQueryChanges()
    {
        // The same trap as the variable explorer's, asserted while the state is set rather than
        // after it is cleared: the clear branch has always written the path and hides this.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, "http://localhost/MuninKelda");

        cut.Find(".munin-explorer-kilder__name").Click();

        Assert.Equal($"/MuninKelda?kilde={id}", Mirrored());
    }

    [Fact]
    public void Kilder_WhenTheReaderClosesTheKilde_ThenThePathTheyArrivedOnComesBackWithItsPathBase()
    {
        // Trap 2, which is invisible locally: replaceState writes an absolute path, so a component
        // that cleared the query by writing "/" would send a reader behind a reverse proxy — which
        // is where helsedata runs — out of the application entirely.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, $"http://localhost/optimizely/kilder?kilde={id}");

        cut.FindAll("button").First(button => button.TextContent.Contains("Tilbake", StringComparison.Ordinal)).Click();

        Assert.Equal("/optimizely/kilder", Mirrored());
    }

    [Fact]
    public void Kilder_WhenTheLinkCarriesASearchKeldaCannotMaintain_ThenItIsLeftAloneRatherThanErased()
    {
        // KildeExplorer has no SearchChanged, so a ?search= this component adopted would be erased
        // on the first render after load: the link would work exactly once and could not be shared
        // onward. It is carried through instead, like any parameter that is not ours.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, $"http://localhost/kilder?search=als&kilde={id}");

        cut.FindAll("button").First(button => button.TextContent.Contains("Tilbake", StringComparison.Ordinal)).Click();

        Assert.Equal("/kilder?search=als", Mirrored());
    }

    [Fact]
    public void Kilder_WhenNoVariableExplorerPathIsGiven_ThenNoHandoverIsOffered()
    {
        // The package cannot know where a host mounted the other explorer, and a selection column
        // leading nowhere is worse than none.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, "http://localhost/kilder");

        Assert.Empty(cut.FindAll(".munin-explorer-kilder__select"));
    }

    [Fact]
    public void Kilder_WhenAVariableExplorerPathIsGiven_ThenTheSelectionColumnIsOffered()
    {
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, "http://localhost/kilder",
                               b => b.Add(c => c.VariableExplorerPath, "/"));

        Assert.NotEmpty(cut.FindAll(".munin-explorer-kilder__select"));
    }

    [Fact]
    public void Kilder_WhenTheReaderHandsTheSelectionOver_ThenItArrivesAsTheFilterQueryTheOtherExplorerReads()
    {
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, "http://localhost/kilder",
                               b => b.Add(c => c.VariableExplorerPath, "/variabler"));

        cut.Find(".munin-explorer-kilder__select input").Change(true);
        cut.FindAll("button").First(button => button.TextContent.Contains("Utforsk", StringComparison.Ordinal)).Click();

        Assert.Equal(
            $"http://localhost/variabler?kildeIds={id}",
            Navigation.Uri);
    }

    [Fact]
    public void Kilder_WhenTheHostIsMountedUnderAPathBase_ThenTheHandoverStaysInsideTheApplication()
    {
        // Closing a kilde has to put the path base back, and so does the handover: NavigateTo with
        // a leading slash resolves against the origin, not the application, so "/variabler" would
        // send the reader outside it. Identical locally, wrong behind the reverse proxy helsedata
        // runs behind — the same shape as the trap the mirror avoids by reading the circuit's URI.
        var id = Guid.NewGuid();
        var navigation = new BasedNavigationManager(
            "http://localhost/optimizely/", "http://localhost/optimizely/kilder");

        Services.AddSingleton<IMuninExplorerClient>(new OneKildeClient(id));
        Services.AddSingleton<NavigationManager>(navigation);
        Prepare();

        var cut = Render<KildeExplorerWithUrlState>(b => b.Add(c => c.VariableExplorerPath, "/variabler"));

        cut.Find(".munin-explorer-kilder__select input").Change(true);
        cut.FindAll("button").First(button => button.TextContent.Contains("Utforsk", StringComparison.Ordinal)).Click();

        Assert.StartsWith("http://localhost/optimizely/variabler?kildeIds=", navigation.Went, StringComparison.Ordinal);
    }
}
