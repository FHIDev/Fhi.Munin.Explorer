using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Every fetch of the result list - the first one, and each search, sort and page turn after it -
/// together with the state that has to survive one.
/// </summary>
public partial class VariableSearch
{
    protected override async Task OnInitializedAsync()
    {
        _search = Search;
        _filter = Filter ?? VariableFilter.None;
        _selected = SelectedVariableId is { } selected ? new VariableDatasamlingKey(selected, SelectedDatasamlingId) : null;
        _selectionNeedsRow = SelectedVariableId is not null && SelectedDatasamlingId is null;
        _instrumentId = SelectedInstrumentId;
        _sort = _sortParameter = Sort;
        _direction = Direction;
        _levelLines = LevelLines;
        _showNodeIcons = ShowNodeIcons;
        _page = Math.Max(Page, 1);
        _pageSize = PageSize;
        ShowRestoredSortColumn();

        // Raised here rather than by OpenInitialInstrumentAsync at the end of this method: the
        // first paint of a page opened on ?instrumentId= would otherwise draw the region blank and
        // aria-busy="false" for the whole of the round trip below.
        _instrumentLoading = _instrumentId is not null;

        try
        {
            // Not SearchAsync: that is what a person pressing the search button does, and it starts
            // by throwing away the page number because a new search renumbers everything. Restoring
            // a shared link is the opposite — the page is the part worth keeping.
            if (await FetchAsync(_search))
            {
                await FetchFacetsAsync();
            }

            await LandOnRealPageAsync();

            // Both echoed back on mount. The search echo is a no-op for a host that just supplied it;
            // the page echo is not: LandOnRealPageAsync may have moved the reader off a page the link
            // asked for, and the host holds the link's number until it is told otherwise.
            await NotifySearchChangedAsync();
            await NotifyPageChangedAsync();

            await OpenInitialSelectionAsync();

            // Last, and unconditional: the instrument view covers the list rather than sitting in a
            // row, so what the search came back with says nothing about whether to fetch it.
            await OpenInitialInstrumentAsync();
        }
        finally
        {
            // Lowered by whoever raised it: every fetch above is awaited, so a flag still up here
            // belongs to no request in flight, and leaving it latched would tell a screen reader the
            // region is busy for the rest of the circuit.
            _instrumentLoading = false;
        }
    }

    /// <summary>Whether a press on the clear control would clear anything.</summary>
    /// <remarks>
    /// One predicate read twice — the clear refuses on it, the refocus decides on it. A second
    /// copy of the condition is the thing that gets inverted later. (Fhi.Metadata-ag4n7)
    /// </remarks>
    private bool CanClearSearch => !_loading && !string.IsNullOrWhiteSpace(_search);

    /// <summary>Empty the box and run the search that leaves, which is no search at all.</summary>
    private async Task ClearSearchAsync()
    {
        // Guard before mutation, the rule SortAsync states: clearing _search and then being dropped
        // by SearchAsync's own _loading check would leave an empty box over the old rows, with the
        // host still holding the previous search. (Fhi.Metadata-5ghur)
        if (!CanClearSearch)
        {
            return;
        }

        _search = null;

        await SearchAsync();
    }

    /// <summary>Take focus off the control about to vanish, then clear the search.</summary>
    /// <remarks>
    /// Focus moves <em>first</em>: the render at <see cref="SearchAsync"/>'s yield removes the control
    /// under the reader's focus, so moving it afterwards returns focus only once the fetch lands. (Fhi.Metadata-ag4n7)
    /// </remarks>
    private async Task ClearSearchAndRefocusAsync()
    {
        if (CanClearSearch)
        {
            await _searchField.FocusAsync();
        }

        await ClearSearchAsync();
    }

    private async Task SearchAsync()
    {
        // Nothing disables the submit button while a search runs — see the comment on it in
        // the markup — so a second submit is dropped here instead.
        if (_loading)
        {
            return;
        }

        // A different search is a different result set; page 7 of the old one means nothing in it.
        _page = 1;
        _keepPager = false;

        // The live contents of the box, which is what submitting means.
        if (await FetchAsync(_search))
        {
            // The counts are cross-filtered against the search as well as the filter, so a new
            // search moves them; only on success, so a failed search leaves the numbers describing
            // the rows that are still on screen.
            await FetchFacetsAsync();
        }

        await NotifySearchChangedAsync();
        await NotifyPageChangedAsync();
    }

    /// <summary>
    /// Sort by <paramref name="sort"/>: the active field again reverses the direction, another
    /// field starts ascending. Runa's rule.
    /// </summary>
    private async Task SortAsync(SortField sort)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit. The
        // guard comes first on purpose: changing the state and then not fetching would leave a
        // button saying the list is ordered one way while it is still ordered the other.
        if (_loading)
        {
            return;
        }

        // Kept so a failed fetch can put them back — see below. The page and the pager as well as
        // the order: reordering sends the reader to page one, and if the reorder never arrives they
        // are still on the page they were on.
        var previousSort = _sort;
        var previousDirection = _direction;
        var previousPage = _page;
        var previousKeepPager = _keepPager;

        if (sort == _sort)
        {
            _direction = _direction == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        }
        else
        {
            _sort = sort;
            _direction = SortDirection.Ascending;
        }

        // Reordering renumbers every page, so the page the user is on is no longer the same rows.
        _page = 1;
        _keepPager = false;

        // _executedSearch, not _search: a click blurs the field first, so _search may hold text the
        // user never submitted. Fetching it would quietly run a search nobody asked for, and
        // desynchronise the host, whose URL only follows SearchChanged.
        if (!await FetchAsync(_executedSearch))
        {
            // The list is still in the old order, so the buttons have to say so. Left moved, they
            // would claim an order the API never delivered, and pressing the same button again would
            // ask for descending, with no way back to the failed ascending fetch short of cycling twice.
            _sort = previousSort;
            _direction = previousDirection;
            _page = previousPage;
            _keepPager = previousKeepPager;

            return;
        }

        await RaiseAsync(SortChanged, _sort, Log);
        await RaiseAsync(DirectionChanged, _direction, Log);

        // Reordering renumbered the pages and sent the reader back to the first one.
        await NotifyPageChangedAsync();
    }

    // Keyed on Sort alone: SortAsync raises field then direction, so a host re-rendering between
    // them hands back a stale direction. Mid-fetch, _sortParameter stays put and OnAfterRenderAsync
    // follows it once the fetch lands, so a host that does not bind still sees its Sort applied.
    private async Task FollowSortParameterAsync()
    {
        if (Sort == _sortParameter || _loading)
        {
            return;
        }

        _sortParameter = Sort;

        if (Sort == _sort)
        {
            return;
        }

        var previousSort = _sort;
        var previousDirection = _direction;
        var previousPage = _page;
        var previousKeepPager = _keepPager;
        var countedSearch = _executedSearch;

        _sort = Sort;
        _direction = Direction;
        _page = 1;
        _keepPager = false;

        if (!await FetchAsync(_requestedSearch))
        {
            _sort = previousSort;
            _direction = previousDirection;
            _page = previousPage;
            _keepPager = previousKeepPager;

            await RaiseAsync(SortChanged, _sort, Log);
            await RaiseAsync(DirectionChanged, _direction, Log);

            return;
        }

        ShowRestoredSortColumn();

        // After a failed read the rows can arrive for a term the counts do not yet describe, or before any counts at all.
        if (_facets is null || _executedSearch != countedSearch)
        {
            await FetchFacetsAsync();
        }

        if (_page != previousPage)
        {
            await NotifyPageChangedAsync();
        }
    }

    /// <summary>
    /// Show page <paramref name="page"/> of the current result, keeping the search and the order.
    /// </summary>
    /// <remarks>
    /// The buttons hand it out-of-range numbers at the list's ends, so the boundary is enforced once, here.
    /// Not a search, so no <see cref="SearchChanged"/>: the host's URL follows the search, which is unchanged.
    /// </remarks>
    private async Task GoToPageAsync(int page)
    {
        // Dropped while a fetch is in flight, like a second submit. The buttons carry aria-disabled,
        // not disabled, so neither leaves the document under the finger that pressed it — which is
        // also why a failed page turn below keeps the rows it already had.
        if (_loading)
        {
            return;
        }

        var target = Math.Clamp(page, 1, TotalPages);

        // Also the whole of what makes a click on an unavailable button inert: at either end the
        // clamped target is the page already on screen.
        if (target == _page)
        {
            return;
        }

        // All three kept so a failed fetch can put them back: the result too, because the retreat
        // below turns a second page and must undo both, and the panel, because the retreat passes
        // through an empty answer that closes it.
        var previous = _page;
        var previousResult = _result;
        var previousPanel = CapturePanel();

        // A pager button was pressed, so the pager stays until a search or a sort replaces the
        // result — including through a retreat that lands on a single-page answer.
        _keepPager = true;

        _page = target;

        // keepResult: the pager is the only pressable thing rendered conditionally, so clearing the
        // rows would take Forrige and Neste out of the document in the error's render, drop focus
        // to <body>, and send a keyboard user back to the top of the host's page.
        if (!await FetchAsync(_executedSearch, keepResult: true))
        {
            // Nothing arrived, so the state has to keep describing what did — and what did is
            // still on screen. Same invariant the sort rollback protects.
            _page = previous;

            return;
        }

        await RetreatFromEmptyPageAsync(previous, previousResult, previousPanel);

        // After the retreat, not before: it can move the page again, and the host should be told
        // where the reader ended up rather than where they were headed.
        await NotifyPageChangedAsync();
    }

    /// <summary>
    /// Show <paramref name="size"/> rows per page, from page 1: a new size renumbers the rows.
    /// </summary>
    /// <remarks>
    /// The pager is kept, as a page turn keeps it: a larger size can collapse the result to one page,
    /// and dropping the pager would remove the pressed button and the only control that puts the size back.
    /// </remarks>
    private async Task SetPageSizeAsync(int size)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a page turn, and inert
        // on the size already in force so pressing it again costs no request.
        if (_loading || size == _pageSize)
        {
            return;
        }

        var previousSize = _pageSize;
        var previousPage = _page;
        var previousKeepPager = _keepPager;

        _pageSize = size;
        _page = 1;
        _keepPager = true;

        // keepResult, for the reason a page turn uses it: the pressed button is inside the pager,
        // which is rendered conditionally, so clearing the rows would take it out of the document
        // in the same render that reports the error and drop focus to <body>.
        if (!await FetchAsync(_executedSearch, keepResult: true))
        {
            // Nothing arrived, so the state has to keep describing what is still on screen — the
            // size included, or the control would report a size the visible rows were not built
            // with.
            _pageSize = previousSize;
            _page = previousPage;
            _keepPager = previousKeepPager;

            return;
        }

        // No retreat is needed on this path: page 1 is the one page that can never be out of range,
        // so an empty answer here is a result with no rows rather than a reader past the end.
        await RaiseAsync(PageSizeChanged, _pageSize, Log);
        await NotifyPageChangedAsync();
    }

    /// <summary>Step back once to a page with rows rather than show "Ingen variabler passet søket".</summary>
    /// <remarks>
    /// The clamp trusts the previous count, so a shrunk index or an out-of-range 404 (an empty page,
    /// not a throw) gets past it. A failed retreat restores the starting page, its rows and its panel.
    /// </remarks>
    private async Task RetreatFromEmptyPageAsync(
        int previous, Page<VariableSummary>? previousResult, PanelState previousPanel)
    {
        if (_page == 1 || _result is not { Items.Count: 0 })
        {
            return;
        }

        // TotalPages reads the answer that just arrived, so this is the new count and not the stale
        // one the clamp trusted. A server still claiming the page exists after sending nothing has
        // told us nothing usable, so page 1 is the only safe answer left.
        var last = TotalCount > 0 ? TotalPages : 1;
        _page = last < _page ? last : 1;

        if (await FetchAsync(_executedSearch, keepResult: true))
        {
            return;
        }

        // Nothing arrived, so — exactly as on the first fetch — the state has to go back to
        // describing the last answer that did. keepResult held on to the empty page that started
        // the retreat, which is the one result that must not be the one left on screen.
        _page = previous;
        _result = previousResult;

        // After the rows, so the row the panel is drawn inside is back before the panel is.
        await RestorePanelAsync(previousPanel);
    }

    /// <summary>What is open in the panel and what was fetched into it.</summary>
    private readonly record struct PanelState(
        VariableDatasamlingKey? Id, VariableDetail? Detail, string? Error, SourceState Source, KodeverkCodeLists? CodeLists);

    /// <summary>What is open in the kilde or datasamling panel inside it, and what was fetched.</summary>
    private readonly record struct SourceState(
        SourceKind? Kind, Guid? TargetId, KildeDetail? Kilde, DatasamlingDetail? Datasamling, string? Error);

    private PanelState CapturePanel() => new(_selected, _detail, _detailError, CaptureSource(), _codeLists);

    private SourceState CaptureSource() => new(_sourceKind, _sourceTargetId, _kilde, _datasamling, _sourceError);

    /// <summary>
    /// Reopen a panel that a fetch closed on its way through, when that fetch then failed.
    /// </summary>
    /// <remarks>
    /// The detail goes back rather than being asked for again, so one failure cannot turn into two; only a
    /// panel captured mid-fetch is fetched again. The host, told null on the way in, is told what is open after.
    /// </remarks>
    private async Task RestorePanelAsync(PanelState panel)
    {
        if (panel.Id is not { } row || _selected == row)
        {
            return;
        }

        var id = row.VariableId;
        _selected = row;
        _detail = panel.Detail;
        _detailError = panel.Error;
        var restoredLists = _codeLists = panel.CodeLists ?? new KodeverkCodeLists(id, Client, Log);

        // A new owner of the panel: whatever was in flight when it closed must not land in the one
        // just put back.
        _detailGeneration++;
        _detailLoading = false;

        if (panel.Detail is null && panel.Error is null)
        {
            await LoadDetailAsync(row);
        }
        else if (panel.Detail is { } restored)
        {
            // Closing stopped the loop for nameless kodeverk. Not awaited: the page turn's own state
            // must settle without waiting on code fetches, and each answer redraws its own list.
            _ = restoredLists.LoadUnnamedAsync(restored, () => ReferenceEquals(_codeLists, restoredLists));
        }

        // After the detail, for the reason the detail comes after the rows: the owner panel is
        // drawn inside the variable's, and LoadDetailAsync clears it on its way through.
        await RestoreSourceAsync(panel.Source, row);

        // _selected rather than row, for the reason ToggleDetailAsync gives: the fetch above
        // yields with the rows already back on screen and clickable, so another row may have been
        // opened while it ran, and what the host is told has to be what is open.
        await RaiseSelectionAsync();
    }

    /// <summary>
    /// Put the kilde or datasamling panel back alongside the variable panel it hung inside.
    /// </summary>
    /// <remarks>
    /// As <see cref="RestorePanelAsync"/>, one level down. Guarded on <paramref name="row"/> still being
    /// selected: a detail re-fetch yields with rows clickable, and another variable must not get this kilde.
    /// </remarks>
    private async Task RestoreSourceAsync(SourceState source, VariableDatasamlingKey row)
    {
        if (source.Kind is not { } kind || _selected != row)
        {
            return;
        }

        _sourceKind = kind;
        _sourceTargetId = source.TargetId;
        _kilde = source.Kilde;
        _datasamling = source.Datasamling;
        _sourceError = source.Error;

        // A new owner of the panel, for the reason RestorePanelAsync bumps the detail's.
        _sourceGeneration++;
        _sourceLoading = false;

        if (source.Kilde is not null || source.Datasamling is not null || source.Error is not null)
        {
            return;
        }

        // The collection's parent can differ from the variable's kilde, so a pending fetch
        // must resume with the requested target rather than derive it from the variable again.
        if (source.TargetId is { } sourceId)
        {
            await LoadSourceAsync(kind, sourceId);
        }
        else
        {
            ClearSource();
        }
    }

    /// <summary>Tell the host what was searched for, so it can reflect it in its own URL.</summary>
    /// <remarks>
    /// Raised whether or not the fetch succeeded: a host URL keeping the previous query after a failed
    /// search would hand out a link that reloads into a different search than the box is showing.
    /// </remarks>
    private Task NotifySearchChangedAsync() => RaiseAsync(SearchChanged, _search, Log);

    /// <summary>
    /// Move to the last real page when a restored link asks for one past the end.
    /// </summary>
    /// <remarks>
    /// The API does not clamp: page 99999 of 734 is empty, with nothing to press (Fhi.Metadata-eujqw).
    /// A page turn's emptied page is <see cref="RetreatFromEmptyPageAsync"/>'s, which can roll back.
    /// </remarks>
    private async Task LandOnRealPageAsync()
    {
        if (_result is not { TotalPages: > 0 } result || result.Items.Count > 0 || _page <= result.TotalPages)
        {
            return;
        }

        _page = result.TotalPages;

        await FetchAsync(_executedSearch);
    }

    /// <summary>Tell the host which page is showing, whether it turned, reset or was clamped.</summary>
    private Task NotifyPageChangedAsync() => RaiseAsync(PageChanged, _page, Log);

    // The datasamling first, so a host reacting to the variable already holds the row's datasamling.
    private async Task RaiseSelectionAsync()
    {
        await RaiseAsync(SelectedDatasamlingIdChanged, _selected?.DatasamlingId, Log);
        await RaiseAsync(SelectedVariableIdChanged, _selected?.VariableId, Log);
    }

    /// <summary>Hand a value to one of the host's callbacks without letting the host's own failure out.</summary>
    /// <remarks>
    /// The handler is the host's, and what it most often does is rewrite a URL. The logger is a
    /// parameter rather than a read of <c>Log</c>, so the helper stays <see langword="static"/>.
    /// </remarks>
    private static async Task RaiseAsync<TValue>(EventCallback<TValue> callback, TValue value, ILogger? log)
    {
        if (!callback.HasDelegate)
        {
            return;
        }

        try
        {
            await callback.InvokeAsync(value);
        }
        catch (NavigationException)
        {
            // A host that navigates from its handler. During static SSR that is signalled by this
            // exception and the framework turns it into the redirect, so swallowing it would drop
            // the navigation on the floor.
            throw;
        }
        catch (Exception ex)
        {
            log?.LogError(ex, "a host callback threw");

            // Unhandled, the host's throw would escape Blazor's dispatch, initial render included, and
            // tear down the circuit for helsedata's whole CMS page. The reader is told nothing more: it
            // is the host's bug, and "Kunne ikke hente variabler" would blame the API for it.
        }
    }

    /// <summary>
    /// Fetch <paramref name="search"/> and settle what the new rows mean for the open panel; true on success.
    /// </summary>
    /// <remarks>
    /// Settled for every caller here, outside the fetch's try/catch, which would report a host navigating from
    /// its callback as a failed search; and after a failure too, or a cleared list hides a selection the URL names.
    /// </remarks>
    private async Task<bool> FetchAsync(string? search, bool keepResult = false)
    {
        var fetched = await FetchRowsAsync(search, keepResult);

        await DropSelectionIfGoneAsync();

        return fetched;
    }

    /// <summary>Fetch <paramref name="search"/> at the current page and ordering. True when it succeeded.</summary>
    /// <remarks>
    /// The search is a parameter, not <c>_search</c>: searching means the box's live contents, sorting the rows' text.
    /// <paramref name="keepResult"/> keeps a failed page turn's rows and pager; a failed search's must go.
    /// </remarks>
    private async Task<bool> FetchRowsAsync(string? search, bool keepResult = false)
    {
        _loading = true;
        _rowsLoading = true;
        _requestedSearch = DisplayText.Trimmed(search);
        _error = null;
        _retryRowsEnabled = false;
        StateHasChanged();

        try
        {
            _result = await Client.SearchVariablesAsync(
                search,
                _filter,
                page: _page,
                pageSize: ClampedPageSize,
                sort: _sort,
                direction: _direction);
            _executedSearch = DisplayText.Trimmed(search);

            // The page that arrived, not the one asked for: a server clamping page 12 to page 8, say,
            // would otherwise caption the rows "Side 12 av 8" and keep Neste walking away from them.
            // One page number for the caption, the two buttons and the range.
            _page = ResultPage;

            // The offer belongs to the failure it answers. Left standing after a fetch someone
            // else started came back, it is a dead control that the atomic alert region reads out
            // again beside every later failure — RetryRowsAsync puts its own back, and says why.
            _failedRows = null;

            // Rows arrived with «Vis historiske» in force, so from here the filter owns Status.
            if (ShowStatusColumn)
            {
                _statusShownForSort = false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // Split the way the sentence below is split: a 429 is the catalogue up and the reader
            // asking too often, which is nobody's fault to go and fix. The search text stays out of
            // it — the page number is what says which request this was.
            if (ex is MuninExplorerRateLimitedException)
            {
                Log?.LogWarning(ex, "the rate limiter refused result page {Page}", _page);
            }
            else
            {
                Log?.LogError(ex, "could not load result page {Page}", _page);
            }

            // A 429 gets its own sentence: pressing Søk again at once, the generic advice, cannot help.
            // The rows are cleared either way, or the old page is captioned with this search's terms;
            // clearing says nothing about hits, since the summary line only speaks over a result.
            if (!keepResult)
            {
                _result = null;
            }

            var rateLimited = ex is MuninExplorerRateLimitedException;

            _error = rateLimited ? T.RateLimitError : T.Error;

            // No retry offered for a 429: pressing it is the one action that provably cannot help,
            // and the sentence beside it says to wait. The request is captured rather than replayed
            // off the fields, which every caller rolls back to describe the rows still on screen.
            if (!rateLimited)
            {
                _failedRows = new RowRequest(
                    search, _page, _pageSize, _sort, _direction, _filter, _keepPager);
                _retryRowsEnabled = true;
            }
            else
            {
                // An offer an earlier retry already answered goes with the 429 too, or this atomic
                // region reads the dead button out as one utterance with "vent litt". When the 429
                // is the answer to that very button, RetryRowsAsync puts it back inert — see there.
                _failedRows = null;
            }

            return false;
        }
        finally
        {
            _loading = false;
            _rowsLoading = false;
        }
    }

    /// <summary>The row request that failed, so the retry button can send that one again.</summary>
    /// <remarks>
    /// Not the fields at press time: each failing caller rolls them back, size included, so replaying them
    /// would report a page turn, sort or size change that never happened. <c>KeepPager</c> moves with <c>Page</c>.
    /// </remarks>
    private readonly record struct RowRequest(
        string? Search,
        int Page,
        int Size,
        SortField Sort,
        SortDirection Direction,
        VariableFilter Filter,
        bool KeepPager);

    /// <summary>
    /// Resend the failed row request unchanged; <see cref="SearchAsync"/> would read a box since edited.
    /// </summary>
    /// <remarks>
    /// Counts are re-asked only after a search or filter change, which they are cross-filtered against;
    /// the host is told only what moved, since a URL rewritten per callback gains needless history.
    /// </remarks>
    private async Task RetryRowsAsync()
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit — and
        // inert rather than absent once there is nothing left to retry, the same as the clear
        // button: the button is the control the reader just pressed, so it must not leave the DOM.
        if (_loading || !_retryRowsEnabled || _failedRows is not { } request)
        {
            return;
        }

        var previousSearch = _executedSearch;
        var previousPage = _page;
        var previousSize = _pageSize;
        var previousSort = _sort;
        var previousDirection = _direction;
        var previousFilter = _filter;
        var previousKeepPager = _keepPager;
        var previousResult = _result;
        var previousPanel = CapturePanel();

        _page = request.Page;
        _pageSize = request.Size;
        _sort = request.Sort;
        _direction = request.Direction;
        _filter = request.Filter;
        _keepPager = request.KeepPager;

        // Only this call is the retry the button offered, which is what the failure box reads to
        // decide whether to say so. Nothing derived from _loading can tell them apart: the offer
        // outlives its answer as a focus anchor, so a later page turn looks identical from there.
        _retryingRows = true;

        try
        {
            await RetryRowsFetchAsync(
                request, previousSearch, previousPage, previousSize, previousSort,
                previousDirection, previousFilter, previousKeepPager, previousResult, previousPanel);
        }
        finally
        {
            _retryingRows = false;
        }
    }

    /// <summary>The body of <see cref="RetryRowsAsync"/>, so the flag around it has one exit.</summary>
    private async Task RetryRowsFetchAsync(
        RowRequest request, string? previousSearch, int previousPage, int previousSize,
        SortField previousSort, SortDirection previousDirection, VariableFilter previousFilter,
        bool previousKeepPager, Page<VariableSummary>? previousResult, PanelState previousPanel)
    {
        // keepResult, because a failed page turn's rows are still on screen and a failed search's
        // are already gone: either way the retry must not be what empties the list.
        if (!await FetchAsync(request.Search, keepResult: true))
        {
            // The same invariant every rollback here protects: the state has to go on describing
            // the rows the reader can see, which are still the ones from before the first failure.
            _page = previousPage;
            _pageSize = previousSize;
            _sort = previousSort;
            _direction = previousDirection;
            _filter = previousFilter;
            _keepPager = previousKeepPager;

            // Same focus rule as after a success: a 429 clears the offer, and this button is the
            // element under the reader's finger. Back inert — _retryRowsEnabled stayed false — and
            // ??= so a plain failure keeps the live request the fetch above captured instead.
            _failedRows ??= request;

            return;
        }

        // A retried page turn can land on a page the result no longer has, exactly as the first
        // attempt could — the index shrinks between two requests — so it takes the same way back.
        await RetreatFromEmptyPageAsync(previousPage, previousResult, previousPanel);

        // Put back the offer the fetch above cleared, because this one button is the element under
        // the reader's finger and removing it would drop focus to <body>. Not over a retreat that
        // failed, which has left its own live request here.
        _failedRows ??= request;

        // Only what this request could have moved. _facets null is the first load's failure: the
        // rows are back and the filter panel is still not on the page at all until they arrive.
        if (_facets is null || _executedSearch != previousSearch || _filter != previousFilter)
        {
            await FetchFacetsAsync();
        }

        // Only on success, and only afterwards: what the host mirrors is what is in force, and
        // until this answer arrived that was the rolled-back state it was already told about.
        if (_sort != previousSort)
        {
            await RaiseAsync(SortChanged, _sort, Log);
        }

        if (_direction != previousDirection)
        {
            await RaiseAsync(DirectionChanged, _direction, Log);
        }

        if (_filter != previousFilter)
        {
            await RaiseAsync(FilterChanged, _filter, Log);
        }

        if (_pageSize != previousSize)
        {
            await RaiseAsync(PageSizeChanged, _pageSize, Log);
        }

        await NotifyPageChangedAsync();
    }
}
