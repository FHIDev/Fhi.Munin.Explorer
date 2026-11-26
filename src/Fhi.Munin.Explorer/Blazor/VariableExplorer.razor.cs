using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The variabelutforsker: search, the reader's own variable lists, and a link that reopens what
/// was on screen when it was copied.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one component a host mounts.</b> Language and IsAuthenticated are the whole of what it
/// needs, helsedata's <c>BlazorComponentPage</c> passes both, and this type's
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
/// <b>A fragment is kept, never written.</b> The section a reader jumped to — <c>#munin-explorer-metadata</c>, say
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

    [Inject] private IMuninExplorerClient Client { get; set; } = default!;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    private ILogger? _log;
    private ILogger? Log => _log ??= ExplorerLog.For<VariableExplorer>(Services);

    /// <inheritdoc cref="VariableSearch.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableSearch.IsAuthenticated"/>
    /// <remarks>
    /// Passed to both tabs. It is a <see langword="bool"/> rather than a callback, so it crosses a
    /// static-SSR boundary intact — see <see cref="KildeExplorer"/> for why that
    /// distinction matters here. Signed out, there is no tab at all — <see cref="VariableSearch"/>
    /// draws one sentence in its place instead (Fhi.Metadata-4ifsa) — unless the host turns
    /// <c>MuninExplorer:Features:CodePicker</c> on, when the tab holds the list kept in the reader's browser.
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

    /// <inheritdoc cref="VariableSearch.TitleTemplate"/>
    [Parameter] public string? TitleTemplate { get; set; }

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

    protected override async Task OnInitializedAsync()
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

        Navigation.LocationChanged += Moved;

        if (NeedsResolving(_state))
        {
            await ResolveAsync(++_resolution);
        }
    }

    // A variable code that arrived without an id is resolved before VariableSearch mounts, because it reads the open
    // variable once (Fhi.Metadata-l9l2n.182). Until then the search is not drawn and the address is not written.
    private bool _resolving;
    private int _resolution;
    private bool _resolvePushed;

    // «The datasamling in the link was not found»: cascaded to VariableSearch, which says it beside «Vis alle treff».
    private bool _linkNote;

    // The arrival's own spelling of a code link and the address it resolved to, so a host navigation that still
    // carries the arrival's spelling is the same view rather than a remount.
    private (string Arrived, string Resolved)? _alias;

    // Whether Munin answers a variable by its code: null until a lookup has said. A code goes into a link only once
    // one has, so a host without the route keeps writing links that open (Fhi.Metadata-l9l2n.182).
    private bool? _codesResolve;

    // What the address needs to name an open variable by its code, learned once per variable and only from Munin's
    // answer to that code: the code, its primary datasamling, whether it is historical, and its datasamlinger's codes.
    private readonly Dictionary<Guid, Known> _known = [];

    private sealed record Known(string Code, Guid? Primary, bool Historical, IReadOnlyDictionary<Guid, string> Datasamlinger)
    {
        // A Munin older than the datasamling's code sends none, and the address names the row by id as before.
        public static Known Of(VariableDetail detail) => new(
            detail.Code,
            detail.DatasamlingId,
            VersionStatusRule.IsHistorical(detail.VersionStatus),
            detail.AllDatasamlinger
                .Where(d => DatasamlingCodes.IsLinkCode(d.Code))
                .GroupBy(d => d.Id)
                .ToDictionary(g => g.Key, g => g.First().Code!.Trim()));
    }
    private readonly HashSet<Guid> _asked = [];

    private static bool NeedsResolving(Binding state) =>
        state.SelectedVariableCode is not null && state.SelectedVariableId is null;

    private static ExplorerUrlState WithoutVariable(ExplorerUrlState state) =>
        state with { SelectedVariableId = null, SelectedVariableCode = null, SelectedDatasamlingId = null, SelectedDatasamlingCode = null };

    // A link naming the variable and nothing else, whose search and filter are the arrival's to make.
    private static bool IsBare(ExplorerUrlState state) => WithoutVariable(state).ToQueryString().Length == 0;

    /// <summary>Open the variable the address's code names, or a search for the code when it names none.</summary>
    private async Task ResolveAsync(int generation)
    {
        if (generation != _resolution || _disposed)
        {
            return;
        }

        var arrived = _state.ToState();
        var code = arrived.SelectedVariableCode!;
        var wantedCode = arrived.SelectedDatasamlingCode;
        var bare = IsBare(arrived);
        _resolving = true;

        VariableDetail? detail;
        try
        {
            detail = await Client.GetVariableByCodeAsync(code);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "could not resolve the variable code in the address");
            detail = null;
        }
        catch (Exception ex)
        {
            // Read as not found: the reader gets the search for the code.
            Log?.LogError(ex, "could not resolve the variable code in the address");
            detail = null;
        }

        if (generation != _resolution || _disposed)
        {
            return;
        }

        var state = _state;
        _linkNote = false;
        if (detail is null || string.IsNullOrWhiteSpace(detail.Code))
        {
            // The list's own empty state is the not-found view, and a code that is only part of one still finds it.
            state.SelectedVariableId = null;
            state.SelectedVariableCode = null;
            state.SelectedDatasamlingId = null;
            state.SelectedDatasamlingCode = null;
            state.Search = string.IsNullOrWhiteSpace(state.Search) ? code : state.Search;
        }
        else
        {
            _codesResolve = true;
            var known = Known.Of(detail);
            var historical = known.Historical;
            _known[detail.Id] = known;

            // A datasamling code or id that names none of the variable's falls back to the primary row. Said only when the
            // variable has more than one, since with one a missing datasamling can only have meant that one.
            var wanted = (wantedCode is null ? null : DatasamlingCodes.Find(detail.AllDatasamlinger, wantedCode))
                ?? arrived.SelectedDatasamlingId;
            var found = wanted is { } id && (id == detail.DatasamlingId || detail.AllDatasamlinger.Any(d => d.Id == id));
            // A Munin that sends no datasamling code cannot say whether a code link's datasamling is there.
            var codeUnanswerable = wantedCode is not null && arrived.SelectedDatasamlingId is null
                && known.Datasamlinger.Count == 0;
            _linkNote = (wantedCode is not null || arrived.SelectedDatasamlingId is not null) && !found
                && !codeUnanswerable && detail.AllDatasamlinger.Count > 1;

            // A bare link searches the code, which ranks first and so puts the pinned row on the first page; an
            // address the explorer wrote itself keeps the reader's own search and filters, as an id link does.
            if (bare)
            {
                state.Search = detail.Code;
                state.Filter = VariableFilter.None with { IncludeHistorical = historical };
            }

            state.SelectedVariableId = detail.Id;
            state.SelectedVariableCode = detail.Code;
            state.SelectedDatasamlingId = found ? wanted : detail.DatasamlingId;
            state.SelectedDatasamlingCode = null;
        }

        // Arrived like any link, or like the entry pushed from the list that Back or Forward reached.
        _resolving = false;
        _mirroredOpen = Open;
        _detailPushed = _resolvePushed && Open;
        _pushedListQuery = _detailPushed ? ListQuery() : null;
        _alias = (Linkable(arrived).ToQueryString(), Linkable(Coded(_state.ToState())).ToQueryString());
        _arrival++;
        StateHasChanged();
    }

    /// <summary>
    /// The state as the address writes it: the open variable by its code once Munin has answered that code, its row's
    /// datasamling by code where Munin sent one, and no search or filter when the code alone makes them again.
    /// </summary>
    private ExplorerUrlState Coded(ExplorerUrlState state, bool datasamlingById = false)
    {
        if (state.SelectedVariableId is not { } id)
        {
            return state;
        }

        if (!_known.TryGetValue(id, out var known))
        {
            if (!NoCodes)
            {
                Learn(id);
            }

            return state with { SelectedVariableCode = null, SelectedDatasamlingCode = null };
        }

        var coded = WithDatasamling(state with { SelectedVariableCode = known.Code }, known, state.SelectedDatasamlingId, datasamlingById);
        var arrival = new ExplorerUrlState
        {
            Search = known.Code,
            Filter = VariableFilter.None with { IncludeHistorical = known.Historical },
        };

        return WithoutVariable(state).ToQueryString() == arrival.ToQueryString()
            ? coded with { Search = null, Filter = VariableFilter.None }
            : coded;
    }

    // Asked once per variable, off the render: the address keeps the id form until Munin has answered the code with
    // this very variable. A failure is asked again at the next render that needs it.
    private void Learn(Guid id, string? code = null)
    {
        if (_codesResolve is false || !_asked.Add(id))
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            try
            {
                code ??= (await Client.GetVariableAsync(id, includeHistorical: true))?.Code;
                if (_disposed || !VariableCodes.IsLinkCode(code))
                {
                    return;
                }

                var detail = await Client.GetVariableByCodeAsync(code!);
                if (_disposed || detail?.Id != id)
                {
                    // Nothing for a code that names a variable: this Munin, or this host's client, has no code route.
                    _codesResolve ??= detail is null ? false : null;
                    return;
                }

                // The same view said another way, so a section the address arrived with stays in it.
                var before = Linkable(Coded(_state.ToState())).ToQueryString();
                _known[id] = Known.Of(detail);
                _codesResolve = true;
                _mirror.Reown(before, Linkable(Coded(_state.ToState())).ToQueryString());
                StateHasChanged();
            }
            catch (MuninExplorerRateLimitedException ex)
            {
                _asked.Remove(id);
                Log?.LogWarning(ex, "could not learn variable {VariableId}'s code for the address", id);
            }
            catch (Exception ex)
            {
                _asked.Remove(id);
                Log?.LogError(ex, "could not learn variable {VariableId}'s code for the address", id);
            }
        });
    }

    // Whether a variable or an instrument was open when the address bar was last written, and whether
    // its entry was pushed from the list, so Back returns to the list (Fhi.Metadata-l9l2n.124).
    private bool? _mirroredOpen;
    private bool _detailPushed;
    private int _arrival;

    private bool Open => _state.SelectedVariableId is not null || _state.SelectedInstrumentId is not null;

    // The list as it was when the detail was pushed: closing is Back only while it is still the
    // same, or a sort or page change made while the variable was open would be undone.
    private string? _pushedListQuery;

    private string ListQuery() =>
        Linkable(_state.ToState() with { SelectedVariableId = null, SelectedVariableCode = null, SelectedDatasamlingId = null, SelectedInstrumentId = null })
            .ToQueryString();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The bookkeeping before each await: renders overlap here, and a second one reading the old
        // values would push the same opening twice.
        if (_mirroredOpen is true && !Open && _detailPushed && _pushedListQuery == ListQuery())
        {
            // Closing what was opened from the list is the browser's own Back, so Forward reopens it.
            _detailPushed = false;
            _mirroredOpen = false;
            await _mirror.BackAsync(Linkable(Coded(_state.ToState())).ToQueryString());
        }
        else if (!_resolving)
        {
            // Only an opening the address shows: a host declining the selection's key gets no entry.
            // A list changed under the open row ends Back's claim, and the entry's mark with it.
            var query = Linkable(Coded(_state.ToState())).ToQueryString();
            var push = _mirroredOpen is false && Open && query != ListQuery();

            _detailPushed = push || (_detailPushed && Open && _pushedListQuery == ListQuery());
            _pushedListQuery = push ? ListQuery() : _pushedListQuery;
            _mirroredOpen = Open;

            await _mirror.MirrorAsync(query, push, _detailPushed);
        }

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

    /// <summary>Back or Forward, or a host's own navigation: the address is read again, as at load.</summary>
    /// <remarks>
    /// VariableSearch reads the open variable once, so a different state remounts it through
    /// <c>@key</c>, as <see cref="KildeExplorer"/> remounts its search for the same reason.
    /// </remarks>
    private void Moved(object? sender, LocationChangedEventArgs e) =>
        _ = InvokeAsync(() => Arrive(e.Location, e.HistoryEntryState == UrlMirror.OpenedFromList));

    private void Arrive(string location, bool pushedFromList)
    {
        if (_disposed)
        {
            return;
        }

        var address = new Uri(location);

        if (!_mirror.IsSamePage(address))
        {
            return;
        }

        var mirror = new UrlMirror(address, JS, Owns);
        var arrived = Binding.From(ExplorerUrlState.Parse(mirror.Owned));
        _mirror = mirror;

        if (!IsShown(Linkable(arrived.ToState()).ToQueryString()))
        {
            // A remount: the browser already shows this address, and a late write could overwrite a Forward entry.
            // A lookup still pending belongs to the view being left.
            mirror.Arrived();

            // The variable alone names its primary row, as ResolveAsync pins it for the code spelling; left to the
            // search, it would open whichever row the list puts first.
            if (Declined("datasamlingId") && arrived.SelectedVariableId is { } id && _known.TryGetValue(id, out var known))
            {
                arrived.SelectedDatasamlingId = known.Primary;
            }

            _state = arrived;
            var generation = ++_resolution;
            _resolving = NeedsResolving(arrived);
            _resolvePushed = pushedFromList;
            _alias = null;

            if (_resolving)
            {
                _ = InvokeAsync(() => ResolveAsync(generation));
            }
            else
            {
                _linkNote = false;
                _arrival++;
            }
        }

        // On every arrival: an entry pushed from the list, reached by Back or Forward, has the list
        // under it; one a link or a host parameter made does not.
        _mirroredOpen = Open;
        _detailPushed = pushedFromList && Open;
        _pushedListQuery = _detailPushed ? ListQuery() : null;

        StateHasChanged();
    }

    // Every spelling of the view on screen: by code, by id, as the code link it arrived as, and with the row's
    // datasamling by id as the explorer wrote it before codes reached datasamlinger.
    private bool IsShown(string query)
    {
        var current = _state.ToState();
        var coded = Linkable(Coded(current)).ToQueryString();

        // While a code is looked up there is no id spelling: without the variable, it would be the plain list.
        return query == coded
            || OldSpellingNamesTheRow(current) && query == Linkable(Coded(current, datasamlingById: true)).ToQueryString()
            || current.SelectedVariableId is not null && OldSpellingNamesTheRow(current)
               && query == Linkable(current with { SelectedVariableCode = null }).ToQueryString()
            || _alias is { } alias && alias.Arrived == query && alias.Resolved == coded;
    }

    // Declining datasamlingId leaves every row's old spelling, by code or by id, as the variable alone, which names
    // only the primary row once the code is known.
    private bool OldSpellingNamesTheRow(ExplorerUrlState current) =>
        !Declined("datasamlingId") || current.SelectedDatasamlingId is not { } row
        || current.SelectedVariableId is { } id && _known.TryGetValue(id, out var known) && known.Primary == row;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        Navigation.LocationChanged -= Moved;

        return _interop?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    // This page as the reader's address bar now reads it, for the drill-in views' contents nav.
    // Built fresh on every render: the state it reads moves without a navigation, and a contents
    // link naming the view before last would take the reader off this page.
    private string PageAddress => _mirror.Address(Linkable(Coded(_state.ToState())).ToQueryString());

    private Func<Guid, string>? _collectionVariablesAddress;

    private Func<Guid, string> DatasamlingVariablesHref => _collectionVariablesAddress ??= id =>
        _mirror.Address(Linkable(_state.ToState() with
        {
            // «This datasamling's variabler» narrows the reader's other ticks, never widens them (Fhi.Metadata-emsc0.1.1).
            Filter = _state.Filter with { DatasamlingIds = [id], OrTreeTicks = false },
            Page = 1,
            SelectedVariableId = null,
            SelectedVariableCode = null,
            SelectedDatasamlingId = null,
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
            SelectedVariableCode = null,
            SelectedDatasamlingId = null,
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

    private Func<VariableListItem, string?>? _variableAddress;
    private Func<VariableSummary, string?>? _searchVariableAddress;

    private Uri? ExportPageUrl => DeclinedVariable || Declined("search") || Declined("datasamlingId")
        ? null
        : Navigation.ToAbsoluteUri(_mirror.Address(""));

    // A variable is linked by its code and its row's datasamling by its code, the same in every environment; by id
    // where Munin sends no datasamling code (Fhi.Metadata-l9l2n.182, Fhi.Metadata-l9l2n.204).
    private Func<VariableListItem, string?>? VariableHref => DeclinedVariable
        ? null
        : _variableAddress ??= item =>
            VariableAddress(item.VariableCode, item.KildeId, item.VersionStatus, item.VariableId, item.DatasamlingId);

    // The same link from a search row and from the whole variable, so «Del variabel» opens one thing everywhere.
    private Func<VariableSummary, string?>? SearchVariableHref => DeclinedVariable
        ? null
        : _searchVariableAddress ??= v => VariableAddress(v.Code, v.KildeId, v.VersionStatus, v.Id, v.DatasamlingId);

    // By code once Munin has answered one, the datasamling as WithDatasamling names it. Until then the id link that
    // worked before: the search keeps a selection only on a row its first page shows, so it searches the code.
    private string? VariableAddress(string? code, Guid? kildeId, string? versionStatus, Guid variableId, Guid? datasamlingId)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (!NoCodes && _codesResolve is true && VariableCodes.IsLinkCode(code))
        {
            // Only an open row's link is drawn, so asking its primary here is one lookup per row opened.
            if (!_known.ContainsKey(variableId))
            {
                Learn(variableId, code.Trim());
            }

            var link = new ExplorerUrlState { SelectedVariableCode = code.Trim(), SelectedDatasamlingId = datasamlingId };
            return Absolute(_known.TryGetValue(variableId, out var known) ? WithDatasamling(link, known, datasamlingId) : link);
        }

        if (!NoCodes)
        {
            Learn(variableId, code.Trim());
        }

        return Declined("search")
            ? null
            : Absolute(new ExplorerUrlState
            {
                Search = code,
                Filter = VariableFilter.None with
                {
                    KildeIds = kildeId is { } kilde ? [kilde] : [],
                    IncludeHistorical = VersionStatusRule.IsHistorical(versionStatus),
                },
                SelectedVariableId = variableId,
                SelectedDatasamlingId = datasamlingId,
            });
    }

    // The row's datasamling by its code, the primary's too, so a link opens the same row in every environment
    // (Fhi.Metadata-l9l2n.204). Without a code, by id as before, and not at all for the primary row.
    private ExplorerUrlState WithDatasamling(ExplorerUrlState state, Known known, Guid? datasamlingId, bool byId = false)
    {
        var row = datasamlingId ?? known.Primary;
        if (!byId && !Declined("datasamling") && row is { } id && known.Datasamlinger.TryGetValue(id, out var code))
        {
            return state with { SelectedDatasamlingCode = code, SelectedDatasamlingId = null };
        }

        return state with
        {
            SelectedDatasamlingCode = null,
            SelectedDatasamlingId = datasamlingId == known.Primary ? null : datasamlingId,
        };
    }

    private string Absolute(ExplorerUrlState state) =>
        Navigation.ToAbsoluteUri(_mirror.Address(Linkable(state).ToQueryString())).ToString();

    private void OnShareCodeChanged(string? code) => _state.ShareCode = code;

    // A code is not read where it would never be written back out.
    private bool Owns(string key) =>
        ExplorerUrlState.QueryKeys.Contains(key) && !Declined(key)
        && !(NoCodes && (string.Equals(key, "variabel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "datasamling", StringComparison.OrdinalIgnoreCase)));

    // Declining variabelId keeps the open variable out of the address, its code and datasamling with it.
    private bool DeclinedVariable => Declined("variabelId");

    // Declining variabel keeps codes out: the open variable is named by its id, as before codes (Fhi.Metadata-l9l2n.182).
    private bool NoCodes => DeclinedVariable || Declined("variabel");

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
            SelectedVariableId = DeclinedVariable ? null : state.SelectedVariableId,
            SelectedVariableCode = NoCodes ? null : state.SelectedVariableCode,
            SelectedDatasamlingId = DeclinedVariable || Declined("datasamlingId") ? null : state.SelectedDatasamlingId,
            SelectedDatasamlingCode = DeclinedVariable || Declined("datasamling") ? null : state.SelectedDatasamlingCode,
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

        private Guid? _selectedVariableId;

        // A different variable opened by the reader is not the one the code named.
        public Guid? SelectedVariableId
        {
            get => _selectedVariableId;
            set
            {
                if (value != _selectedVariableId)
                {
                    SelectedVariableCode = null;
                }

                _selectedVariableId = value;
            }
        }

        public string? SelectedVariableCode { get; set; }

        public Guid? SelectedDatasamlingId { get; set; }

        // Only an arrival's, until ResolveAsync turns it into SelectedDatasamlingId.
        public string? SelectedDatasamlingCode { get; set; }

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
            SelectedVariableCode = state.SelectedVariableCode,
            SelectedDatasamlingId = state.SelectedDatasamlingId,
            SelectedDatasamlingCode = state.SelectedDatasamlingCode,
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
            SelectedVariableCode = SelectedVariableCode,
            SelectedDatasamlingId = SelectedDatasamlingId,
            SelectedDatasamlingCode = SelectedDatasamlingCode,
            SelectedInstrumentId = SelectedInstrumentId,
            ShareCode = ShareCode,
        };
    }
}
