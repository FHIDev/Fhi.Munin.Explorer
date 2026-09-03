using Fhi.Munin.Explorer.Contracts;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Which row is open, which owner is drilled into, and fetching what they show.
///
/// Separate from the panels themselves: those decide how a payload is drawn, this decides which
/// payload there is to draw and what happens to it when the rows underneath change.
/// </summary>
public partial class VariableExplorer
{
    /// <summary>Whether the whole variable is showing, rather than the panel's summary of it.</summary>
    /// <remarks>
    /// A view, not a route. The package has no router, so "the full detail page" opens in place —
    /// the same move the kilde drill-in makes, and the answer to the open question on
    /// Fhi.Metadata-xbynn about how a host reaches a detail without one.
    /// </remarks>
    private bool _wholeVariable;

    private string WholeVariableId => $"munin-variable-{_instance}";

    private string WholeVariableHeadingId => $"munin-variable-heading-{_instance}";

    /// <summary>Open the whole variable, or close it and put the reader back in the list.</summary>
    /// <remarks>
    /// Nothing is fetched: the panel already holds the detail this view draws, because opening the
    /// row fetched it. Going deeper should not cost a round trip for something already in hand.
    /// </remarks>
    private Task ToggleWholeVariableAsync()
    {
        _wholeVariable = !_wholeVariable;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Open this row's detail panel, or close it when it is the one already open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One panel at a time. Opening a second row closes the first, which is what keeps the
    /// component to one fetched detail and one selection to report to the host — and what stops a
    /// long list from turning into a page of expanded cards nobody can find their way back through.
    /// </para>
    /// <para>
    /// Not dropped while a list fetch is in flight, unlike a sort or a page turn. Those all ask the
    /// same question of the same endpoint and would race each other; this one asks a different
    /// endpoint about a row that is already on screen, and making the reader wait for a slow search
    /// before a card will open would be a delay with nothing behind it. If the search does replace
    /// the rows underneath, the selection goes with them — see
    /// <see cref="DropSelectionIfGoneAsync"/>.
    /// </para>
    /// </remarks>
    private async Task ToggleDetailAsync(VariableSummary v)
    {
        if (IsSelected(v))
        {
            ClearSelection();
            await RaiseAsync<Guid?>(SelectedVariableIdChanged, null);

            return;
        }

        _selectedId = v.Id;

        // Back to the first tab for the newly opened row. A reader who was on Data for one variable
        // has not asked to be on Data for the next, and arriving on a tab you did not choose — with
        // different content under it — reads as the panel having lost your place.
        _tab = PanelTab.Details;

        await LoadDetailAsync(v.Id);

        // _selectedId rather than v.Id: the fetch above yields, so another row may have been opened
        // while it ran, and what the host is told has to be what is open — the same rule
        // FilterChanged follows after a rollback.
        await RaiseAsync(SelectedVariableIdChanged, _selectedId);
    }

    /// <summary>
    /// Fetch the detail for <paramref name="id"/> into the open panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every write back into the component is guarded by the generation this call claimed still
    /// being the current one — not by the id, which names the variable and not the call, so it
    /// cannot tell two fetches for the same row apart. Two rows opened in quick succession are two
    /// requests in flight, and
    /// nothing says the first one answers first — without the guard the slower answer would paint
    /// itself under the other row's heading, which is a panel describing a variable the reader is
    /// not looking at rather than a visibly broken one.
    /// </para>
    /// <para>
    /// The historical variables the filter is showing are asked for here too. The endpoint hides
    /// them by default, so a reader who turned "Vis historiske" on would otherwise be told that a
    /// row they can see does not exist.
    /// </para>
    /// <para>
    /// Null is not a failure — <see cref="IMuninExplorerClient"/> answers it for something that is
    /// not published — so it is reported as "not found" rather than as "try again in a moment",
    /// which is advice that would never come good.
    /// </para>
    /// </remarks>
    private async Task LoadDetailAsync(Guid id)
    {
        // Claimed before anything is written, and never reused: ownership of the panel is per call,
        // which is what the guards below compare against.
        var generation = ++_detailGeneration;

        _detail = null;
        _detailError = null;
        _detailLoading = true;

        // The owner panel is drawn from the detail being replaced, so it cannot survive the
        // replacement — opening a second row with a kilde disclosed would otherwise show that
        // kilde under the new variable's name until its own fetch landed.
        ClearSource();

        // Neither can the code lists, and the reason is sharper: the codes are fetched per variable
        // as well as per reference, so a cache kept across the replacement would answer the new
        // variable's kodeverk with the old one's codes rather than merely looking out of place.
        ClearCodes();

        StateHasChanged();

        try
        {
            var detail = await Client.GetVariableAsync(id, includeHistorical: _filter.IncludeHistorical);

            if (_detailGeneration != generation)
            {
                return;
            }

            _detail = detail;
            _detailError = detail is null ? T.DetailMissing : null;
        }
        catch (MuninExplorerRateLimitedException)
        {
            if (_detailGeneration == generation)
            {
                // Opening one row after another is what meets the limiter, so this branch is on the
                // reader's likeliest path into it. Said in the panel, same as below.
                _detailError = T.RateLimitError;
            }
        }
        catch (Exception)
        {
            if (_detailGeneration == generation)
            {
                // Said in the panel, not in the component's alert region: the rows are unaffected.
                _detailError = T.DetailError;
            }
        }
        finally
        {
            // Only when this call still owns the panel. A later selection has already set the flag
            // for its own fetch, and clearing it here would report that one as finished.
            if (_detailGeneration == generation)
            {
                _detailLoading = false;
            }
        }

        // After the panel is drawn rather than inside its fetch: a link with no name has nothing to
        // show until its codes arrive, and making the whole panel wait for them would hold back
        // every line that is ready (Fhi.Metadata-l9l2n.38).
        await LoadUnnamedCodesAsync();
    }

    /// <summary>Close the panel and forget what was fetched for it.</summary>
    private void ClearSelection()
    {
        // The whole-variable view belongs to the row that opened it. Left set, it would reappear
        // over whichever variable was opened next.
        _wholeVariable = false;

        _tab = PanelTab.Details;

        _selectedId = null;
        _detail = null;
        _detailError = null;

        // Closing is what disowns a fetch still in flight for the row that was open — the id it was
        // made for can come back, but the generation it claimed cannot.
        _detailGeneration++;

        // Cleared as well, because that abandoned fetch will not clear it: its own guard keeps it
        // from writing anything back at all.
        _detailLoading = false;

        // The owner panel hangs inside the panel being closed, so it goes with it. Left behind it
        // would be a kilde nothing draws, and the next variable opened would inherit it.
        ClearSource();

        // The code lists hang in it too, and for them "inherited by the next variable" is worse
        // than a stray panel: two variables can share a reference, so a cache left behind would
        // look right and be another variable's answer.
        ClearCodes();
    }

    /// <summary>Close the kilde or datasamling panel and forget what was fetched for it.</summary>
    private void ClearSource()
    {
        _sourceKind = null;
        _kilde = null;
        _datasamling = null;
        _sourceError = null;

        // Same reason ClearSelection bumps the detail's: closing disowns a fetch still in flight,
        // and the generation it claimed cannot come back even though the id can.
        _sourceGeneration++;
        _sourceLoading = false;
    }

    /// <summary>
    /// Open the kilde or the datasamling the variable belongs to, or close the one already open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One owner at a time, the same rule the variable panels follow: pressing Datasamling with
    /// Datakilde open swaps the panel rather than stacking a second one inside a result card.
    /// </para>
    /// <para>
    /// The id comes from the variable's own detail, which is the payload the buttons are rendered
    /// from — so a press can only ever ask for an owner the open panel names. It is re-read here
    /// rather than captured in the callback because <see cref="_detail"/> is what the panel is
    /// drawn from: an owner fetched for a variable that is no longer the open one would paint
    /// itself under the wrong heading.
    /// </para>
    /// </remarks>
    private async Task ToggleSourceAsync(SourceKind kind)
    {
        if (SourceOpen(kind))
        {
            ClearSource();

            return;
        }

        if (_detail is not { } detail || SourceIdOf(detail, kind) is not { } id)
        {
            return;
        }

        _sourceKind = kind;
        await LoadSourceAsync(kind, id);
    }

    /// <summary>
    /// Leave the open owner and narrow the list to that owner's variables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of "← Tilbake til variabler", which returns to the whole list. Runa's kilde
    /// and datasamling views both link back to the variable list pre-filtered on what the reader
    /// was looking at; this is that path, and it is the same filter the facet panel sets — no new
    /// state and no new contract, which is why it goes through <see cref="ApplyFilterAsync"/>
    /// rather than filtering the rows on the way past. A list narrowed without the filter being
    /// set would look right and leave the facet panel showing nothing to remove.
    /// </para>
    /// <para>
    /// The id is re-read from <see cref="_detail"/> for the reason
    /// <see cref="ToggleSourceAsync"/> re-reads it: that payload is what the open view was drawn
    /// from, so a press can only ever narrow to an owner the view actually names.
    /// </para>
    /// <para>
    /// Replaces that facet rather than adding to it — the button says "bare variabler fra denne
    /// datakilden", and appending to a kilde already chosen would widen the list instead. The
    /// reader's other facets are left alone: they narrowed the search this variable was found in,
    /// and clearing them silently would undo work the button never mentions.
    /// </para>
    /// <para>
    /// Closed before the filter is applied, not after: the drill-in renders <em>instead of</em>
    /// the list, so applying first would fetch rows behind a view that is still covering them.
    /// <see cref="ClearSource"/> rather than <see cref="CloseSourceAsync"/>, so that pressing this
    /// while the owner is still loading disowns that fetch instead of leaving it to land into a
    /// panel that has gone.
    /// </para>
    /// </remarks>
    private async Task ShowSourceVariablesAsync()
    {
        if (_sourceKind is not { } kind || _detail is not { } detail || SourceIdOf(detail, kind) is not { } id)
        {
            return;
        }

        var narrowed = kind == SourceKind.Kilde
            ? _filter with { KildeIds = [id] }
            : _filter with { DatasamlingIds = [id] };

        ClearSource();

        await ApplyFilterAsync(narrowed);
    }

    /// <summary>
    /// Fetch one owner into the open panel.
    /// </summary>
    /// <remarks>
    /// Guarded per call rather than per id, for the reason <see cref="LoadDetailAsync"/> is: the
    /// two endpoints are different but the hazard is the same, and swapping between Datakilde and
    /// Datasamling twice quickly is two calls whose answers can arrive in either order. Null is
    /// "the catalogue does not publish this", not a failure, so it is reported as "not found"
    /// rather than as advice to try again.
    /// </remarks>
    private async Task LoadSourceAsync(SourceKind kind, Guid id)
    {
        var generation = ++_sourceGeneration;

        _kilde = null;
        _datasamling = null;
        _sourceError = null;
        _sourceLoading = true;
        StateHasChanged();

        try
        {
            if (kind == SourceKind.Kilde)
            {
                var kilde = await Client.GetKildeAsync(id);

                if (_sourceGeneration != generation)
                {
                    return;
                }

                _kilde = kilde;
                _sourceError = kilde is null ? T.KildeMissing : null;
            }
            else
            {
                var datasamling = await Client.GetDatasamlingAsync(id);

                if (_sourceGeneration != generation)
                {
                    return;
                }

                _datasamling = datasamling;
                _sourceError = datasamling is null ? T.DatasamlingMissing : null;
            }
        }
        catch (MuninExplorerRateLimitedException)
        {
            if (_sourceGeneration == generation)
            {
                // One sentence for both kinds, unlike the branch below: which endpoint the limiter
                // refused is not what the reader has to know, and it changes nothing about waiting.
                _sourceError = T.RateLimitError;
            }
        }
        catch (Exception)
        {
            if (_sourceGeneration == generation)
            {
                // Said in the owner panel, not in the variable's above it and not in the
                // component's alert region: neither the rows nor the variable is stale because the
                // kilde endpoint was unreachable.
                _sourceError = kind == SourceKind.Kilde ? T.KildeError : T.DatasamlingError;
            }
        }
        finally
        {
            if (_sourceGeneration == generation)
            {
                _sourceLoading = false;
            }
        }
    }

    /// <summary>
    /// Close the panel when the variable it belongs to is no longer among the rows on screen.
    /// </summary>
    /// <remarks>
    /// The panel is drawn inside its own row, so a selection the current result does not contain is
    /// one nothing renders — state the reader cannot see and cannot get rid of, which would come
    /// back the moment they paged past that row again. Run after every result that arrives, so a
    /// new search, a filter, a reordering and a page turn are all covered by one rule rather than
    /// four. The host is told, because a URL naming a variable the page is not showing hands out a
    /// link that opens something else.
    /// </remarks>
    private async Task DropSelectionIfGoneAsync()
    {
        if (_selectedId is not { } id || IsOnScreen(id))
        {
            return;
        }

        ClearSelection();
        await RaiseAsync<Guid?>(SelectedVariableIdChanged, null);
    }

    private bool IsOnScreen(Guid id) => _result?.Items.Any(v => v.Id == id) is true;

    /// <summary>
    /// Open the panel the host asked for, once the first result is known.
    /// </summary>
    /// <remarks>
    /// After the search rather than before it, because whether the id is worth fetching depends on
    /// whether the row is there to draw it in. Whether it is there is not asked here, though:
    /// <see cref="FetchAsync"/> runs <see cref="DropSelectionIfGoneAsync"/> after every fetch,
    /// failed or answered, so a selection the first result does not hold has already been closed
    /// and reported as null by the time this runs. A selection still set is a row on screen, and
    /// the only thing left to do with it is fetch it.
    /// </remarks>
    private async Task OpenInitialSelectionAsync()
    {
        if (_selectedId is not { } id)
        {
            return;
        }

        await LoadDetailAsync(id);
    }
}
