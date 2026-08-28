using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Every fetch of the result list - the first one, and each search, sort and page turn after it -
/// together with the state that has to survive one.
/// </summary>
public partial class VariableExplorer
{
    protected override async Task OnInitializedAsync()
    {
        _search = Search;
        _filter = Filter ?? VariableFilter.None;
        _selectedId = SelectedVariableId;
        _sort = Sort;
        _direction = Direction;
        _page = Math.Max(Page, 1);
        _pageSize = PageSize;

        // Not SearchAsync: that is what a person pressing the search button does, and it starts by
        // throwing away the page number because a new search renumbers everything. Restoring a
        // shared link is the opposite — the page is the part worth keeping.
        if (await FetchAsync(_search))
        {
            await FetchFacetsAsync();
        }

        await LandOnRealPageAsync();

        // Both echoed back on mount, as SearchAsync did when it ran this path. The search echo is a
        // no-op for a host that just supplied it, but it is existing behaviour and not this
        // change's to remove. The page echo is not a no-op: LandOnRealPageAsync above may have moved
        // the reader off a page the link asked for and the result set no longer has, and the host
        // is holding the number from the link until it is told otherwise.
        await NotifySearchChangedAsync();
        await NotifyPageChangedAsync();

        await OpenInitialSelectionAsync();
    }

    /// <summary>Empty the box and run the search that leaves, which is no search at all.</summary>
    private async Task ClearSearchAsync()
    {
        // Guard before mutation, the rule SortAsync states: clearing _search and then being dropped
        // by SearchAsync's own _loading check would leave an empty box over the old rows, with the
        // host still holding the previous search. (Fhi.Metadata-5ghur)
        if (_loading || string.IsNullOrWhiteSpace(_search))
        {
            return;
        }

        _search = null;

        await SearchAsync();
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
    /// field starts ascending. Runa's rule, moved off the column header it used to live on.
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

        // _executedSearch, not _search. Sorting is not searching: a click blurs the field first, so
        // by the time this runs the box's contents have already been written to _search — text the
        // user may never have submitted. Fetching with it would run a search nobody asked for,
        // quietly, under a status line that then described the accidental search instead of saying
        // anything moved. It would also desynchronise the host, whose URL only follows SearchChanged.
        if (!await FetchAsync(_executedSearch))
        {
            // The same invariant the _loading guard above protects, on the path that guard cannot
            // see: the list is still in the old order, so the buttons have to say so. Left moved,
            // they would claim an order the API never delivered — and pressing the same button
            // again would take the reversal branch and ask for descending, with no way back to the
            // ascending fetch that just failed short of cycling twice.
            _sort = previousSort;
            _direction = previousDirection;
            _page = previousPage;
            _keepPager = previousKeepPager;

            return;
        }

        await RaiseAsync(SortChanged, _sort);
        await RaiseAsync(DirectionChanged, _direction);

        // Reordering renumbered the pages and sent the reader back to the first one.
        await NotifyPageChangedAsync();
    }

    /// <summary>
    /// Show page <paramref name="page"/> of the current result, keeping the search and the order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one way the page number ever changes, which is what the pager's two buttons, the clamp
    /// and a future URL-backed page all go through. Both buttons hand it an out-of-range number at
    /// the ends of the list rather than being guarded at the call site, so the boundary is enforced
    /// once, here, instead of once per caller.
    /// </para>
    /// <para>
    /// Not a search, so <see cref="SearchChanged"/> is not raised: the host's URL follows what was
    /// searched for, and turning a page did not change that.
    /// </para>
    /// </remarks>
    private async Task GoToPageAsync(int page)
    {
        // Dropped rather than queued while a fetch is in flight, the same as a second submit and a
        // sort click — and for the same reason the buttons carry aria-disabled instead of disabled:
        // neither is taken out of the document under the finger that pressed it, which is also why
        // a failed page turn below keeps the rows it already had.
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

        // All three kept so a failed fetch can put them back. The result as well as the number,
        // because the retreat below turns a second page and has to be able to undo both of them
        // together — and the panel with them, because the retreat's route passes through an empty
        // answer that closes it on the way.
        var previous = _page;
        var previousResult = _result;
        var previousPanel = CapturePanel();

        // A pager button was pressed, so the pager stays until a search or a sort replaces the
        // result — including through a retreat that lands on a single-page answer.
        _keepPager = true;

        _page = target;

        // keepResult: the pressed button must survive the failure. The rest of the component
        // never removes a control the user just used, and the pager is the only pressable thing in
        // it that is rendered conditionally — so a page turn that cleared the rows would take
        // Forrige and Neste out of the document in the same render that reports the error, drop
        // focus to <body>, and leave a keyboard user restarting from the top of the host's page.
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
    /// Show <paramref name="size"/> rows per page, from the first page of the result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Back to page 1, always. A change of size renumbers the rows, so page 3 of the old paging and
    /// page 3 of the new one are not the same rows: keeping the number would move the reader
    /// somewhere they never asked to go and leave them looking for their place in a result that no
    /// longer has one. A change of search or of sort resets the page for the same reason.
    /// </para>
    /// <para>
    /// The pager is kept afterwards the way a page turn keeps it. A larger size can collapse a
    /// three-page result into one, and dropping the pager in that render would take the button the
    /// reader just pressed out of the document, along with the only control that could put the size
    /// back.
    /// </para>
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
        await RaiseAsync(PageSizeChanged, _pageSize);
        await NotifyPageChangedAsync();
    }

    /// <summary>
    /// Step back to a page that has rows, when the page just fetched turned out not to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clamp in <see cref="GoToPageAsync"/> measures the target against the count the
    /// <em>previous</em> answer carried, so it can only ever ask for a page that existed when that
    /// answer was written. Two routes lead past it: the index shrinks between the two requests, and
    /// the API answers an out-of-range page with 404 — which
    /// <see cref="IMuninExplorerClient.SearchVariablesAsync"/> reports as an empty page rather than
    /// throwing, so no rollback runs.
    /// </para>
    /// <para>
    /// Left alone, either one strands the reader: the status line would say "Ingen variabler passet
    /// søket" over a search that matched hundreds, with no rows to show and nothing but a fresh
    /// search to get back from. So the component takes itself back to a page that exists — the last
    /// one the new answer admits to, or page 1, which is the one page that can never be out of
    /// range. One step only: a second empty answer is not retreated from again, so the reader is
    /// left on that page with the pager still under their finger rather than walking backwards
    /// through the result a page at a time.
    /// </para>
    /// <para>
    /// And its own fetch is checked like every other one. <paramref name="previous"/>,
    /// <paramref name="previousResult"/> and <paramref name="previousPanel"/> are the page turn's
    /// starting point — a page that had rows on it, and whatever was open among them — so a retreat
    /// that fails puts the reader back where they pressed the button instead of leaving
    /// <c>_page</c> naming one page while the empty answer for another is still on screen. That
    /// pairing is what would otherwise report "Ingen variabler passet søket" over a search that
    /// matched hundreds and take the pager with it, which is the exact state this method exists to
    /// prevent. The panel is part of the same undo: the empty answer closed it on the way past, and
    /// a rollback that put the rows back without it would leave the reader looking at the row they
    /// opened, shut, with their URL no longer naming it.
    /// </para>
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
    private readonly record struct PanelState(Guid? Id, VariableDetail? Detail, string? Error, SourceState Source);

    /// <summary>What is open in the kilde or datasamling panel inside it, and what was fetched.</summary>
    private readonly record struct SourceState(
        SourceKind? Kind, KildeDetail? Kilde, DatasamlingDetail? Datasamling, string? Error);

    private PanelState CapturePanel() => new(_selectedId, _detail, _detailError, CaptureSource());

    private SourceState CaptureSource() => new(_sourceKind, _kilde, _datasamling, _sourceError);

    /// <summary>
    /// Reopen a panel that a fetch closed on its way through, when that fetch then failed.
    /// </summary>
    /// <remarks>
    /// The fetched detail goes back rather than being asked for again, for the reason the previous
    /// result does: it is the answer that described these very rows, and putting a second request
    /// in the way of a rollback would let one failure turn into two. The exception is a panel
    /// captured while its own fetch was still running — it has no answer to put back, so that one
    /// is fetched, and the host waits for that fetch before being told: what is raised is the
    /// selection as it stands afterwards, which on a slow re-fetch the reader may have moved.
    /// The host is told at all because it was told null on the way in.
    /// </remarks>
    private async Task RestorePanelAsync(PanelState panel)
    {
        if (panel.Id is not { } id || _selectedId == id)
        {
            return;
        }

        _selectedId = id;
        _detail = panel.Detail;
        _detailError = panel.Error;

        // A new owner of the panel: whatever was in flight when it closed must not land in the one
        // just put back.
        _detailGeneration++;
        _detailLoading = false;

        if (panel.Detail is null && panel.Error is null)
        {
            await LoadDetailAsync(id);
        }

        // After the detail, for the reason the detail comes after the rows: the owner panel is
        // drawn inside the variable's, and LoadDetailAsync clears it on its way through.
        await RestoreSourceAsync(panel.Source, id);

        // _selectedId rather than id, for the reason ToggleDetailAsync gives: the fetch above
        // yields with the rows already back on screen and clickable, so another row may have been
        // opened while it ran, and what the host is told has to be what is open.
        await RaiseAsync(SelectedVariableIdChanged, _selectedId);
    }

    /// <summary>
    /// Put the kilde or datasamling panel back alongside the variable panel it hung inside.
    /// </summary>
    /// <remarks>
    /// Same reasoning as <see cref="RestorePanelAsync"/>, one level down: the rollback exists so a
    /// failed page turn does not leave the reader on the row they had opened, shut — and a reader
    /// who had opened the kilde inside it was two presses in, not one. The fetched payload goes
    /// back rather than being asked for again, except when it had not arrived yet, which is the one
    /// case with nothing to put back.
    /// <para>
    /// Guarded on the selection still being the restored row: the detail above may have been
    /// re-fetched, and that yields with the rows clickable, so the reader can have opened another
    /// variable in the meantime. Restoring an owner into that one would name the wrong kilde under
    /// the wrong variable.
    /// </para>
    /// </remarks>
    private async Task RestoreSourceAsync(SourceState source, Guid id)
    {
        if (source.Kind is not { } kind || _selectedId != id)
        {
            return;
        }

        _sourceKind = kind;
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

        // Nothing had arrived when it closed, so it has to be asked for again — and only the
        // restored detail can say which id to ask for. A detail that came back without one is a
        // panel with nothing to open, so the owner closes rather than hanging empty.
        if (_detail is { } detail && SourceIdOf(detail, kind) is { } sourceId)
        {
            await LoadSourceAsync(kind, sourceId);
        }
        else
        {
            ClearSource();
        }
    }

    /// <summary>
    /// Tell the host what was searched for, so it can reflect it in its own URL.
    /// </summary>
    /// <remarks>
    /// Raised whether or not the fetch succeeded, which is what <see cref="SearchChanged"/>
    /// documents: a host whose URL kept the previous query after a failed search would hand out a
    /// link that reloads into a different search than the box on screen is showing.
    /// </remarks>
    private Task NotifySearchChangedAsync() => RaiseAsync(SearchChanged, _search);

    /// <summary>
    /// Move to the last real page when a restored link asks for one past the end.
    /// </summary>
    /// <remarks>
    /// A link outlives the result set it was made from. Someone shares page 40, a filter is
    /// tightened or rows are unpublished, and the link now points past the end.
    /// <para>
    /// The API does not clamp: asked for page 99999 of 734 it answers with page 99999 and no rows,
    /// which is truthful and useless. The reader gets an empty list under "Side 99999 av 734" and
    /// nothing to press, because the pager's Next is already at the end and Previous steps back one
    /// page at a time from 99999. Found by opening such a link rather than by any test — the stub
    /// in the suite had never been asked for a page it did not have.
    /// </para>
    /// <para>
    /// Only on this path. A page turn cannot overshoot, because the pager clamps what it asks for,
    /// and an emptied page reached by turning is <see cref="RetreatFromEmptyPageAsync"/>'s job —
    /// that one has rollback state to unwind, which a first render does not.
    /// </para>
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
    private Task NotifyPageChangedAsync() => RaiseAsync(PageChanged, _page);

    /// <summary>
    /// Hand a value to one of the host's callbacks without letting the host's own failure out.
    /// </summary>
    /// <remarks>
    /// Shared by <see cref="SearchChanged"/> and <see cref="FilterChanged"/>, because what has to be
    /// survived is the same for both: the handler is the host's, and what it most often does is
    /// rewrite a URL.
    /// </remarks>
    private static async Task RaiseAsync<TValue>(EventCallback<TValue> callback, TValue value)
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
        catch (Exception)
        {
            // The host's handler threw, and a NavigationManager call or a CMS URL rewrite is
            // exactly the kind that does. Left unhandled it would propagate out of Blazor's event
            // dispatch — and this same path runs from OnInitializedAsync, so during initial render
            // too. In helsedata's legacy Blazor Server host inside Optimizely that tears down the
            // circuit for the whole CMS page, not just this component.
            //
            // Nothing is said to the reader on top of what the search already reported for itself,
            // success or failure. What broke here is the host's own URL, which is the host's bug to
            // find in the host's logs — and reporting it as "Kunne ikke hente variabler" would
            // blame the API for a call the API was never part of.
        }
    }

    /// <summary>
    /// Fetch <paramref name="search"/> at the current page and ordering, and settle what the new
    /// rows mean for the open detail panel. True when the fetch succeeded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The panel is settled here rather than at the five call sites, which is what makes "the
    /// selection is always a row on screen" one rule instead of five. It is outside the fetch's own
    /// try/catch on purpose: the host's callback runs in it, and a host that navigates from its
    /// handler signals that with an exception the catch would otherwise swallow and report as a
    /// failed search.
    /// </para>
    /// <para>
    /// Settled after a failure too, not only after an answer. A search or a sort that fails clears
    /// the rows, so the panel leaves the document with them — and a selection left set behind it is
    /// the invisible, unremovable state <see cref="DropSelectionIfGoneAsync"/> exists to prevent,
    /// with the host's URL still naming a variable the page is not showing. A page turn fails with
    /// <paramref name="keepResult"/>, so its rows and its panel are both still there and the check
    /// finds nothing to drop.
    /// </para>
    /// </remarks>
    private async Task<bool> FetchAsync(string? search, bool keepResult = false)
    {
        var fetched = await FetchRowsAsync(search, keepResult);

        await DropSelectionIfGoneAsync();

        return fetched;
    }

    /// <summary>Fetch <paramref name="search"/> at the current page and ordering. True when it succeeded.</summary>
    /// <remarks>
    /// <para>
    /// The search is a parameter rather than read from <c>_search</c>, because the two callers do
    /// not mean the same thing by it: searching means the live contents of the box, sorting means
    /// the text the visible rows actually came from.
    /// </para>
    /// <para>
    /// <paramref name="keepResult"/> keeps the rows already on screen when the call fails,
    /// which is what a page turn wants and a search does not. A search that failed has no result
    /// to describe — the rows on screen came from a different query, and leaving them there under
    /// the new search's error message would say they answered it. A page turn's rows came from the
    /// query that is still on screen, so they stay, and with them the pager button the reader is
    /// standing on.
    /// </para>
    /// </remarks>
    private async Task<bool> FetchRowsAsync(string? search, bool keepResult = false)
    {
        _loading = true;
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
            _executedSearch = Trimmed(search);

            // The page we are on is the page that arrived, not the page that was asked for. A
            // server that clamps page 12 to page 8 and says so has answered truthfully, and
            // ResultPage already counts the row range from its answer — leaving _page at 12 would
            // caption those rows "Side 12 av 8" and, worse, keep Neste enabled against a number
            // the server disowned, so every further press would walk the position further from the
            // rows without ever moving them. One page number for the caption, the two buttons and
            // the range, taken from the same place.
            _page = ResultPage;

            // The offer belongs to the failure it answers. Left standing after a fetch someone
            // else started came back, it is a dead control that the atomic alert region reads out
            // again beside every later failure — RetryRowsAsync puts its own back, and says why.
            _failedRows = null;

            return true;
        }
        catch (Exception ex)
        {
            // One branch for both failures, because everything except the sentence is the same: say
            // what the reader can do about it and clear the rows. The detail belongs in the host's
            // logs, not on the page.
            //
            // A 429 gets its own sentence because the answer differs — the catalogue is up and the
            // reader has asked too often, so pressing Søk again at once is the one thing that cannot
            // help, which is exactly what the generic text advises.
            //
            // The rows are cleared either way. Leaving the previous page under a failed search would
            // caption somebody else's result with this search's terms; clearing them says nothing
            // about hits, because the summary line only speaks when there is a result at all
            // (VariableExplorer.razor:275).
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
        }
    }

    /// <summary>The row request that failed, so the retry button can send that one again.</summary>
    /// <remarks>
    /// Not the fields as they stand when the button is pressed. Every caller that fails rolls its
    /// own state back to describe the rows still on screen, so by then <c>_page</c> is the page the
    /// reader never left and <c>_sort</c> the order they are still looking at. Retrying from those
    /// would re-fetch what is already there and clear the error, reporting success for a page turn
    /// or a reordering that never happened.
    /// <para>
    /// <c>KeepPager</c> travels with <c>Page</c>, because every handler that moves one moves the
    /// other: a sort or a narrowing renumbers to page one and takes the pager down with it.
    /// </para>
    /// <para>
    /// <c>Size</c> is here for the same reason the page is, and it is the one field a reader could
    /// not otherwise get back to. A failed size change rolls the size back to describe the rows
    /// still on screen, so a retry replaying the fields as they stand would fetch the old size,
    /// succeed, and clear the error — reporting a size change that never happened, from the one
    /// control the reader has no other way to press again once the pager is gone.
    /// </para>
    /// </remarks>
    private readonly record struct RowRequest(
        string? Search,
        int Page,
        int Size,
        SortField Sort,
        SortDirection Direction,
        VariableFilter Filter,
        bool KeepPager);

    /// <summary>Send the row request that failed once more, unchanged.</summary>
    /// <remarks>
    /// <para>
    /// Deliberately not <see cref="SearchAsync"/>. That is a new search — page one, and the live
    /// contents of the box, which the reader may have typed into while the error sat on screen.
    /// What failed was one particular request, and that is the one the button offers to repeat.
    /// </para>
    /// <para>
    /// The counts follow only when the retried request moved the selection they describe — a search
    /// or a filter change, which they are cross-filtered against. A failed refresh of them reports
    /// itself separately, so re-asking after a page turn or a sort would answer that message with
    /// the other one's request; not asking after a search or a narrowing would leave the numbers
    /// describing a selection nothing on screen is in any more, and say nothing about it.
    /// </para>
    /// <para>
    /// The host is told what moved and nothing else, the way each original handler is. A retried
    /// page turn raises <see cref="PageChanged"/> alone: the other three carry values the host was
    /// told during the rollback and the retried request never touched, and a host rewriting a URL
    /// per callback would take three more history entries for a page turn.
    /// </para>
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
            await RaiseAsync(SortChanged, _sort);
        }

        if (_direction != previousDirection)
        {
            await RaiseAsync(DirectionChanged, _direction);
        }

        if (_filter != previousFilter)
        {
            await RaiseAsync(FilterChanged, _filter);
        }

        if (_pageSize != previousSize)
        {
            await RaiseAsync(PageSizeChanged, _pageSize);
        }

        await NotifyPageChangedAsync();
    }

    private static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? Period(VariableSummary v) => Period(v.DataFrom, v.DataTo);

    /// <summary>
    /// The years a variable has data for, as the cards and the detail panel both write it.
    /// </summary>
    /// <remarks>
    /// Shared so a row and the panel opened from it cannot word the same period differently — the
    /// two dates come from different payloads, but the sentence they are written into is one.
    /// </remarks>
    private static string? Period(DateTimeOffset? dataFrom, DateTimeOffset? dataTo)
    {
        var from = dataFrom?.Year.ToString();
        var to = dataTo?.Year.ToString();
        return (from, to) switch
        {
            (null, null) => null,
            (not null, null) => $"{from}–",
            (null, not null) => $"–{to}",
            _ => from == to ? from! : $"{from}–{to}"
        };
    }
}
