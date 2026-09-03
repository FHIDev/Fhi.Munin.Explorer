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
/// Which tab is open is circuit state and not a query key: a link carries the search, not the
/// reader's own lists, and a shared link that opened on somebody else's Variabelliste would be a
/// link to an empty page for everyone but its author.
/// </para>
/// </remarks>
public partial class VariableExplorer : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc cref="VariableSearch.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableSearch.IsAuthenticated"/>
    /// <remarks>
    /// Passed to both tabs. It is a <see langword="bool"/> rather than a callback, so it crosses a
    /// static-SSR boundary intact — see <see cref="KildeExplorerWithUrlState"/> for why that
    /// distinction matters here. Signed out, <see cref="VariableListView"/> renders nothing at all,
    /// and this component does not second-guess it: the tab is still there, and it is empty.
    /// </remarks>
    [Parameter] public bool IsAuthenticated { get; set; }

    /// <inheritdoc cref="VariableSearch.HeadingLevel"/>
    [Parameter] public int HeadingLevel { get; set; } = 2;

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

    protected override Task OnAfterRenderAsync(bool firstRender) =>
        _mirror.MirrorAsync(Linkable(_state.ToState()).ToQueryString()).AsTask();

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

        public static Binding From(ExplorerUrlState state) => new()
        {
            Search = state.Search,
            Filter = state.Filter,
            Sort = state.Sort,
            Direction = state.Direction,
            Page = state.Page,
            PageSize = state.PageSize,
            SelectedVariableId = state.SelectedVariableId,
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
        };
    }
}
