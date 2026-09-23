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
    private string _incompleteCopyName = "";

    private bool _confirmingEmpty;
    private bool _emptyInFlight;
    private ListActionFailure _emptyFailure;

    /// <summary>Why a copy did not finish. Incomplete is its own state: the new list exists.</summary>
    private enum CopyFailure
    {
        None = 0,
        Failed,
        Throttled,
        Incomplete,
        IncompleteElsewhere
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
        CopyFailure.IncompleteElsewhere => T.CopyIncompleteElsewhere(_incompleteCopyName),
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
    /// Copies every variable on screen into a new list, then shows it. No annotations: no bulk write.
    /// </summary>
    private async Task CopyListAsync()
    {
        if (State is null || _shownList is not { } source || ShownListIsEmpty || _copyInFlight)
        {
            return;
        }

        var name = _copyName.Trim();
        var movesAtStart = _shownListMoves;

        ForgetFailures();
        _copyNameProblem = SaveNameProblem.None;
        _copyInFlight = true;

        VariableList? created = null;

        try
        {
            _copyNameProblem = await NameProblemAsync(name);

            if (_copyNameProblem != SaveNameProblem.None)
            {
                return;
            }

            // Gone in another tab: no copy at all beats one that silently holds nothing.
            if (await ReadWholeListAsync(source) is not { } items)
            {
                _copyFailure = CopyFailure.Failed;
                return;
            }

            created = await State.CreateAsync(name);

            if (created is null)
            {
                return;
            }

            // The source stays active through the writes, so the save buttons never write to a copy
            // that is not on screen; the sibling save of a shared list does the same.
            foreach (var chunk in items.Select(i => i.VariableId).Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
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

        // A reader who chose another list meanwhile stays on it, even one who came back to the source.
        if (_shownListMoves != movesAtStart)
        {
            if (_copyFailure == CopyFailure.Incomplete)
            {
                _copyFailure = CopyFailure.IncompleteElsewhere;
                _incompleteCopyName = created.Name;
            }

            await CountTheCopyAsync(created.Id);
            return;
        }

        // Nothing is rolled back: the copy is shown with whatever landed, and the alert says so.
        _shownList = created.Id;
        _shownListMoves++;
        _pageNumber = 1;
        ForgetListControls();

        // Left open, now offering to copy the copy: the reader's focus is on the submit button.
        _copying = true;
        _copyName = T.DefaultCopyName(created.Name);

        try
        {
            await State.SetActiveListAsync(created.Id);
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not switch to the copy {ListId}", created.Id);
        }

        await CountTheCopyAsync(created.Id);
        await LoadPageAsync();
    }

    // The adds were made while the copy was not active, so the holder counted none of them.
    private async Task CountTheCopyAsync(Guid copy)
    {
        try
        {
            await State!.RefreshAsync();
        }
        catch (Exception ex)
        {
            Log?.LogWarning(ex, "could not read the lists again after copying into {ListId}", copy);
        }
    }

    /// <summary>
    /// Empties the list on screen once confirmed, unnarrowed by the ticked kilder. Its name stays.
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
            if (await ReadWholeListAsync(list) is not { } items)
            {
                _emptyFailure = ListActionFailure.Failed;
                return;
            }

            // Through the holder, which drops the removed ids from its membership, so the search
            // rows' save buttons redraw as unsaved.
            foreach (var chunk in items.Select(i => i.VariableId).Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
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
