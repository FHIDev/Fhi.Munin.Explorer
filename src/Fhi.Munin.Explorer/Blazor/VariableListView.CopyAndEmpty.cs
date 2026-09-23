using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

// Copying the list on screen under a new name, and emptying it (Fhi.Metadata-ntpbd.2). Munin has
// neither endpoint, so both are composed from the paged read and the batch writes.
public sealed partial class VariableListView
{
    private bool _copying;
    private bool _copyInFlight;
    private string _copyName = "";
    private SaveNameProblem _copyNameProblem;
    private CopyFailure _copyFailure;

    private bool _confirmingEmpty;
    private bool _emptyInFlight;
    private ListActionFailure _emptyFailure;

    /// <summary>Why a copy did not finish. Incomplete is its own state: the new list exists.</summary>
    private enum CopyFailure
    {
        None = 0,
        Failed,
        Throttled,
        Incomplete
    }

    private string CopyToggleId => $"munin-explorer-copy-toggle-{_instance}";

    private string CopyListNameId => $"munin-explorer-copy-name-{_instance}";

    private string CopyNameProblemId => $"munin-explorer-copy-problem-{_instance}";

    private string CopyNoteId => $"munin-explorer-copy-note-{_instance}";

    private string EmptyButtonId => $"munin-explorer-empty-list-{_instance}";

    private string ConfirmEmptyId => $"munin-explorer-confirm-empty-{_instance}";

    private string? CopyNameMessage => NameProblemMessage(_copyNameProblem);

    /// <summary>The copy's field is described by the note, and by the refusal when there is one.</summary>
    private string CopyNameDescribedBy =>
        CopyNameMessage is null ? CopyNoteId : $"{CopyNoteId} {CopyNameProblemId}";

    private string? CopyMessage => _copyFailure switch
    {
        CopyFailure.Throttled => T.RateLimitError,
        CopyFailure.Failed => T.SaveError,
        CopyFailure.Incomplete => T.CopyIncomplete,
        _ => null
    };

    private string? EmptyingMessage => _emptyFailure switch
    {
        ListActionFailure.Throttled => T.RateLimitError,
        ListActionFailure.Failed => T.ListActionError,
        _ => null
    };

    private void ForgetCopyAndEmptyFailures()
    {
        _copyFailure = CopyFailure.None;
        _emptyFailure = ListActionFailure.None;
    }

    private void ForgetCopyAndEmptyControls()
    {
        _copying = false;
        _copyName = "";
        _copyNameProblem = SaveNameProblem.None;
        _confirmingEmpty = false;
    }

    // Refused on an empty list, as sharing is: aria-disabled leaves the press reaching here.
    private void ToggleCopyingFromControl(MouseEventArgs released)
    {
        if (ShownListIsEmpty || ShownList is not { } shown)
        {
            return;
        }

        Toggle(released, ref _copying);

        if (_copying)
        {
            _copyName = T.DefaultCopyName(shown.Name);
            _copyNameProblem = SaveNameProblem.None;
        }
    }

    private void ToggleConfirmingEmptyFromControl(MouseEventArgs released)
    {
        if (ShownListIsEmpty || _shownList is null)
        {
            return;
        }

        Toggle(released, ref _confirmingEmpty);
    }

    /// <summary>
    /// Makes a new list under the name in the field holding every variable of the one on screen,
    /// then shows it. The annotations stay behind: there is no bulk write for them.
    /// </summary>
    private async Task CopyListAsync()
    {
        if (State is null || _shownList is not { } source || ShownListIsEmpty || _copyInFlight)
        {
            return;
        }

        var name = _copyName.Trim();

        ForgetFailures();
        _copyInFlight = true;

        VariableList? created = null;

        try
        {
            _copyNameProblem = await NameProblemAsync(name);

            if (_copyNameProblem != SaveNameProblem.None)
            {
                return;
            }

            var ids = (await ReadWholeListAsync(source)).Select(i => i.VariableId).ToList();

            created = await State.CreateAsync(name);

            if (created is null)
            {
                return;
            }

            // Active before the writes, so the holder counts them against the copy and the save
            // buttons read its membership rather than the source's.
            await State.SetActiveListAsync(created.Id);

            foreach (var chunk in ids.Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
            {
                if (!await State.AddVariablesAsync(created.Id, chunk))
                {
                    _copyFailure = CopyFailure.Incomplete;
                    break;
                }
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused copying list {ListId}", source);
            _copyFailure = created is null ? CopyFailure.Throttled : CopyFailure.Incomplete;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused copying list {ListId} as unauthorised", source);
            _copyFailure = created is null ? CopyFailure.Failed : CopyFailure.Incomplete;
        }
        catch (Exception ex)
        {
            // Uncaught, this takes the circuit with it. The name the reader typed stays out of the log.
            Log?.LogError(ex, "could not copy list {ListId}", source);
            _copyFailure = created is null ? CopyFailure.Failed : CopyFailure.Incomplete;
        }
        finally
        {
            _copyInFlight = false;
        }

        if (created is null)
        {
            return;
        }

        // Nothing is rolled back: the copy is shown with whatever landed, and the alert says so.
        _shownList = created.Id;
        _pageNumber = 1;
        ForgetListControls();

        // Left open, now offering to copy the copy: the reader's focus is on the submit button.
        _copying = true;
        _copyName = T.DefaultCopyName(created.Name);

        await LoadPageAsync();
    }

    /// <summary>
    /// Takes every variable out of the list on screen, once confirmed. The list and its name stay.
    /// Unnarrowed by the ticked kilder: the question says every variable.
    /// </summary>
    private async Task EmptyListAsync()
    {
        if (State is null || _shownList is not { } list || ShownListIsEmpty || _emptyInFlight)
        {
            return;
        }

        _confirmingEmpty = false;
        ForgetFailures();
        _emptyInFlight = true;

        try
        {
            var ids = (await ReadWholeListAsync(list)).Select(i => i.VariableId).ToList();

            // Through the holder, which drops the removed ids from its membership, so the search
            // rows' save buttons redraw as unsaved.
            foreach (var chunk in ids.Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
            {
                if (!await State.RemoveVariablesAsync(list, chunk))
                {
                    _emptyFailure = ListActionFailure.Failed;
                    break;
                }
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused emptying list {ListId}", list);
            _emptyFailure = ListActionFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused emptying list {ListId} as unauthorised", list);
            _emptyFailure = ListActionFailure.Failed;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not empty list {ListId}", list);
            _emptyFailure = ListActionFailure.Failed;
        }
        finally
        {
            _emptyInFlight = false;
        }

        if (_shownList != list)
        {
            return;
        }

        _pageNumber = 1;
        await LoadPageAsync();
    }
}
