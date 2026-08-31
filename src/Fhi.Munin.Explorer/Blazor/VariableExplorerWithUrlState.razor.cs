using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// <see cref="VariableExplorer"/> with its state in the host's address bar: a link opens the search
/// it was copied from and the variable that was open in it, and every change the reader makes
/// updates the URL.
/// </summary>
/// <remarks>
/// <para>
/// The optional half of <see cref="ExplorerUrlState"/>. That type is what a host uses to write this
/// itself — parsing, escaping and the page-size default — and this is the whole of it done for you.
/// A host mounts it in place of <see cref="VariableExplorer"/>, at an interactive render mode, and
/// writes nothing else.
/// </para>
/// <para>
/// <b>What it will not do is take a key away from you.</b> It reads and rewrites the keys in
/// <see cref="ExplorerUrlState.QueryKeys"/> and touches nothing else in the query, so a host's own
/// <c>?utm_source=</c> survives every filter change. <see cref="DeclinedKeys"/> is how you keep one
/// of ours as well — a page that already has a <c>?page=</c> of its own says so, and that parameter
/// is then carried through untouched rather than overwritten.
/// </para>
/// <para>
/// <b>It must be mounted interactively</b> — <c>render-mode="Server"</c> in a legacy Blazor Server
/// host, <c>@rendermode</c> with <c>prerender: false</c> in a modern one. Prerendered, it throws on
/// initialisation rather than rendering a page whose URL silently never follows the view — and it
/// would also fetch everything twice, which is why the explorer itself is mounted that way too.
/// </para>
/// </remarks>
public partial class VariableExplorerWithUrlState : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc cref="VariableExplorer.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableExplorer.IsAuthenticated"/>
    /// <remarks>
    /// Passed straight through. It is a <see langword="bool"/> rather than a callback, so it
    /// crosses a static-SSR boundary intact — see <see cref="KildeExplorerWithUrlState"/> for why
    /// that distinction matters here.
    /// </remarks>
    [Parameter] public bool IsAuthenticated { get; set; }

    /// <inheritdoc cref="VariableExplorer.HeadingLevel"/>
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
        InteractiveMount.Require(RendererInfo.IsInteractive, nameof(VariableExplorerWithUrlState));

        foreach (var key in DeclinedKeys ?? [])
        {
            if (!ExplorerUrlState.ScalarQueryKeys.Contains(key))
            {
                throw new ArgumentException(
                    $"'{key}' is not a key {nameof(VariableExplorerWithUrlState)} maintains, so declining it " +
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
