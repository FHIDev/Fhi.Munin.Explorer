using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The variabelutforsker: search, the reader's own variable lists, and a link that reopens what
/// was on screen when it was copied.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one component a host mounts.</b> Language and IsAuthenticated are the whole of what it
/// needs — which is exactly what helsedata's <c>BlazorComponentPage</c> offers, and this type's
/// full name is already that page's shipped default. <see cref="VariableSearch"/> and
/// <see cref="VariableListView"/> stay public underneath for a host that wants to lay the two
/// surfaces out itself, or to own its own query string.
/// </para>
/// <para>
/// <b>It must be mounted interactively</b> — <c>render-mode="Server"</c> in a legacy Blazor Server
/// host, <c>@rendermode</c> with <c>prerender: false</c> in a modern one. Prerendered it throws on
/// initialisation rather than rendering a page whose URL silently never follows the view, and it
/// would fetch everything twice.
/// </para>
/// <para>
/// <b>It will not take a query key away from you.</b> It reads and rewrites the keys in
/// <see cref="ExplorerUrlState.QueryKeys"/> and touches nothing else, so a host's own
/// <c>?utm_source=</c> survives every filter change. <see cref="DeclinedKeys"/> is how you keep one
/// of ours as well.
/// </para>
/// <para>
/// <b>A fragment is kept, never written.</b> The section a reader jumped to — <c>#metadata</c>, say
/// — survives every rewrite while the view it names is the one on screen, so the address bar stays
/// a link worth copying, and it is dropped at the first different search, facet, page, sort or open
/// variable. No link this component builds carries a fragment taken from the address, because an id
/// naming a section of the view being left names nothing in the view a link opens.
/// </para>
/// <para>
/// Which tab is open is circuit state and not a query key: a link carries the search, not the
/// reader's own lists, and a shared link that opened on somebody else's Variabelliste would be a
/// link to an empty page for everyone but its author.
/// </para>
/// </remarks>
public sealed partial class VariableExplorer : ComponentBase, IAsyncDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc cref="VariableSearch.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableSearch.IsAuthenticated"/>
    /// <remarks>
    /// Passed to both tabs. It is a <see langword="bool"/> rather than a callback, so it crosses a
    /// static-SSR boundary intact — see <see cref="KildeExplorer"/> for why that
    /// distinction matters here. Signed out, there is no tab at all — <see cref="VariableSearch"/>
    /// draws one sentence in its place instead (Fhi.Metadata-4ifsa).
    /// </remarks>
    [Parameter] public bool IsAuthenticated { get; set; }

    /// <inheritdoc cref="VariableSearch.HeadingLevel"/>
    [Parameter] public int HeadingLevel { get; set; } = 2;

    /// <inheritdoc cref="VariableSearch.Lede"/>
    /// <remarks>
    /// Declared here as well as on <see cref="VariableSearch"/>, and forwarded, because this is the
    /// mount a host names: a parameter the mounted type does not declare is dropped before it is
    /// set, silently and with everything still compiling. Not passed to the lists tab.
    /// </remarks>
    [Parameter] public string? Lede { get; set; }

    /// <summary>
    /// Query keys this component must leave alone: not read when the page opens, not written when
    /// the reader changes something, and carried through the address bar exactly as they arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For the host whose page already means something else by <c>?page=</c> or <c>?search=</c>.
    /// Declining a key does not take the control away — the reader can still search — it only keeps
    /// that part of the view out of the link.
    /// </para>
    /// <para>
    /// Only the names in <see cref="ExplorerUrlState.ScalarQueryKeys"/> can be declined, and
    /// anything else throws rather than being ignored. The facet keys are the explorer's own
    /// vocabulary: no host means something else by <c>?variabelgruppeIds=</c>, and half a filter in
    /// a URL would describe a search nobody is looking at. Read once, when the component
    /// initialises.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">A name that is not a key this component maintains.</exception>
    [Parameter] public IReadOnlyCollection<string>? DeclinedKeys { get; set; }

    // @bind- needs settable properties and ExplorerUrlState is a record, so the binding target is a
    // mutable holder that converts at both ends.
    private Binding _state = new();

    private UrlMirror _mirror = default!;

    private ExplorerInterop? _interop;

    private bool _disposed;

    protected override void OnInitialized()
    {
        InteractiveMount.Require(RendererInfo.IsInteractive, nameof(VariableExplorer));

        foreach (var key in DeclinedKeys ?? [])
        {
            if (!ExplorerUrlState.ScalarQueryKeys.Contains(key))
            {
                throw new ArgumentException(
                    $"'{key}' is not a key {nameof(VariableExplorer)} maintains, so declining it " +
                    $"would do nothing. The ones it does are {string.Join(", ", ExplorerUrlState.ScalarQueryKeys)}.",
                    nameof(DeclinedKeys));
            }
        }

        _mirror = new UrlMirror(Navigation, JS, Owns);
        _state = Binding.From(ExplorerUrlState.Parse(_mirror.Owned));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await _mirror.MirrorAsync(Linkable(_state.ToState()).ToQueryString());

        if (!firstRender)
        {
            return;
        }

        // The result is discarded on purpose: nothing rendered above reads the module, so a host
        // that does not serve it draws exactly this page (Fhi.Metadata-35w0p.14). Here rather than
        // in OnInitialized because prerender has no JS runtime to import with.
        var interop = new ExplorerInterop(JS);

        // Assigned before the import so DisposeAsync can see it, and released here when disposal
        // already ran: this continuation resumes after an await the renderer does not wait for.
        _interop = interop;

        await interop.TryLoadAsync();

        if (_disposed)
        {
            await interop.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;

        return _interop?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    // This page as the reader's address bar now reads it, for the drill-in views' contents nav.
    // Built fresh on every render: the state it reads moves without a navigation, and a contents
    // link naming the view before last would take the reader off this page.
    private string PageAddress => _mirror.Address(Linkable(_state.ToState()).ToQueryString());

    private Func<Guid, string>? _collectionVariablesAddress;

    private Func<Guid, string> DatasamlingVariablesHref => _collectionVariablesAddress ??= id =>
        _mirror.Address(Linkable(_state.ToState() with
        {
            Filter = _state.Filter with { DatasamlingIds = [id] },
            Page = 1,
            SelectedVariableId = null,
        }).ToQueryString());

    private Func<Guid, string>? _instrumentAddress;

    /// <summary>This page showing one instrument, with everything else about the view kept.</summary>
    /// <remarks>
    /// The search, the facets and the open variable travel with it on purpose: leaving the
    /// instrument puts the reader back where they came from, and none of it was torn down.
    /// <para>
    /// None at all where the host declined <c>instrumentId</c>: this address is that one key, so
    /// <see cref="Linkable"/> would strip it and leave every name an <c>&lt;a&gt;</c> back to the
    /// page the reader is already on. Null is what makes them words instead.
    /// </para>
    /// </remarks>
    private Func<Guid, string>? InstrumentHref => Declined("instrumentId")
        ? null
        : _instrumentAddress ??= id =>
            _mirror.Address(Linkable(_state.ToState() with { SelectedInstrumentId = id }).ToQueryString());

    private Func<Guid, string>? _instrumentVariablesAddress;

    /// <summary>This search narrowed to one instrument's variables, and to nothing else.</summary>
    /// <remarks>
    /// The whole filter is replaced rather than amended, unlike
    /// <see cref="DatasamlingVariablesHref"/>, and the search term goes with it: the link names the
    /// instrument's own count, so a target carrying the reader's other narrowing would open a page
    /// with fewer rows than the number they pressed.
    /// </remarks>
    private Func<Guid, string> InstrumentVariablesHref => _instrumentVariablesAddress ??= id =>
        _mirror.Address(Linkable(_state.ToState() with
        {
            Filter = VariableFilter.None with { InstrumentIds = [id] },
            Search = null,
            Page = 1,
            SelectedVariableId = null,
            SelectedInstrumentId = null,
        }).ToQueryString());

    private Func<string, string>? _sharedListAddress;

    // Absolute and carrying nothing of the sender's own view but the host's parameters: the link
    // leaves the page by e-mail, and the recipient asked for the list, not the sender's search.
    private Func<string, string>? SharedListHref => Declined("delekode")
        ? null
        : _sharedListAddress ??= code =>
            Navigation.ToAbsoluteUri(_mirror.Address(new ExplorerUrlState { ShareCode = code }.ToQueryString()))
                .ToString();

    private void OnShareCodeChanged(string? code) => _state.ShareCode = code;

    private bool Owns(string key) =>
        ExplorerUrlState.QueryKeys.Contains(key) && !Declined(key);

    private bool Declined(string key) =>
        DeclinedKeys?.Contains(key, StringComparer.OrdinalIgnoreCase) == true;

    /// <summary>The state with the declined keys back at their defaults, so nothing writes them.</summary>
    private ExplorerUrlState Linkable(ExplorerUrlState state)
    {
        if (DeclinedKeys is not { Count: > 0 })
        {
            return state;
        }

        return state with
        {
            Search = Declined("search") ? null : state.Search,
            SelectedVariableId = Declined("variabelId") ? null : state.SelectedVariableId,
            SelectedInstrumentId = Declined("instrumentId") ? null : state.SelectedInstrumentId,
            ShareCode = Declined("delekode") ? null : state.ShareCode,
            Sort = Declined("sort") ? SortField.Default : state.Sort,
            Direction = Declined("sortDir") ? SortDirection.Ascending : state.Direction,
            Page = Declined("page") ? 1 : state.Page,
            PageSize = Declined("pageSize") ? ExplorerUrlState.DefaultPageSize : state.PageSize,
        };
    }

    /// <summary>The state as separate settable properties, which is what <c>@bind-</c> needs.</summary>
    private sealed class Binding
    {
        public string? Search { get; set; }

        public VariableFilter Filter { get; set; } = VariableFilter.None;

        public SortField Sort { get; set; } = SortField.Default;

        public SortDirection Direction { get; set; } = SortDirection.Ascending;

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = ExplorerUrlState.DefaultPageSize;

        public Guid? SelectedVariableId { get; set; }

        public Guid? SelectedInstrumentId { get; set; }

        public string? ShareCode { get; set; }

        public static Binding From(ExplorerUrlState state) => new()
        {
            Search = state.Search,
            Filter = state.Filter,
            Sort = state.Sort,
            Direction = state.Direction,
            Page = state.Page,
            PageSize = state.PageSize,
            SelectedVariableId = state.SelectedVariableId,
            SelectedInstrumentId = state.SelectedInstrumentId,
            ShareCode = state.ShareCode,
        };

        public ExplorerUrlState ToState() => new()
        {
            Search = Search,
            Filter = Filter,
            Sort = Sort,
            Direction = Direction,
            Page = Page,
            PageSize = PageSize,
            SelectedVariableId = SelectedVariableId,
            SelectedInstrumentId = SelectedInstrumentId,
            ShareCode = ShareCode,
        };
    }
}
