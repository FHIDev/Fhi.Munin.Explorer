namespace Fhi.Munin.Explorer.State;

/// <summary>
/// Which variables are in the active list, held for the circuit rather than by the row that draws
/// the button.
/// </summary>
/// <remarks>
/// The result rows are redrawn whenever the facet counts change, so a row that remembered "saved"
/// itself would forget it on the next refiltering and show the wrong state for a variable that is
/// in the list. Membership therefore lives here, beside the lists themselves, and a row only asks.
/// </remarks>
public sealed partial class VariableListState
{
    private readonly HashSet<Guid> _saved = [];
    private Guid? _activeListId;
    private bool _membershipLoaded;

    /// <summary>The list a save goes to. The reader's first list until something else picks one.</summary>
    public Guid? ActiveListId => _activeListId;

    /// <summary>
    /// Whether the variable is in the active list. A plain read with no request behind it, because
    /// every row on screen calls it on every render.
    /// </summary>
    public bool IsSaved(Guid variableId) => _saved.Contains(variableId);

    /// <summary>
    /// Picks the reader's first list as the active one and reads what is in it, so the rows know
    /// which variables are already saved before anybody presses anything.
    /// </summary>
    /// <remarks>
    /// Without this the set is empty until the first save, and a variable the reader saved
    /// yesterday renders as "not saved" — so the button offers to save it and the press takes it
    /// out instead. The label, the action and the state have to agree on the very first render.
    /// A reader with no list yet gets none made here: that happens on the first save, where it is
    /// something they asked for.
    /// </remarks>
    public async Task EnsureActiveListAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated || _activeListId is not null)
        {
            return;
        }

        var startedAt = _generation;
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (!StillCurrent(startedAt))
        {
            return;
        }

        if (_lists.FirstOrDefault() is { } first)
        {
            await SetActiveListAsync(first.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Chooses which list saves go to, and reads what is already in it.</summary>
    public async Task SetActiveListAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        if (_activeListId == listId && _membershipLoaded)
        {
            return;
        }

        _activeListId = listId;
        _membershipLoaded = false;
        _saved.Clear();
        await LoadMembershipAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Puts the variable in the active list, or takes it out if it is already there. Returns what
    /// the variable's state is afterwards, so a caller need not ask again.
    /// </summary>
    /// <remarks>
    /// Creates a list first when the reader has none — helsedata's 118497 is this same action for a
    /// reader with nothing saved yet, and refusing until they have made a list somewhere else would
    /// make the button lie about what it does.
    /// </remarks>
    public async Task<bool> ToggleSavedAsync(
        Guid variableId,
        string nameForFirstList,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        // Same guard the list methods use: every await below is a point at which the reader can
        // sign out, and a continuation that wrote to _saved afterwards would put the previous
        // reader's variable back into the set the sign-out just cleared.
        var startedAt = _generation;

        await EnsureActiveListAsync(cancellationToken).ConfigureAwait(false);

        if (!StillCurrent(startedAt))
        {
            return false;
        }

        if (_activeListId is null)
        {
            // No list to pick, so this save makes one — the reader asked for somewhere to put it.
            var target = await CreateAsync(nameForFirstList, cancellationToken).ConfigureAwait(false);

            if (target is null)
            {
                return false;
            }

            await SetActiveListAsync(target.Id, cancellationToken).ConfigureAwait(false);

            if (!StillCurrent(startedAt))
            {
                return false;
            }
        }

        var listId = _activeListId!.Value;
        var wasSaved = _saved.Contains(variableId);

        var accepted = wasSaved
            ? await RemoveVariablesAsync(listId, [variableId], cancellationToken).ConfigureAwait(false)
            : await AddVariablesAsync(listId, [variableId], cancellationToken).ConfigureAwait(false);

        // The call may well have succeeded on the server — it went out under the old token. It is
        // this circuit's copy that must not keep the answer, because the reader it belongs to is no
        // longer the one at the screen.
        if (!StillCurrent(startedAt))
        {
            return false;
        }

        if (accepted)
        {
            if (wasSaved)
            {
                _saved.Remove(variableId);
            }
            else
            {
                _saved.Add(variableId);
            }
        }

        Changed?.Invoke();
        return _saved.Contains(variableId);
    }

    /// <summary>
    /// Reads every page of the active list, because the answer this feeds is "is this one variable
    /// in it", and a half-read list answers that wrongly for everything it did not reach.
    /// </summary>
    private async Task LoadMembershipAsync(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated || _activeListId is null || _membershipLoaded)
        {
            return;
        }

        const int pageSize = 1000; // The API's own ceiling — fewer round trips for a long list.
        var page = 1;
        var startedAt = _generation;
        var listId = _activeListId.Value;

        while (true)
        {
            var result = await _client
                .GetMyListVariablesAsync(listId, page, pageSize, cancellationToken)
                .ConfigureAwait(false);

            // A sign-out between pages ends the read: the rest of this list is not this reader's.
            if (!StillCurrent(startedAt))
            {
                return;
            }

            // Null is "no such list of yours" — the list went away in another tab. Stop rather than
            // loop, and leave membership empty rather than half-read.
            if (result is null)
            {
                break;
            }

            foreach (var item in result.Items)
            {
                _saved.Add(item.VariableId);
            }

            if (result.Items.Count == 0 || _saved.Count >= result.TotalCount)
            {
                break;
            }

            page++;
        }

        _membershipLoaded = true;
        Changed?.Invoke();
    }
}
