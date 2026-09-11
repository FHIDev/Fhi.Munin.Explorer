using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The kildeutforsker: the kilde list, the drill-in, and a link that reopens the kilde that was on
/// screen when it was copied.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one component a host mounts.</b> <see cref="Language"/> is the whole of what it needs —
/// which is what helsedata's <c>BlazorComponentPage</c> offers — and the name a host reaches for
/// first is therefore the one that works, as it is for <see cref="VariableExplorer"/>.
/// <see cref="KildeSearch"/> stays public underneath for a host that wants to own its own query
/// string or lay the surface out itself.
/// </para>
/// <para>
/// The kildeutforsker's half of what <see cref="VariableExplorer"/> does for the variable side, and
/// much smaller because Kelda carries less: the open kilde, the datasamling opened out of it and
/// the order the list is in are the parts of the view worth linking to, and there are no personal
/// lists to put behind a second tab. A link opens that kilde; closing it puts the reader back on
/// the path they arrived on, <c>PathBase</c> included, rather than on the site root.
/// </para>
/// <para>
/// <b>It reads and writes <c>?kilde=</c>, <c>?datasamling=</c> and <c>?sort=</c>, and nothing
/// else.</b> A host's own parameters — and <c>?search=</c>, which Kelda cannot maintain and so must
/// not adopt — are carried through untouched. <c>?sort=</c> is omitted while the list is in the
/// order the catalogue sent it, so a link made before this component could sort still opens the
/// same page.
/// </para>
/// <para>
/// <b>Opening a datasamling is a link.</b> That is what buys middle-click, Ctrl+click and working
/// Back and Forward buttons in a package with no router of its own. A host with no router follows
/// it as an ordinary page load; a host with a <c>Router</c> intercepts the press, and this
/// component reads the new address itself rather than leaving it ahead of the view — see
/// <c>Moved</c>. Either way the kilde's hierarchy comes back collapsed.
/// </para>
/// <para>
/// <b>It must be mounted interactively</b> — <c>render-mode="Server"</c> in a legacy Blazor Server
/// host, <c>@rendermode</c> with <c>prerender: false</c> in a modern one — and it throws on
/// initialisation if it is not. This component is where that mattered first: an
/// <see cref="EventCallback"/> created in a statically rendered parent serialises to an empty
/// delegate, so a host wrapper raising <see cref="KildeSearch.SelectedKildeIdChanged"/> across
/// that boundary was silently dead. The callbacks are created inside this component instead, which
/// is why <see cref="VariableExplorerPath"/> is a path and not a delegate.
/// </para>
/// </remarks>
public sealed partial class KildeExplorer : ComponentBase, IDisposable
{
    /// <summary>The query key this component owns: the id of the kilde the reader has open.</summary>
    public const string QueryKey = "kilde";

    /// <summary>
    /// The second key it owns: the datasamling the reader opened out of that kilde.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="QueryKey"/> rather than instead of it, so the address names the whole path
    /// the reader walked: closing the datasamling, refreshing, and pressing Back all have the kilde
    /// to return to. It is therefore only honoured with a kilde — <c>?datasamling=</c> on its own
    /// names a page this explorer has no way back out of, and is dropped from the address bar on
    /// the first render.
    /// </remarks>
    public const string DatasamlingQueryKey = "datasamling";

    /// <summary>The third key it owns: the order the kilde list is in.</summary>
    /// <remarks>
    /// <c>sort</c>, spelled and read exactly as <see cref="ExplorerUrlState"/> spells it, and
    /// carrying a <see cref="KildeSortOrder"/> member's own name. The catalogue's own order is
    /// never written, so a link made before this component could sort still means what it did.
    /// <para>
    /// It is a name a host may plausibly already mean something by, as <c>page</c> and
    /// <c>search</c> are on the variable side. There is no declining it here — <c>?sort=</c> is
    /// read and rewritten whatever else on the page means by it — so a host with a sort of its own
    /// on this page mounts <see cref="KildeSearch"/> and owns the query string itself.
    /// </para>
    /// </remarks>
    public const string OrderQueryKey = "sort";

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc cref="KildeSearch.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="KildeSearch.ShowAccessAndPrices"/>
    /// <remarks>
    /// Declared here as well as on <see cref="KildeSearch"/>, and forwarded, because this is the
    /// mount a CMS host names: a parameter the mounted type does not declare is dropped before it
    /// is set, silently and with everything still compiling.
    /// </remarks>
    [Parameter] public bool ShowAccessAndPrices { get; set; }

    /// <summary>
    /// Where the host mounted <see cref="VariableSearch"/>, so the chosen kilder can be handed
    /// over to it. Leave it null and the selection column is not offered at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one thing only the host knows, and the reason it is a string: a delegate given to this
    /// component by a statically rendered parent would arrive empty. The handover is a full page
    /// load, because this package has no router and cannot know whether the host has one.
    /// </para>
    /// <para>
    /// <b>Relative to the application, not to the domain.</b> <c>"variabler"</c> and
    /// <c>"/variabler"</c> both mean the same page of your application; the leading slash does not
    /// send the reader to the domain root, because this resolves against
    /// <see cref="NavigationManager.BaseUri"/> so that a host mounted under a path base keeps it. A
    /// full URL is taken as given, for the host whose other explorer is in another application.
    /// </para>
    /// <para>
    /// <b>A CMS host cannot set it, and null is the right answer there.</b> helsedata's
    /// <c>BlazorComponentPage</c> offers a fixed candidate list — <c>Language</c>, <c>SkjemaId</c>,
    /// <c>IsAuthenticated</c> — and drops every name outside it, so this parameter is reachable
    /// only from a host that writes the mount itself. Defaulting it to a guess such as
    /// <c>"variabler"</c> would be worse than leaving it null: the column would be drawn and its
    /// button would land on a page that host may not have. Null draws no column, which is the same
    /// page that mount renders today.
    /// </para>
    /// </remarks>
    [Parameter] public string? VariableExplorerPath { get; set; }

    private Guid? _selectedKildeId;

    private Guid? _selectedDatasamlingId;

    private KildeSortOrder _order;

    private UrlMirror _mirror = default!;

    /// <summary>
    /// How many times the address has moved under a standing circuit, which is the key
    /// <see cref="KildeSearch"/> is mounted under.
    /// </summary>
    /// <remarks>
    /// A counter rather than the state itself, because the two do not change together: opening a
    /// kilde from the list is this component's own field moving with no navigation behind it, and
    /// keying on that would throw away the list and the fetch the reader is already watching.
    /// </remarks>
    private int _arrival;

    private EventCallback<IReadOnlyList<Guid>> Handover =>
        VariableExplorerPath is null
            ? default
            : EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, ExploreVariables);

    protected override void OnInitialized()
    {
        InteractiveMount.Require(RendererInfo.IsInteractive, nameof(KildeExplorer));

        _mirror = new UrlMirror(Navigation, JS, Owns);
        (_selectedKildeId, _selectedDatasamlingId, _order) = Read(_mirror);

        Navigation.LocationChanged += Moved;
    }

    /// <summary>What the three owned keys say, or their defaults where the URL says nothing usable.</summary>
    /// <remarks>
    /// An id in a URL is whatever a stranger typed. One that does not parse opens the list, and one
    /// that parses but names nothing the API publishes opens a view that says so — the component's
    /// own documented behaviour, so nothing is validated here. <c>Enum.TryParse</c> alone is not
    /// enough for the order, for <c>ExplorerUrlState.Named</c>'s reason: it accepts any number, so
    /// <c>?sort=999</c> would succeed and hand the list an order no arm covers.
    /// </remarks>
    private static (Guid? Kilde, Guid? Datasamling, KildeSortOrder Order) Read(UrlMirror mirror)
    {
        var kilde = Guid.TryParse(mirror.Value(QueryKey), out var parsed) ? parsed : (Guid?)null;

        var datasamling = kilde is not null && Guid.TryParse(mirror.Value(DatasamlingQueryKey), out var open)
            ? open
            : (Guid?)null;

        var order = Enum.TryParse<KildeSortOrder>(mirror.Value(OrderQueryKey), ignoreCase: true, out var sorted)
                    && Enum.IsDefined(sorted)
            ? sorted
            : KildeSortOrder.Standard;

        return (kilde, datasamling, order);
    }

    /// <summary>
    /// Read the address again when something moved the page under a standing circuit, and draw
    /// what it now names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opening a datasamling is an <c>&lt;a href&gt;</c>, so that middle-click, Ctrl+click and the
    /// browser's own Back and Forward buttons all mean what they mean everywhere else. In a host
    /// with no router — helsedata's Optimizely CMS, and <c>samples/LegacyHost</c> — the browser
    /// simply follows it and this component initialises again from the new address. A host with a
    /// <c>Router</c> intercepts the press instead, leaves the circuit standing and re-renders: the
    /// address changes and nothing reads it, because the query is read at initialisation. This is
    /// what makes one component behave the same in both.
    /// </para>
    /// <para>
    /// <b>Nothing is navigated from here</b>, and that is the point rather than an economy. A
    /// forced reload would answer the same question, but every navigation clears the browser's
    /// forward list — so arriving on a Back and reloading would take the reader's Forward button
    /// away, which is the one thing a real link was chosen to keep. Re-reading and remounting is
    /// a press-for-press equal of the load the router-less host performs, minus the load.
    /// </para>
    /// <para>
    /// What it does not carry across is the tree: <see cref="KildeSearch"/> is remounted, so an
    /// open kilde is fetched again and every disclosure in its hierarchy comes back collapsed.
    /// The router-less host loses the same thing to the page load, so the two agree.
    /// </para>
    /// </remarks>
    private void Moved(object? sender, LocationChangedEventArgs e)
    {
        var address = new Uri(e.Location);

        if (!string.Equals(address.AbsolutePath, _mirror.Path, StringComparison.Ordinal))
        {
            return;
        }

        var mirror = new UrlMirror(address, JS, Owns);
        var arrived = Read(mirror);

        if (arrived == (_selectedKildeId, _selectedDatasamlingId, _order))
        {
            return;
        }

        // The mirror too, not only what it said: it holds the host's own parameters, and mirroring
        // the ones this component arrived with would put back keys the navigation had dropped.
        _mirror = mirror;
        (_selectedKildeId, _selectedDatasamlingId, _order) = arrived;
        _arrival++;

        StateHasChanged();
    }

    public void Dispose() => Navigation.LocationChanged -= Moved;

    private static bool Owns(string key) =>
        string.Equals(key, QueryKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, DatasamlingQueryKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, OrderQueryKey, StringComparison.OrdinalIgnoreCase);

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        _mirror.MirrorAsync(Query(_selectedDatasamlingId)).AsTask();

    /// <summary>The three keys this component owns, as a query string, omitting what is at its default.</summary>
    /// <remarks>
    /// The catalogue's own order writes nothing, so an untouched explorer leaves the address bar as
    /// it found it — <see cref="ExplorerUrlState.ToQueryString"/>'s rule, for its reason: a link
    /// carries what someone chose rather than a transcript of every setting.
    /// </remarks>
    /// <param name="datasamling">
    /// The datasamling the query should name, which is the open one for the address bar and any of
    /// the kilde's for a link that would open it.
    /// </param>
    private string Query(Guid? datasamling)
    {
        string[] owned =
        [
            _selectedKildeId is { } id
                ? QueryKey + "=" + Uri.EscapeDataString(id.ToString())
                : "",
            _selectedKildeId is not null && datasamling is { } open
                ? DatasamlingQueryKey + "=" + Uri.EscapeDataString(open.ToString())
                : "",
            _order == KildeSortOrder.Standard
                ? ""
                : OrderQueryKey + "=" + Uri.EscapeDataString(_order.ToString()),
        ];

        return string.Join("&", owned.Where(pair => pair.Length != 0));
    }

    private Func<Guid?, string>? _address;

    /// <summary>
    /// This page's address showing the open kilde, and the datasamling named — or the kilde alone
    /// when none is.
    /// </summary>
    /// <remarks>
    /// The one thing <see cref="KildeSearch"/> cannot work out for itself: only this component knows
    /// which query keys the address carries and which of the host's own it has to carry through. A
    /// delegate rather than a callback, so the drill-in stays a link the browser opens — and one
    /// delegate for the component's life rather than a lambda in the markup, which would be a
    /// changed parameter on every render.
    /// </remarks>
    private Func<Guid?, string> DatasamlingHref =>
        _address ??= datasamling => _mirror.Address(Query(datasamling));

    /// <summary>Follow the open kilde, and drop the datasamling that was a step inside it.</summary>
    /// <remarks>
    /// Without the second half the id would outlive the kilde it belongs to in a field nothing
    /// draws, and <see cref="Moved"/> would compare an arriving address against it.
    /// </remarks>
    private void KildeChanged(Guid? kilde)
    {
        _selectedKildeId = kilde;
        _selectedDatasamlingId = null;
    }

    /// <summary>Turn the chosen kilder into the query the variable explorer reads, and go there.</summary>
    /// <remarks>
    /// An empty list is not a selection of none: it is what the component sends when the reader
    /// narrowed nothing, so it lands on the unfiltered variable list. The format is not restated
    /// here — <see cref="VariableFilter.ToQueryString"/> writes what its own <c>Parse</c> reads.
    /// </remarks>
    private void ExploreVariables(IReadOnlyList<Guid> kildeIds)
    {
        var query = new VariableFilter { KildeIds = kildeIds }.ToQueryString();

        // Against the application base rather than the origin. NavigateTo("/variabler") from an app
        // mounted under /optimizely drops the prefix, which is identical locally and sends the
        // reader out of the application behind a reverse proxy — the trap the mirror avoids too.
        var path = (VariableExplorerPath ?? "").TrimStart('/');
        var destination = Navigation.ToAbsoluteUri(query.Length == 0 ? path : path + "?" + query);

        Navigation.NavigateTo(destination.ToString(), forceLoad: true);
    }
}
