using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

// Sharing a list as a code, and opening a list somebody shared (Fhi.Metadata-ntpbd.1).
public sealed partial class VariableListView
{
    /// <summary>
    /// The code of a shared list to show in place of the reader's own, or null. Upper-cased codes
    /// and lower-cased ones open the same list; a malformed one says it found nothing.
    /// </summary>
    /// <remarks>
    /// Bind it (<c>@bind-ShareCode</c>) to put the code in the host's address: the view raises
    /// <see cref="ShareCodeChanged"/> when the reader opens a code from the field, closes the
    /// shared list, or saves it as their own. Unbound, the field still works within the page.
    /// A shared list is shown signed out too, read-only; nothing under <c>my/lists</c> is asked
    /// for then. <see cref="VariableExplorer"/> binds it to <c>?delekode=</c>.
    /// </remarks>
    [Parameter] public string? ShareCode { get; set; }

    /// <summary>Raised with the code the view now shows, or null when it shows none.</summary>
    [Parameter] public EventCallback<string?> ShareCodeChanged { get; set; }

    /// <summary>
    /// An address that opens the shared list with a given code, offered beside the code when the
    /// reader shares a list. Null offers the code alone. <see cref="VariableExplorer"/> passes one
    /// carrying <c>?delekode=</c>, unless the host declined that key.
    /// </summary>
    [Parameter] public Func<string, string>? SharedListHref { get; set; }

    /// <summary>The last <see cref="ShareCode"/> the host passed, so only a change is followed.</summary>
    private string? _hostShareCode;

    /// <summary>The code the view is showing or trying to show, normalised.</summary>
    private string? _openCode;

    private SharedList? _sharedList;
    private int _sharedPageNumber = 1;
    private SharedListFailure _sharedListFailure;
    private string _failedCode = "";

    private bool _openingShared;
    private string _sharedCodeInput = "";

    private bool _sharingList;
    private bool _shareInFlight;
    private ShareMade? _shareMade;
    private ListActionFailure _shareFailure;

    private bool _savingShared;
    private string _saveSharedName = "";
    private SaveNameProblem _saveNameProblem;
    private ListActionFailure _saveSharedFailure;

    /// <summary>A code made for one list, and the link that opens it when there is one.</summary>
    private sealed record ShareMade(Guid ListId, string Code, string? Link);

    private enum SharedListFailure
    {
        None = 0,
        NotFound,
        Failed,
        Throttled
    }

    private string ShareToggleId => $"munin-explorer-share-toggle-{_instance}";

    /// <summary>"Listen er tom", which every control an empty list refuses is described by.</summary>
    private string ListEmptyReasonId => $"munin-explorer-list-empty-{_instance}";

    private string ShareCodeFieldId => $"munin-explorer-share-code-{_instance}";

    private string ShareLinkFieldId => $"munin-explorer-share-link-{_instance}";

    private string OpenSharedToggleId => $"munin-explorer-open-shared-toggle-{_instance}";

    private string SharedCodeFieldId => $"munin-explorer-shared-code-{_instance}";

    private string SaveSharedToggleId => $"munin-explorer-save-shared-toggle-{_instance}";

    private string SaveSharedNameId => $"munin-explorer-save-shared-name-{_instance}";

    private string SaveSharedNameProblemId => $"munin-explorer-save-shared-problem-{_instance}";

    /// <summary>
    /// Whether the shared list takes the view: once it has arrived, and signed out from the moment
    /// a code is present, since a signed-out reader has no lists of their own to fall back on.
    /// </summary>
    private bool ShowsSharedList => _openCode is not null && (_sharedList is not null || !IsAuthenticated);

    private bool ShownListIsEmpty => ShownList is { VariableCount: 0 };

    private string SharedListName =>
        _sharedList?.Name is { Length: > 0 } name ? name : T.SharedListEyebrow;

    private string? SharedListMessage => _sharedListFailure switch
    {
        SharedListFailure.NotFound => T.SharedListNotFound(_failedCode),
        SharedListFailure.Throttled => T.RateLimitError,
        SharedListFailure.Failed => T.SharedListError,
        _ => null
    };

    private string? ShareMessage => _shareFailure switch
    {
        ListActionFailure.Throttled => T.RateLimitError,
        ListActionFailure.Failed => T.ShareError,
        _ => null
    };

    private string? SaveSharedMessage => _saveSharedFailure switch
    {
        ListActionFailure.Throttled => T.RateLimitError,
        ListActionFailure.Failed => T.SaveError,
        _ => null
    };

    private string? SaveNameMessage => NameProblemMessage(_saveNameProblem);

    private IReadOnlyList<VariableListItem> SharedPageItems =>
        _sharedList is null
            ? []
            : [.. _sharedList.Items.Skip((_sharedPageNumber - 1) * PageSize).Take(PageSize)];

    private int SharedTotalPages =>
        _sharedList is null ? 1 : Math.Max(1, (_sharedList.Items.Count + PageSize - 1) / Math.Max(1, PageSize));

    private string ShareMailTo(ShareMade made)
    {
        var subject = T.ShareMailSubject(ShownList?.Name ?? "");
        var body = T.ShareMailBody(made.Code, made.Link);

        return $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
    }

    private void ForgetSharingFailures()
    {
        _sharedListFailure = SharedListFailure.None;
        _shareFailure = ListActionFailure.None;
        _saveSharedFailure = ListActionFailure.None;
    }

    /// <summary>Follows a code the host passed, leaving one the view already shows alone.</summary>
    private async Task FollowShareCodeAsync()
    {
        if (string.Equals(ShareCode, _hostShareCode, StringComparison.Ordinal))
        {
            return;
        }

        _hostShareCode = ShareCode;

        var normalized = SharedList.NormalizeCode(ShareCode);

        if (string.IsNullOrWhiteSpace(ShareCode))
        {
            ForgetSharedList();
            return;
        }

        if (normalized is not null && normalized == _openCode)
        {
            return;
        }

        await OpenSharedListAsync(ShareCode);
    }

    /// <summary>Reads the snapshot behind a code. Says so in the alert when there is none.</summary>
    private async Task<bool> OpenSharedListAsync(string code)
    {
        ForgetFailures();
        ForgetSharedList();

        if (SharedList.NormalizeCode(code) is not { } normalized)
        {
            _failedCode = code.Trim();
            _sharedListFailure = SharedListFailure.NotFound;
            return false;
        }

        _openCode = normalized;
        SharedListFailure failure;

        try
        {
            var shared = await Client.GetSharedListAsync(normalized);

            if (_openCode != normalized)
            {
                return false;
            }

            if (shared is not null)
            {
                _sharedList = shared;
                _saveSharedName = shared.Name;
                return true;
            }

            failure = SharedListFailure.NotFound;
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the shared list {Code}", normalized);
            failure = SharedListFailure.Throttled;
        }
        catch (Exception ex)
        {
            // Uncaught, this takes the circuit and the host's page with it.
            Log?.LogError(ex, "could not read the shared list {Code}", normalized);
            failure = SharedListFailure.Failed;
        }

        // A code opened since this one owns the view; this answer must not clear or mark it.
        if (_openCode != normalized)
        {
            return false;
        }

        _failedCode = normalized;
        _sharedListFailure = failure;

        // Signed in, the reader's own lists come back under the sentence; signed out the code stays
        // so the tab it opened stays, with the sentence and a way to close it.
        if (IsAuthenticated)
        {
            _openCode = null;
            await AnnounceShareCodeAsync(null);
        }

        return false;
    }

    private void ForgetSharedList()
    {
        _openCode = null;
        _sharedList = null;
        _sharedPageNumber = 1;
        _savingShared = false;
        _saveSharedName = "";
        _saveNameProblem = SaveNameProblem.None;
    }

    private async Task AnnounceShareCodeAsync(string? code)
    {
        if (string.Equals(code, _hostShareCode, StringComparison.Ordinal))
        {
            return;
        }

        await ShareCodeChanged.InvokeAsync(code);
    }

    private void ToggleOpeningSharedFromControl(MouseEventArgs released) => Toggle(released, ref _openingShared);

    private async Task OpenSharedFromFieldAsync()
    {
        var typed = _sharedCodeInput.Trim();

        if (typed.Length == 0)
        {
            return;
        }

        if (await OpenSharedListAsync(typed))
        {
            _sharedCodeInput = "";
            await AnnounceShareCodeAsync(_openCode);
        }
    }

    private async Task CloseSharedListAsync()
    {
        ForgetFailures();
        ForgetSharedList();
        await AnnounceShareCodeAsync(null);
    }

    // Paged here rather than by the API: the snapshot arrives whole.
    private Task GoToSharedPageAsync(int page)
    {
        if (page >= 1 && page <= SharedTotalPages)
        {
            _sharedPageNumber = page;
        }

        return Task.CompletedTask;
    }

    // Refused on an empty list before any request: the API answers an empty snapshot with a 400,
    // and aria-disabled leaves the press reaching here.
    private async Task ToggleSharingFromControl(MouseEventArgs released)
    {
        if (ShownListIsEmpty || _shownList is null)
        {
            return;
        }

        Toggle(released, ref _sharingList);

        if (_sharingList && _shareMade?.ListId != _shownList)
        {
            await ShareShownListAsync();
        }
    }

    private async Task ShareShownListAsync()
    {
        if (_shownList is not { } list || ShownList is not { VariableCount: > 0 } shown || _shareInFlight)
        {
            return;
        }

        ForgetFailures();
        _shareInFlight = true;

        try
        {
            var items = await ReadWholeListAsync(list);

            if (items is not { Count: > 0 } || _shownList != list)
            {
                return;
            }

            var code = await Client.ShareListAsync(shown.Name, items);

            if (_shownList == list)
            {
                _shareMade = new ShareMade(list, code, SharedListHref?.Invoke(code));
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused sharing list {ListId}", list);
            _shareFailure = ListActionFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // The walk in front of the share reads my/lists, which is the host's token to fix.
            Log?.LogWarning(ex, "the API refused reading list {ListId} for sharing as unauthorised", list);
            _shareFailure = ListActionFailure.Failed;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not share list {ListId}", list);
            _shareFailure = ListActionFailure.Failed;
        }
        finally
        {
            _shareInFlight = false;
        }
    }

    private void ToggleSavingSharedFromControl(MouseEventArgs released) => Toggle(released, ref _savingShared);

    /// <summary>
    /// Makes the shared list the reader's own under the name in the field, refusing one they
    /// already use — compared trimmed and case-insensitively, with no suffix chosen for them.
    /// </summary>
    private async Task SaveSharedListAsync()
    {
        if (State is null || !IsAuthenticated || _sharedList is not { } shared)
        {
            return;
        }

        var name = _saveSharedName.Trim();

        ForgetFailures();
        _saveNameProblem = SaveNameProblem.None;

        VariableList? created;

        try
        {
            _saveNameProblem = await NameProblemAsync(name);

            if (_saveNameProblem != SaveNameProblem.None)
            {
                return;
            }

            created = await State.CreateAsync(name);

            if (created is null)
            {
                return;
            }

            foreach (var chunk in shared.Items.Select(i => i.VariableId).Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
            {
                if (!await State.AddVariablesAsync(created.Id, chunk))
                {
                    _saveSharedFailure = ListActionFailure.Failed;
                    return;
                }
            }

            await State.SetActiveListAsync(created.Id);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused saving a shared list");
            _saveSharedFailure = ListActionFailure.Throttled;
            return;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused saving a shared list as unauthorised");
            _saveSharedFailure = ListActionFailure.Failed;
            return;
        }
        catch (Exception ex)
        {
            // The name the reader typed stays out of the log.
            Log?.LogError(ex, "could not save a shared list");
            _saveSharedFailure = ListActionFailure.Failed;
            return;
        }

        await CountTheAddsAsync(created.Id);

        ForgetSharedList();
        await AnnounceShareCodeAsync(null);

        _shownList = created.Id;
        _shownListMoves++;
        _pageNumber = 1;
        ForgetListControls();
        await LoadPageAsync();
    }
}
