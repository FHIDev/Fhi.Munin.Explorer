using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

// A signed-out reader's list kept in the browser, shown through the shared-list view (Fhi.Metadata-emsc0.3.12).
public sealed partial class VariableListView
{
    private bool _sharingBrowserList;
    private ShareMade? _browserShare;
    private bool _confirmingClear;
    private bool _focusClearToggle;
    private ElementReference _clearToggle;
    private bool _carryingOver;
    private bool _carryOverFailed;
    private string? _carriedOverAs;
    private bool _carriedOverButStillHere;
    private int _notCarried;
    private bool _browserShareTooLong;
    private bool _browserPageFailed;
    private bool _browserShareUnreadable;

    // The page on screen and what it was read for: the list's version and the page number.
    private IReadOnlyList<VariableListItem> _browserPage = [];
    private (int Version, int Page, int Size)? _browserPageFor;

    // A failed read is not retried for the same page and version, or every render would ask again; a change does.
    private (int Version, int Page, int Size)? _browserPageFailedFor;
    private int _browserVersionSeen = -1;

    private string ClearBrowserListId => $"munin-explorer-clear-browser-list-{_instance}";

    private string ConfirmClearId => $"munin-explorer-confirm-clear-{_instance}";

    /// <summary>Signed out, no shared list on screen, and the host offers the list in the browser.</summary>
    private bool ShowsBrowserCopy => !ShowsSharedList && !IsAuthenticated && State?.UsesBrowserList == true;

    private int BrowserRowCount => State?.BrowserCount ?? 0;

    /// <summary>How many rows the shared-list view draws: the snapshot's, or the browser list's.</summary>
    private int? ShownCount => _sharedList?.Items.Count ?? (ShowsBrowserCopy ? BrowserRowCount : null);

    private string? BrowserListMessage =>
        !ShowsBrowserCopy ? null
        : _browserShareTooLong ? T.BrowserListTooLongToShare
        : _browserPageFailed || _browserShareUnreadable ? T.BrowserListReadError
        : ShareMessage ?? DownloadMessage;

    private string? CarriedOverMessage =>
        _carriedOverAs is not { } name ? null
        : (_carriedOverButStillHere ? T.BrowserListSavedButStillHere(name) : T.BrowserListSavedAs(name))
          + (_notCarried > 0 ? " " + T.BrowserListNotCarried(_notCarried) : "");

    /// <summary>The sentence over the browser list: kept, kept for this page only, or not there at all.</summary>
    private string BrowserListStorageSentence =>
        State?.BrowserListAvailable == false ? T.BrowserListUnavailable
        : State?.BrowserListForeign == true ? T.BrowserListForeign
        : State?.BrowserListKept == false ? T.BrowserListNotKept
        : T.BrowserListKeptHere;

    /// <summary>Turns the browser list on when the host does; GetService, so a host with neither still draws the view.</summary>
    private void OfferBrowserList()
    {
        if (State is { OffersBrowserList: false } && ServiceProvider.GetService<AnonymousListsSwitch>()?.On == true)
        {
            State.OfferBrowserList(new BrowserListStore(Js));
        }
    }

    /// <summary>
    /// From <c>OnAfterRenderAsync</c>: reads the stored list once, the page on screen when it is stale, and gives
    /// focus back after «Tøm listen».
    /// </summary>
    private async Task AfterBrowserListRenderAsync()
    {
        if (State is not { OffersBrowserList: true } state)
        {
            return;
        }

        await state.ReadBrowserListAsync();

        var wanted = (state.BrowserVersion, _sharedPageNumber, PageSize);

        if (ShowsBrowserCopy && state.BrowserListRead && _browserPageFor != wanted && _browserPageFailedFor != wanted)
        {
            _browserPageFor = wanted;
            var rows = await state.ReadBrowserRowsAsync((_sharedPageNumber - 1) * PageSize, PageSize);

            if (_browserPageFor == wanted)
            {
                // A failure is remembered for this page and version only, so the next change reads it again.
                _browserPageFailed = rows is null;
                _browserPageFailedFor = rows is null ? wanted : null;
                _browserPage = rows is null ? [] : [.. rows.Select(r => r.Item)];
                StateHasChanged();
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
    }

    private async Task ClearBrowserListAsync()
    {
        if (State is null || !ShowsBrowserCopy)
        {
            return;
        }

        ForgetFailures();
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

        if (!_sharingBrowserList || _browserShare is not null)
        {
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
    /// Saves the browser list as a new list of the reader's and shows it. A failure leaves the browser list as it
    /// was and says so; nothing is merged into a list the reader already has.
    /// </summary>
    private async Task CarryBrowserListOverAsync()
    {
        if (State is null || !IsAuthenticated || _carryingOver)
        {
            return;
        }

        _carryingOver = true;
        _carryOverFailed = false;
        ForgetFailures();

        // Before the first await, as a create does: a form the reader opens meanwhile keeps the focus.
        var formsOpenAtStart = OpenForms();
        var reader = State.Reader;
        VariableListState.CarryOver? carried;

        try
        {
            carried = await State.CarryBrowserListOverAsync(FreeListName(T.FirstListName));
        }
        catch (Exception ex)
        {
            if (ex is MuninExplorerRateLimitedException or MuninExplorerUnauthorizedException)
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

        if (carried is not { List: var created })
        {
            _carryOverFailed = true;
            return;
        }

        // As for a saved shared list: the adds went to a list that was not active, so the counts are read once.
        _holdingReloads = true;
        await CountTheAddsAsync(created.Id);
        _holdingReloads = false;

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

        await LoadPageAsync();

        // The button left with its offer: focus goes where a created list sends it, and the alert says what happened.
        _formsOpenAtCreate = formsOpenAtStart;
        _focusAfterCreate = true;
        _carriedOverAs = created.Name;
        _carriedOverButStillHere = !carried.LeftTheBrowser;
        _notCarried = carried.NotCarried;
    }

    // The list's name, made free among the reader's lists the way a reader would: «Navn (2)».
    private string FreeListName(string name)
    {
        var taken = Lists.Select(l => l.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = name.Trim();

        for (var n = 2; taken.Contains(candidate); n++)
        {
            candidate = $"{name.Trim()} ({n})";
        }

        return candidate.Length > MaxListNameLength ? candidate[..MaxListNameLength] : candidate;
    }
}
