using System.Diagnostics;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

// A signed-out reader's list kept in the browser, shown through the shared-list view (Fhi.Metadata-emsc0.3.12).
public sealed partial class VariableListView
{
    private bool _sharingBrowserList;
    private ShareMade? _browserShare;
    private bool _confirmingClear;
    private bool _focusClearToggle;

    // Into the share code once the panel shows one, as a signed-in share does (Fhi.Metadata-emsc0.3.18).
    private bool _focusBrowserShareCode;
    private bool _pressesWatched;
    private ElementReference _clearToggle;
    private bool _carryingOver;
    private bool _removing;
    private bool _carryOverFailed;
    private MuninExplorerListQuotaException? _carryOverQuota;
    private bool _carryOverAlreadySaving;
    private string? _carriedOverAs;
    private bool _carriedOverButStillHere;

    // The reader the carry-over's sentences and remove button were set for; another reader at the screen sees none.
    private int? _carryOverNoticesFor;

    // The variables Munin refused at the last press, whose rows the remove button takes out of the browser.
    // The alert counts them as the button does, from the rows this page holds; the press's own count only once none are.
    private IReadOnlySet<Guid>? _refusal;
    private (int Rows, int Variables)? _refusalSaid;
    private (bool Removed, int Rows, int Left)? _refusedRemoved;
    private ElementReference _carryOverButton;
    private ElementReference _removeRefusedButton;
    private (OfferControl Control, int Forms, int Reader)? _offerFocus;
    private bool _browserShareTooLong;
    private bool _browserPageFailed;
    private bool _browserShareUnreadable;

    // The page on screen and what it was read for: the list's version and the page number.
    private IReadOnlyList<VariableListItem> _browserPage = [];
    private (int Version, int Page, int Size)? _browserPageFor;

    // A failed read is not retried for the same page and version, or every render would ask again; a change does.
    private (int Version, int Page, int Size)? _browserPageFailedFor;
    private int _browserVersionSeen = -1;

    // A row's «Fjern» leaves with it: focus goes to the row now in its place, else the one before it, else the heading;
    // set only once the page is read again for the change, so the buttons it names are drawn (Fhi.Metadata-emsc0.3.13).
    private (int Index, int Page, int Moves)? _browserRemoveFocus;
    private bool _browserPageReading;
    private bool _browserRowNotRemoved;
    private string? _browserRemovedSaid;

    // Rows pressed and still drawn: null while the browser is asked, then the list version the removal made, until a
    // page read of that version lands. A second press meanwhile would say the row was removed twice.
    private readonly Dictionary<VariableDatasamlingKey, int?> _browserRowsRemoving = [];

    // The reader and list version a removal's sentences speak for; a sign-in, «Tøm listen» or another tab's change ends them.
    private (int Reader, int Version)? _browserRemoveNoticesFor;

    // A new element per removal, so a second one under the same name is announced again.
    private int _browserRemovedCount;

    private string ClearBrowserListId => $"munin-explorer-clear-browser-list-{_instance}";

    private string ConfirmClearId => $"munin-explorer-confirm-clear-{_instance}";

    private string RefusalSentenceId => $"munin-explorer-browser-list-refusal-{_instance}";

    /// <summary>Signed out, no shared list on screen, and the host offers the list in the browser.</summary>
    private bool ShowsBrowserCopy => !ShowsSharedList && !IsAuthenticated && State?.UsesBrowserList == true;

    private int BrowserRowCount => State?.BrowserCount ?? 0;

    /// <summary>How many rows the shared-list view draws: the snapshot's, or the browser list's.</summary>
    private int? ShownCount => _sharedList?.Items.Count ?? (ShowsBrowserCopy ? BrowserRowCount : null);

    private string? BrowserListMessage =>
        !ShowsBrowserCopy ? null
        : _browserShareTooLong ? T.BrowserListTooLongToShare
        : _browserPageFailed || _browserShareUnreadable ? T.BrowserListReadError
        : _browserRowNotRemoved && BrowserRemoveNoticesShown ? T.BrowserListRowNotRemoved
        : ShareMessage ?? DownloadMessage;

    private bool BrowserRemoveNoticesShown =>
        State is { } state && _browserRemoveNoticesFor == (state.Reader, state.BrowserVersion);

    private bool CarryOverNoticesShown => State is { } state && state.Reader == _carryOverNoticesFor;

    private string? CarryOverAlert =>
        !CarryOverNoticesShown ? null
        : _carryOverAlreadySaving ? T.CarryBrowserListOverAlreadySaving
        : _carryOverFailed ? (_carryOverQuota is { } quota ? T.ListQuota(quota) : T.CarryBrowserListOverError)
        : null;

    /// <summary>The alert says the refusal, or that its rows were not removed: the remove button's sentence.</summary>
    private bool RefusalSaidShown =>
        !_failed && CarryOverNoticesShown && (_refusedRemoved is { Removed: false } || (_refusedRemoved is null && _refusalSaid is not null));

    private string? CarriedOverMessage =>
        !CarryOverNoticesShown ? null
        : _refusedRemoved is { } removal
            ? (removal.Removed ? T.BrowserListRefusedRemoved(removal.Rows, removal.Left) : T.BrowserListRefusedNotRemoved)
        : _refusalSaid is { } said ? T.BrowserListRefused((RefusedRowsShown ?? said).Rows, (RefusedRowsShown ?? said).Variables)
        : _carriedOverAs is not { } name ? null
        : _carriedOverButStillHere ? T.BrowserListSavedButStillHere(name) : T.BrowserListSavedAs(name);

    private enum OfferControl
    {
        CarryOver,
        RemoveRefused
    }

    /// <summary>Signed in with rows in the browser, and no carry-over that saved them but could not take them out.</summary>
    private bool ShowsCarryOverOffer => State is { IsAuthenticated: true, BrowserCount: > 0, BrowserCarryOverStuck: false };

    /// <summary>The rows and variables the remove button would take out, while its sentence is the one on screen.</summary>
    private (int Rows, int Variables)? RefusedRowsShown =>
        _refusal is { } refused && RefusalSaidShown && State!.BrowserRowsOf(refused) is > 0 and var rows
            ? (rows, State.BrowserVariablesOf(refused))
            : null;

    /// <summary>The sentence over the browser list: kept, kept for this page only, or not there at all.</summary>
    private string BrowserListStorageSentence =>
        State?.BrowserListAvailable == false ? T.BrowserListUnavailable
        : State?.BrowserListForeign == true ? T.BrowserListForeign
        : State?.BrowserListKept == false ? T.BrowserListNotKept
        : T.BrowserListKeptHere;

    /// <summary>Turns the browser list on when the host does; GetService, so a host that registered no switch still draws the view.</summary>
    private void OfferBrowserList()
    {
        if (State is { OffersBrowserList: false } && ServiceProvider.GetService<CodePickerSwitch>()?.On == true)
        {
            State.OfferBrowserList(new BrowserListStore(Js));
        }
    }

    /// <summary>
    /// From <c>OnAfterRenderAsync</c>: reads the stored list once, the page on screen when it is stale, and gives
    /// focus back after «Tøm listen» or a row's «Fjern».
    /// </summary>
    private async Task AfterBrowserListRenderAsync()
    {
        if (State is not { OffersBrowserList: true } state)
        {
            return;
        }

        // A sign-in or a shared list took the view: the removal's focus would land on whatever comes back later.
        if (!ShowsBrowserCopy)
        {
            _browserRemoveFocus = null;
        }

        await state.ReadBrowserListAsync();

        // Before «Del liste» can be pressed: the share's focus move asks what was pressed last (Fhi.Metadata-emsc0.3.19).
        if (ShowsBrowserCopy && !_pressesWatched)
        {
            try
            {
                _pressesWatched = await ExplorerInterop.WatchPressesAsync(Js);
            }
            catch (JSException ex)
            {
                _pressesWatched = true;
                Log?.LogError(ex, "the module could not watch the reader's presses for the browser list's share");
            }
            catch (Exception ex)
            {
                Log?.LogWarning(ex, "could not watch the reader's presses for the browser list's share");
            }
        }

        var wanted = (state.BrowserVersion, _sharedPageNumber, PageSize);

        if (!_browserPageReading && _browserPageFor == wanted)
        {
            await FocusAfterBrowserRemoveAsync();
        }

        if (ShowsBrowserCopy && state.BrowserListRead && _browserPageFor != wanted && _browserPageFailedFor != wanted)
        {
            _browserPageFor = wanted;
            _browserPageReading = true;
            var rows = await state.ReadBrowserRowsAsync((_sharedPageNumber - 1) * PageSize, PageSize);

            if (_browserPageFor == wanted)
            {
                _browserPageReading = false;

                // A failure is remembered for this page and version only, so the next change reads it again.
                _browserPageFailed = rows is null;
                _browserPageFailedFor = rows is null ? wanted : null;
                _browserPage = rows is null ? [] : [.. rows.Select(r => r.Item)];

                foreach (var done in _browserRowsRemoving.Where(r => r.Value <= wanted.BrowserVersion).Select(r => r.Key).ToList())
                {
                    _browserRowsRemoving.Remove(done);
                }

                StateHasChanged();
            }
        }

        if (_focusBrowserShareCode)
        {
            _focusBrowserShareCode = false;

            if (ShowsBrowserCopy && _sharingBrowserList && _browserShare is not null)
            {
                // The browser says where focus is and what was pressed last: a reader who moved on while the share was out keeps it.
                try
                {
                    await ExplorerInterop.FocusIfStillOnAsync(Js, ShareToggleId, ShareCodeFieldId);
                }
                catch (JSException ex)
                {
                    Log?.LogError(ex, "the module could not move focus to the browser list's share code");
                }
                catch (Exception ex)
                {
                    Log?.LogWarning(ex, "could not move focus to the browser list's share code");
                }
            }
        }

        if (_focusClearToggle && ShowsBrowserCopy)
        {
            _focusClearToggle = false;

            try
            {
                await _clearToggle.FocusAsync();
            }
            catch (Exception ex)
            {
                Log?.LogWarning(ex, "could not move focus after emptying the browser list");
            }
        }
    }

    private async Task RemoveFromBrowserListAsync(VariableListItem item)
    {
        var key = VariableDatasamlingKey.Of(item);

        if (State is null || !ShowsBrowserCopy || !_browserRowsRemoving.TryAdd(key, null))
        {
            return;
        }

        var at = _browserPage.ToList().FindIndex(i => VariableDatasamlingKey.Of(i) == key);
        var page = _sharedPageNumber;
        var moves = _readerMoves;
        var name = RowName(item);

        ForgetFailures();
        _browserRemovedSaid = null;

        var reader = State.Reader;
        var outcome = VariableListState.BrowserListOutcome.Unavailable;

        // The search's own removal: one change, in its turn, against the stored value.
        try
        {
            outcome = await State.ToggleInBrowserListAsync(item, drawnAsSaved: true);
        }
        finally
        {
            if (outcome == VariableListState.BrowserListOutcome.Removed)
            {
                _browserRowsRemoving[key] = State.BrowserVersion;
            }
            else
            {
                _browserRowsRemoving.Remove(key);
            }
        }

        _browserRemoveNoticesFor = (reader, State.BrowserVersion);

        if (outcome != VariableListState.BrowserListOutcome.Removed)
        {
            _browserRowNotRemoved = true;
            return;
        }

        _browserRemovedSaid = T.RemovedFromList(name);
        _browserRemovedCount++;

        if (State.Reader == reader)
        {
            _browserRemoveFocus = (Math.Max(at, 0), page, moves);
        }
    }

    private async Task FocusAfterBrowserRemoveAsync()
    {
        if (_browserRemoveFocus is not { } wanted)
        {
            return;
        }

        _browserRemoveFocus = null;

        // Not once the reader has moved on during the call: a page, or a fold or question opened or shut, each a move.
        if (_readerMoves != wanted.Moves)
        {
            return;
        }

        // Back a page when the last row of the last page went: the row before it is that page's last.
        var index = wanted.Page == _sharedPageNumber ? Math.Min(wanted.Index, _browserPage.Count - 1) : _browserPage.Count - 1;
        var target = index >= 0 ? RemoveButtonId(_browserPage[index]) : ListHeadingId;

        // The browser says where focus is: a reader who Tabbed or clicked away meanwhile keeps their place.
        try
        {
            await ExplorerInterop.FocusIfLostAsync(Js, target);
        }
        catch (JSException ex)
        {
            Log?.LogError(ex, "the module could not move focus after removing a row from the browser list");
        }
        catch (Exception ex)
        {
            Log?.LogWarning(ex, "could not move focus after removing a row from the browser list");
        }
    }

    /// <summary>A change to the browser list ends a share of the rows it had, and a page past its end.</summary>
    private void FollowBrowserListChange()
    {
        if (State is not { } state || state.BrowserVersion == _browserVersionSeen)
        {
            return;
        }

        _browserVersionSeen = state.BrowserVersion;

        if (ShowsBrowserCopy)
        {
            _browserShare = null;
            _sharingBrowserList = false;
            _browserShareTooLong = false;
            _confirmingClear = false;
            _sharedPageNumber = Math.Min(_sharedPageNumber, SharedTotalPages);
        }
    }

    private void ToggleConfirmingClear()
    {
        if (BrowserRowCount == 0)
        {
            _confirmingClear = false;
            return;
        }

        _confirmingClear = !_confirmingClear;
        _readerMoves++;
    }

    private async Task ClearBrowserListAsync()
    {
        if (State is null || !ShowsBrowserCopy)
        {
            return;
        }

        ForgetFailures();
        _readerMoves++;
        await State.ClearBrowserListAsync();
        _confirmingClear = false;
        _sharedPageNumber = 1;

        // The yes button leaves with the question; focus goes back to the control that asked it.
        _focusClearToggle = true;
    }

    private async Task ToggleSharingBrowserListAsync()
    {
        if (State is null || !ShowsBrowserCopy || BrowserRowCount == 0 || _shareInFlight)
        {
            return;
        }

        ForgetFailures();
        _browserShareTooLong = false;
        _browserShareUnreadable = false;
        _sharingBrowserList = !_sharingBrowserList;
        _readerMoves++;

        if (!_sharingBrowserList || _browserShare is not null)
        {
            _focusBrowserShareCode = _sharingBrowserList;
            return;
        }

        _shareInFlight = true;
        var version = State.BrowserVersion;

        try
        {
            // Every row, or no share: a list cut short at the read would be sent as though it were whole.
            var count = State.BrowserCount;

            if (await State.ReadBrowserRowsAsync(0, count) is not { Count: > 0 } read || read.Count < count)
            {
                _shareFailure = ListActionFailure.Failed;
                _sharingBrowserList = false;
                return;
            }

            // A row too long to hand over whole came back as its code: shared, it would read as the whole row.
            if (read.Any(r => r.Truncated))
            {
                _browserShareUnreadable = true;
                _sharingBrowserList = false;
                return;
            }

            var rows = read.Select(r => r.Item).ToList();

            if (MuninExplorerClient.ShareSnapshotLength(T.FirstListName, rows) > MuninExplorerClient.MaxShareSnapshotChars)
            {
                RefuseShareAsTooLong();
                return;
            }

            var code = await Client.ShareListAsync(T.FirstListName, rows);

            // The list changed while the share was out: the code holds rows the reader no longer has.
            if (State.BrowserVersion != version)
            {
                _sharingBrowserList = false;
                return;
            }

            _browserShare = new ShareMade(Guid.Empty, code, SharedListHref?.Invoke(code));

            _focusBrowserShareCode = true;
        }
        catch (SharedSnapshotTooLargeException ex)
        {
            Log?.LogWarning(ex, "the API refused sharing the browser list as too large");
            RefuseShareAsTooLong();
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused sharing the browser list");
            _shareFailure = ListActionFailure.Throttled;
            _sharingBrowserList = false;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not share the browser list");
            _shareFailure = ListActionFailure.Failed;
            _sharingBrowserList = false;
        }
        finally
        {
            _shareInFlight = false;
        }
    }

    private void RefuseShareAsTooLong()
    {
        _browserShareTooLong = true;
        _sharingBrowserList = false;
    }

    /// <summary>The browser list's rows as one file, through the anonymous export by (variable, datasamling).</summary>
    private async Task DownloadBrowserListAsync(ExportFormat format)
    {
        if (State is null || _downloading || BrowserRowCount == 0)
        {
            return;
        }

        _downloading = true;
        _downloadingFormat = format;
        _downloadDone = false;
        ForgetFailures();

        try
        {
            if (await State.ReadBrowserKeysAsync() is not { Count: > 0 } rows)
            {
                _downloadFailure = DownloadFailure.Failed;
                return;
            }

            var file = await Client.ExportItemsAsync([.. rows.Distinct()], ExplorerPageUrl, format, _includeKodeverk);

            await BrowserDownload.OfferAsync(Js, file);
            _downloadDone = true;

            if (DownloadPanel.ClosesOnDelivery && _downloadOpen)
            {
                _downloadOpen = false;
                _menuFocus = MenuFocus.DownloadToggle;
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the {Format} export of the browser list", format);
            _downloadFailure = DownloadFailure.Throttled;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not export the browser list as {Format}", format);
            _downloadFailure = DownloadFailure.Failed;
        }
        finally
        {
            _downloading = false;
            _downloadingFormat = null;
        }
    }

    /// <summary>
    /// Saves the browser list as a new list of the reader's and shows it, all rows or none. When none, the rows stay in
    /// the browser and the alert says why; when Munin named variables, a button offers to remove their rows.
    /// </summary>
    private async Task CarryBrowserListOverAsync()
    {
        if (State is null || !IsAuthenticated || _carryingOver || _removing)
        {
            return;
        }

        _carryingOver = true;
        ForgetFailures();

        // Before the first await, as a create does: a form the reader opens meanwhile keeps the focus.
        var formsOpenAtStart = OpenForms();
        var reader = State.Reader;
        VariableListState.CarryOver? carried;

        try
        {
            // Read afresh, as the shared-list save does: the state's lists may not be read yet, or a refresh may have failed.
            var existing = await Client.GetMyListsAsync();

            // The rows are the pressing reader's: one who signed out, or was replaced, during the read gets nothing made.
            if (State.Reader != reader)
            {
                return;
            }

            carried = await State.CarryBrowserListOverAsync(FreeListName(T.FirstListName, existing));
        }
        catch (Exception ex)
        {
            _carryOverQuota = ex as MuninExplorerListQuotaException;

            if (ex is MuninExplorerRateLimitedException or MuninExplorerUnauthorizedException or MuninExplorerListQuotaException)
            {
                Log?.LogWarning(ex, "the API refused saving the browser list as a new list");
            }
            else
            {
                Log?.LogError(ex, "could not save the browser list as a new list");
            }

            carried = null;
        }
        finally
        {
            _carryingOver = false;
        }

        // Another reader is at the screen now: nothing of this press is theirs to see or be moved to.
        if (State.Reader != reader)
        {
            return;
        }

        _carryOverNoticesFor = reader;

        if (carried is null)
        {
            _carryOverFailed = true;
            return;
        }

        // Nothing made and nothing taken: the rows stay here, and the save already under way is the one that moves them.
        if (carried.AlreadyCarrying)
        {
            _carryOverAlreadySaving = true;
            return;
        }

        if (carried.List is not { } created)
        {
            // Munin named variables and saved nothing: every row stays, and the button beside the offer removes theirs.
            _refusal = carried.Refused ?? new HashSet<Guid>();
            _refusalSaid = (carried.RefusedRows, carried.Refused?.Count ?? 0);
            _offerFocus = (OfferControl.RemoveRefused, formsOpenAtStart, reader);
            return;
        }

        // As for a saved shared list: the adds went to a list that was not active, so the counts are read once.
        _holdingReloads = true;
        await CountTheAddsAsync(created.Id);
        _holdingReloads = false;

        if (State.Reader != reader)
        {
            return;
        }

        _shownList = created.Id;
        _shownListMoves++;
        _pageNumber = 1;
        ForgetListControls();

        try
        {
            await State.SetActiveListAsync(created.Id);
        }
        catch (Exception ex)
        {
            Log?.LogWarning(ex, "could not read the new list {ListId} after saving the browser list", created.Id);
        }

        if (State.Reader != reader)
        {
            return;
        }

        await LoadPageAsync();

        if (State.Reader != reader)
        {
            return;
        }

        // The button left with its offer: focus goes where a created list sends it, and the alert says what happened.
        _formsOpenAtCreate = formsOpenAtStart;
        _focusAfterCreate = true;
        _carriedOverAs = created.Name;
        _carriedOverButStillHere = !carried.LeftTheBrowser;
    }

    /// <summary>Takes the refused variables' rows out of the browser, which signed in has no «Tøm listen» to do it.</summary>
    private async Task RemoveRefusedFromBrowserAsync()
    {
        if (State is null || _carryingOver || _removing || _refusal is not { } refused || !CarryOverNoticesShown)
        {
            return;
        }

        // The sentence stays while the rows are taken out, so the button keeps it until there is a new one to say.
        _removing = true;
        var reader = State.Reader;
        var formsOpenAtStart = OpenForms();
        var rows = State.BrowserRowsOf(refused);
        bool removed;

        try
        {
            removed = await State.RemoveRefusedFromBrowserAsync(refused);
        }
        finally
        {
            _removing = false;
        }

        if (State.Reader != reader)
        {
            return;
        }

        ForgetFailures();
        _refusedRemoved = (removed, rows, State.BrowserCount);

        // The button leaves with its rows; focus goes to the offer for the rest, or where a created list's would.
        if (removed)
        {
            _offerFocus = (OfferControl.CarryOver, formsOpenAtStart, reader);
        }
        else
        {
            _refusal = refused;

            // Kept only where another page reads them, the rows are gone from this one and the button with them.
            if (State.BrowserRowsOf(refused) == 0)
            {
                _offerFocus = (OfferControl.CarryOver, formsOpenAtStart, reader);
            }
        }
    }

    /// <summary>Where focus goes after a refusal or a removal, gated as <c>TakeFocusTarget</c> is, or null.</summary>
    private ElementReference? TakeOfferFocusTarget()
    {
        if (_offerFocus is not { } wanted)
        {
            return null;
        }

        _offerFocus = null;

        if (ShowsSharedList || State is not { } state || state.Reader != wanted.Reader
            || (OpenForms() & ~wanted.Forms) != 0)
        {
            return null;
        }

        return wanted.Control switch
        {
            OfferControl.RemoveRefused => RefusedRowsShown is not null ? _removeRefusedButton : null,
            OfferControl.CarryOver => ShowsCarryOverOffer ? _carryOverButton : Lists.Count > 1 ? _listPicker : _createToggle,
            _ => throw new UnreachableException($"no focus target for {wanted.Control}"),
        };
    }

    // The list's name, made free among the reader's lists the way a reader would: «Navn (2)».
    private static string FreeListName(string name, IEnumerable<VariableList> lists)
    {
        var taken = lists.Select(l => l.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = name.Trim();

        for (var n = 2; taken.Contains(candidate); n++)
        {
            candidate = $"{name.Trim()} ({n})";
        }

        return candidate.Length > MaxListNameLength ? candidate[..MaxListNameLength] : candidate;
    }
}
