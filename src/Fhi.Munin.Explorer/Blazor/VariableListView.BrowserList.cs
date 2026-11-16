using Fhi.Munin.Explorer.Contracts;
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

    private string ClearBrowserListId => $"munin-explorer-clear-browser-list-{_instance}";

    private string ConfirmClearId => $"munin-explorer-confirm-clear-{_instance}";

    /// <summary>Signed out, no shared list on screen, and the host offers the list in the browser.</summary>
    private bool ShowsBrowserCopy => !ShowsSharedList && !IsAuthenticated && State?.UsesBrowserList == true;

    private int BrowserRowCount => State?.BrowserItems.Count ?? 0;

    /// <summary>What the shared-list view draws: the snapshot behind a code, or the browser list.</summary>
    private SharedList? ShownSnapshot =>
        _sharedList ?? (ShowsBrowserCopy ? new SharedList(State!.BrowserListName ?? T.FirstListName, State.BrowserItems) : null);

    private string? BrowserListMessage => ShowsBrowserCopy ? ShareMessage ?? DownloadMessage : null;

    /// <summary>Turns the browser list on when the host does; GetService, so a host with neither still draws the view.</summary>
    private void OfferBrowserList()
    {
        if (State is not null && ServiceProvider.GetService<AnonymousListsSwitch>()?.On == true)
        {
            State.OfferBrowserList(new BrowserListStore(Js));
        }
    }

    /// <summary>From <c>OnAfterRenderAsync</c>: reads the stored list once, and gives focus back after «Tøm listen».</summary>
    private async Task AfterBrowserListRenderAsync()
    {
        if (State is { OffersBrowserList: true } state)
        {
            await state.ReadBrowserListAsync();
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
        if (!ShowsBrowserCopy)
        {
            return;
        }

        _browserShare = null;
        _sharingBrowserList = false;
        _sharedPageNumber = Math.Min(_sharedPageNumber, SharedTotalPages);
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

        _sharingBrowserList = !_sharingBrowserList;

        if (!_sharingBrowserList || _browserShare is not null)
        {
            return;
        }

        ForgetFailures();
        _shareInFlight = true;

        try
        {
            var name = State.BrowserListName ?? T.FirstListName;
            var code = await Client.ShareListAsync(name, [.. State.BrowserItems]);
            _browserShare = new ShareMade(Guid.Empty, code, SharedListHref?.Invoke(code));
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
            var rows = State.BrowserItems.Select(VariableDatasamlingKey.Of).Distinct().ToList();
            var file = await Client.ExportItemsAsync(rows, ExplorerPageUrl, format, _includeKodeverk);

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
        VariableList? created;

        try
        {
            created = await State.CarryBrowserListOverAsync(FreeListName(State.BrowserListName ?? T.FirstListName));
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

            created = null;
        }
        finally
        {
            _carryingOver = false;
        }

        if (created is null)
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
    }

    // The name the browser list had, made free among the reader's lists the way a reader would: «Navn (2)».
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
