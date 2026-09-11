using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
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
    /// <remarks>
    /// And one datasamling under it, because the drill-in Kelda offers out of a kilde is a link
    /// this component builds the address for — the tree has to have a node to hang it on.
    /// </remarks>
    private sealed class OneKildeClient(Guid id, Guid? datasamling = null) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>([Kilde(id, "Als registeret")]);

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(new KildeDetail { Id = id, PreferredTerm = "Als registeret" });

        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(datasamling is { } open
                ? new KildeHierarchy
                {
                    KildeId = id,
                    DirectDatasamlinger = [new() { Id = open, Name = "Inklusjon" }]
                }
                : new KildeHierarchy { KildeId = id });

        public override Task<DatasamlingDetail?> GetDatasamlingAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<DatasamlingDetail?>(new() { Id = id, Code = "K_ALS.INKLUSJON", PreferredTerm = "Inklusjon" });
    }

    /// <summary>The kildeutforsker at <paramref name="url"/>, over a kilde with one datasamling.</summary>
    private IRenderedComponent<KildeExplorer> RenderKilder(Guid kilde, Guid datasamling, string url)
    {
        Services.AddSingleton<IMuninExplorerClient>(new OneKildeClient(kilde, datasamling));
        Prepare();
        Navigation.NavigateTo(url);

        return Render<KildeExplorer>();
    }

    /// <inheritdoc cref="RenderExplorer"/>
    private IRenderedComponent<KildeExplorer> RenderKilder(
        Guid id, string url,
        Action<ComponentParameterCollectionBuilder<KildeExplorer>>? parameters = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new OneKildeClient(id));
        Prepare();
        Navigation.NavigateTo(url);

        return Render<KildeExplorer>(b => parameters?.Invoke(b));
    }

    [Fact]
    public void Kilder_WhenALinkCarriesAKilde_ThenThatKildeIsOpen()
    {
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, $"http://localhost/kilder?kilde={id}");

        Assert.NotEmpty(cut.FindAll(".munin-explorer-drilldown"));
    }

    [Fact]
    public void Kilder_WhenALinkCarriesADatasamlingBesideItsKilde_ThenItOpensAndTheAddressKeepsBoth()
    {
        // The whole claim of ?datasamling=: a link pasted into a fresh tab lands on the same page,
        // and the address it rewrites still names the kilde the reader can go back out to.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?kilde={kilde}&datasamling={datasamling}");

        Assert.NotEmpty(cut.FindAll(".munin-explorer-datasamling"));
        Assert.Equal($"/kilder?kilde={kilde}&datasamling={datasamling}", Mirrored());
    }

    [Fact]
    public void Kilder_WhenALinkCarriesADatasamlingAndNoKilde_ThenItIsDroppedFromTheAddress()
    {
        // A datasamling opens in place of the kilde it belongs to, so one named on its own is a
        // view with no way back out. Dropped rather than opened, and the address says so.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?datasamling={datasamling}");

        Assert.Empty(cut.FindAll(".munin-explorer-datasamling"));
        Assert.Equal("/kilder", Mirrored());
    }

    [Fact]
    public void Kilder_WhenAKildeIsOpen_ThenItsTreesRouteCarriesBothKeysAndTheHostsOwnParameter()
    {
        // The one thing KildeSearch cannot work out for itself: which keys the address carries.
        // A link that dropped the host's own parameter would erase it on the drill-in.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?utm_source=nyhetsbrev&kilde={kilde}");

        Assert.Equal(
            $"/kilder?utm_source=nyhetsbrev&kilde={kilde}&datasamling={datasamling}",
            cut.Find("a.munin-explorer-hierarchy__open").GetAttribute("href"));
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
        // KildeExplorer owns ?kilde= and nothing else, and cannot own ?search=: the search box is
        // KildeSearch's and raises no SearchChanged, so a ?search= adopted here would be erased on
        // the first render after load. Carried through instead, like any key that is not ours.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, $"http://localhost/kilder?search=als&kilde={id}");

        cut.FindAll("button").First(button => button.TextContent.Contains("Tilbake", StringComparison.Ordinal)).Click();

        Assert.Equal("/kilder?search=als", Mirrored());
    }

    /// <summary>The sort control the kilde list hangs above its table.</summary>
    private static IElement Sort(IRenderedComponent<KildeExplorer> cut) =>
        cut.Find("select[id^='munin-explorer-sort']");

    [Theory]
    [InlineData(KildeSortOrder.Name)]
    [InlineData(KildeSortOrder.Variables)]
    [InlineData(KildeSortOrder.SourceUpdated)]
    [InlineData(KildeSortOrder.Established)]
    public void Kilder_WhenTheReaderSortsTheList_ThenTheOrderIsInTheUrlAndComesBackFromIt(KildeSortOrder order)
    {
        // The round trip, both halves in one test and per order: writing a token the component
        // cannot read back is a link that silently opens on the wrong order, and the two halves are
        // written in different files. So this presses the control, reads what reached replaceState,
        // and mounts a second explorer on exactly that URL.
        var id = Guid.NewGuid();

        var chosen = RenderKilder(id, "http://localhost/kilder");

        Sort(chosen).Change(order.ToString());

        Assert.Equal($"/kilder?sort={order}", Mirrored());

        // Mounted afresh on the URL the first one wrote, which is what a reload is. The client is
        // registered already, so this goes through Render rather than through RenderKilder — bUnit
        // seals its service collection the moment anything resolves from it.
        Navigation.NavigateTo($"http://localhost{Mirrored()}");

        Assert.Equal(order.ToString(), Selected(Render<KildeExplorer>()));
    }

    /// <summary>The order the control is showing, read off the option the markup marks.</summary>
    private static string? Selected(IRenderedComponent<KildeExplorer> cut) =>
        Sort(cut).QuerySelectorAll("option").Single(option => option.HasAttribute("selected")).GetAttribute("value");

    /// <summary>The names down the table, which is the only place the order is actually visible.</summary>
    private static IReadOnlyList<string> RowNames(IRenderedComponent<KildeExplorer> cut) =>
        [.. cut.FindAll(".munin-explorer-kilder__name").Select(button => button.TextContent.Trim())];

    /// <summary>Three kilder in an order that is neither alphabetical nor its reverse.</summary>
    private sealed class ThreeKilderClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
            [
                Kilde(Guid.NewGuid(), "Reseptregisteret"),
                Kilde(Guid.NewGuid(), "Als registeret"),
                Kilde(Guid.NewGuid(), "Barnediabetes"),
            ]);
    }

    /// <inheritdoc cref="RenderExplorer"/>
    private IRenderedComponent<KildeExplorer> RenderThreeKilder(string url)
    {
        Services.AddSingleton<IMuninExplorerClient>(new ThreeKilderClient());
        Prepare();
        Navigation.NavigateTo(url);

        return Render<KildeExplorer>();
    }

    [Fact]
    public void Kilder_WhenTheReaderSortsTheList_ThenTheRowsThemselvesMoveAndNotOnlyTheControl()
    {
        // Every other sort test here reads the control or the address bar back, and both would go
        // on agreeing with a component that never handed the order down to the table it draws.
        var cut = RenderThreeKilder("http://localhost/kilder");

        Assert.Equal(["Reseptregisteret", "Als registeret", "Barnediabetes"], RowNames(cut));

        Sort(cut).Change(KildeSortOrder.Name.ToString());

        Assert.Equal(["Als registeret", "Barnediabetes", "Reseptregisteret"], RowNames(cut));
    }

    [Fact]
    public void Kilder_WhenALinkCarriesAnOrder_ThenTheRowsArriveInItOnTheFirstPaint()
    {
        // The other half of the same thread, and the half a control-only assertion cannot see: the
        // order has to reach KildeSearch's Order parameter before the child reads it, so a parent
        // that set its own field after that read would still show a control naming this order.
        var cut = RenderThreeKilder("http://localhost/kilder?sort=Name");

        Assert.Equal(["Als registeret", "Barnediabetes", "Reseptregisteret"], RowNames(cut));
    }

    [Fact]
    public void Kilder_WhenTheListIsInTheOrderItArrivedIn_ThenNothingIsWrittenToTheUrl()
    {
        // The catalogue's own order is the one every link made before this control existed carries,
        // so writing ?sort=Standard onto it would be this component changing links it did not make.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, "http://localhost/kilder");

        Sort(cut).Change(KildeSortOrder.Name.ToString());
        Sort(cut).Change(KildeSortOrder.Standard.ToString());

        Assert.Equal("/kilder", Mirrored());
    }

    [Fact]
    public void Kilder_WhenALinkCarriesBothAKildeAndAnOrder_ThenBothSurviveClosingTheKilde()
    {
        // The two keys are written by one method, and a component that rebuilt the query from the
        // kilde alone would drop the order the moment the reader pressed Back — which reads as the
        // list resetting itself for no reason.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, $"http://localhost/kilder?kilde={id}&sort=Variables");

        cut.FindAll("button").First(button => button.TextContent.Contains("Tilbake", StringComparison.Ordinal)).Click();

        Assert.Equal("/kilder?sort=Variables", Mirrored());
    }

    [Theory]
    // Enum.TryParse alone accepts any number, so 999 would arrive as an order no arm covers.
    [InlineData("999")]
    [InlineData("Flest variabler")]
    [InlineData("")]
    public void Kilder_WhenALinkCarriesAnOrderNobodyDefined_ThenTheListOpensInTheOrderItArrivedIn(string token)
    {
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, $"http://localhost/kilder?sort={Uri.EscapeDataString(token)}");

        Assert.Equal(KildeSortOrder.Standard.ToString(), Selected(cut));

        // And the unreadable token is taken off the address bar rather than carried: it is one of
        // ours, so leaving it would hand on a link that says the list is in an order it is not in.
        Assert.Equal("/kilder", Mirrored());
    }

    [Fact]
    public void Kilder_WhenALinkSpellsTheOrderInAnotherCase_ThenItIsStillRead()
    {
        // ExplorerUrlState parses its own enums case-insensitively, and a URL is typed by hand as
        // often as it is copied.
        var id = Guid.NewGuid();

        var cut = RenderKilder(id, "http://localhost/kilder?SORT=variables");

        Assert.Equal(KildeSortOrder.Variables.ToString(), Selected(cut));
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

    /// <summary>
    /// What a host's <c>Router</c> does to a standing circuit: the address moves and nothing
    /// reloads, which is the one event <c>KildeExplorer.Moved</c> exists for.
    /// </summary>
    /// <remarks>
    /// Every other test here navigates and <em>then</em> renders, which is the fresh-mount path a
    /// router-less host takes. Nothing below mounts anything after moving: the component is already
    /// standing, exactly as it is on helsedata once the circuit is up.
    /// </remarks>
    private void Move(string url) => Navigation.NavigateTo(url);

    /// <summary>Every navigation this test's browser has made, so one nobody asked for is visible.</summary>
    private int Moves => ((BunitNavigationManager)Navigation).History.Count;

    /// <summary>Two kilder, a datasamling under the first, for drilling in and coming back out.</summary>
    private sealed class TwoKilderClient(Guid first, Guid second, Guid datasamling) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
                [Kilde(first, "Als registeret"), Kilde(second, "Reseptregisteret")]);

        public override Task<KildeDetail?> GetKildeAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeDetail?>(new KildeDetail { Id = id, PreferredTerm = "Als registeret" });

        public override Task<KildeHierarchy?> GetKildeHierarchyAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<KildeHierarchy?>(new KildeHierarchy
            {
                KildeId = id,
                DirectDatasamlinger = id == first ? [new() { Id = datasamling, Name = "Inklusjon" }] : [],
            });

        public override Task<DatasamlingDetail?> GetDatasamlingAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<DatasamlingDetail?>(new() { Id = id, Code = "K_ALS.INKLUSJON", PreferredTerm = "Inklusjon" });
    }

    /// <inheritdoc cref="RenderExplorer"/>
    private IRenderedComponent<KildeExplorer> RenderTwoKilder(
        Guid first, Guid second, Guid datasamling, string url)
    {
        Services.AddSingleton<IMuninExplorerClient>(new TwoKilderClient(first, second, datasamling));
        Prepare();
        Navigation.NavigateTo(url);

        return Render<KildeExplorer>();
    }

    [Fact]
    public void Moved_WhenARouterInterceptsTheDrillIn_ThenTheStandingComponentDrawsWhatTheNewAddressNames()
    {
        // The whole of the second half of this change. Without it the address bar says
        // ?datasamling= and the view underneath is still the kilde, because the query is read at
        // initialisation and a router leaves the component standing.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?kilde={kilde}");

        Assert.Empty(cut.FindAll(".munin-explorer-datasamling"));

        Move($"/kilder?kilde={kilde}&datasamling={datasamling}");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".munin-explorer-datasamling")));
    }

    [Fact]
    public void Moved_WhenTheReaderPressesBackOutOfADatasamling_ThenTheKildeIsDrawnAgainWithoutALoad()
    {
        // The other direction, and the reason a real link was chosen: Back has to mean what it
        // means everywhere else, and it arrives here as a LocationChanged and nothing else.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?kilde={kilde}&datasamling={datasamling}");

        Move($"/kilder?kilde={kilde}");

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".munin-explorer-datasamling"));
            Assert.NotEmpty(cut.FindAll(".munin-explorer-hierarchy"));
        });
    }

    [Fact]
    public void Moved_WhenItReadsTheNewAddress_ThenItNavigatesNowhereItself()
    {
        // Every navigation clears the browser's forward list, so a reload issued from here would
        // take away the one thing a real link was chosen to keep — and one slipping in later
        // would leave every other assertion in this file passing.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?kilde={kilde}");

        var before = Moves;

        Move($"/kilder?kilde={kilde}&datasamling={datasamling}");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".munin-explorer-datasamling")));

        // The reader's own move and nothing behind it.
        Assert.Equal(before + 1, Moves);
        Assert.Equal($"http://localhost/kilder?kilde={kilde}&datasamling={datasamling}", Navigation.Uri);
    }

    [Fact]
    public void Moved_WhenTheNavigationIsToAnotherPage_ThenThisOneLeavesItAlone()
    {
        // A host's Router raises LocationChanged for every page it serves, not only for this one.
        // Without the path guard the explorer would read another page's query as its own and
        // rewrite that page's address bar on the way out.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?kilde={kilde}&datasamling={datasamling}");

        var written = JSInterop.Invocations[ReplaceState].Count;

        Move($"/variabler?kilde={Guid.NewGuid()}");

        Assert.NotEmpty(cut.FindAll(".munin-explorer-datasamling"));
        Assert.Equal(written, JSInterop.Invocations[ReplaceState].Count);
        Assert.Equal($"/kilder?kilde={kilde}&datasamling={datasamling}", Mirrored());
    }

    [Fact]
    public void Moved_WhenOnlyTheHostsOwnParametersChange_ThenTheyAreCarriedForwardRatherThanPutBack()
    {
        // The early-return path, which is the one that looks harmless. The owned keys are equal, so
        // nothing is redrawn — but the mirror holding the host's parameters has to be taken anyway,
        // or every later link and every later rewrite restores the value the navigation dropped.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?utm_source=a&kilde={kilde}");

        Move($"/kilder?utm_source=b&kilde={kilde}");

        // A Router re-renders the page it serves, so the drill-in's route is rebuilt from the
        // address that arrived rather than from the one the component mounted on.
        cut.Render();

        Assert.Equal(
            $"/kilder?utm_source=b&kilde={kilde}&datasamling={datasamling}",
            cut.Find("a.munin-explorer-hierarchy__open").GetAttribute("href"));

        // And the next owned change writes the host's new value back rather than erasing it.
        cut.FindAll("button").First(button => button.TextContent.Contains("Tilbake", StringComparison.Ordinal)).Click();

        Assert.Equal("/kilder?utm_source=b", Mirrored());
    }

    [Fact]
    public void Moved_WhenAnOwnedKeyChangesToo_ThenTheHostsArrivingParametersAreTheOnesMirrored()
    {
        // The same refresh on the path that does redraw. A mirror left pinned to the mounting
        // address would put utm_source=a back the moment anything rewrote the URL.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?utm_source=a&kilde={kilde}");

        Move($"/kilder?utm_source=b&kilde={kilde}&datasamling={datasamling}");

        cut.WaitForAssertion(
            () => Assert.Equal($"/kilder?utm_source=b&kilde={kilde}&datasamling={datasamling}", Mirrored()));
    }

    /// <summary>Whoever is listening for a navigation, read off the event itself.</summary>
    /// <remarks>
    /// Reflection because a leak has no other symptom: rendering a disposed component is a no-op,
    /// so a handler left behind costs nothing any assertion on a view could see. The field is
    /// asserted to exist, so a framework renaming it fails here rather than reporting none ever.
    /// </remarks>
    private static IReadOnlyList<object> Listeners(NavigationManager navigation)
    {
        var field = typeof(NavigationManager).GetField(
            "_locationChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        return field.GetValue(navigation) is EventHandler<LocationChangedEventArgs> subscribed
            ? [.. subscribed.GetInvocationList().Select(handler => handler.Target!)]
            : [];
    }

    [Fact]
    public void Moved_WhenTheComponentIsGone_ThenItIsNotStillListeningForNavigations()
    {
        // LocationChanged belongs to the host and outlives every component that touches it. A
        // handler left behind keeps this one alive with it, and reads the next page's address as
        // its own — silently, because rendering a disposed component does nothing at all.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?kilde={kilde}");

        var explorer = cut.Instance;

        Assert.Contains(explorer, Listeners(Navigation));

        // The call the renderer makes when a host's Router leaves the page, made directly: the
        // renderer's own DisposeComponents queues it and returns, which is a race from here.
        ((IDisposable)explorer).Dispose();

        Assert.DoesNotContain(explorer, Listeners(Navigation));
        Assert.Null(Record.Exception(() => Move($"/kilder?kilde={kilde}&datasamling={datasamling}")));
    }

    [Fact]
    public async Task Kilder_WhenAnotherKildeIsReportedWhileADatasamlingIsOpen_ThenTheDatasamlingGoesWithIt()
    {
        // Driven at the seam KildeChanged guards: SelectedKildeIdChanged is a public parameter and
        // says only which kilde is open. Nothing composed today raises it with a datasamling still
        // held here, so the assignment is an invariant — and this is what notices it going.
        var kilde = Guid.NewGuid();
        var datasamling = Guid.NewGuid();
        var second = Guid.NewGuid();

        var cut = RenderKilder(kilde, datasamling, $"http://localhost/kilder?kilde={kilde}&datasamling={datasamling}");
        var reported = cut.FindComponent<KildeSearch>().Instance.SelectedKildeIdChanged;

        await cut.InvokeAsync(() => reported.InvokeAsync(second));

        Assert.Equal($"/kilder?kilde={second}", Mirrored());
    }

    [Fact]
    public void Kilder_WhenAnotherKildeIsOpenedAfterComingBackOutOfADatasamling_ThenTheAddressCarriesNoDatasamling()
    {
        // The pair KildeChanged keeps in step. A datasamling id that outlived the kilde it belongs
        // to would sit in a field nothing draws, go into every address this component writes, and
        // be what Moved compares the next arriving one against.
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var datasamling = Guid.NewGuid();

        var cut = RenderTwoKilder(first, second, datasamling, $"http://localhost/kilder?kilde={first}");

        // In, and back out to the kilde, the way the two links in the markup go.
        Move($"/kilder?kilde={first}&datasamling={datasamling}");
        Move($"/kilder?kilde={first}");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".munin-explorer-hierarchy")));

        cut.FindAll("button").First(button => button.TextContent.Contains("Tilbake", StringComparison.Ordinal)).Click();

        cut.FindAll(".munin-explorer-kilder__name")[1].Click();

        Assert.Equal($"/kilder?kilde={second}", Mirrored());
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

        var cut = Render<KildeExplorer>(b => b.Add(c => c.VariableExplorerPath, "/variabler"));

        cut.Find(".munin-explorer-kilder__select input").Change(true);
        cut.FindAll("button").First(button => button.TextContent.Contains("Utforsk", StringComparison.Ordinal)).Click();

        Assert.StartsWith("http://localhost/optimizely/variabler?kildeIds=", navigation.Went, StringComparison.Ordinal);
    }
}
