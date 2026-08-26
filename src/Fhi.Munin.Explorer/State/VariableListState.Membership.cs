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

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        if (_activeListId is null)
        {
            var target = _lists.FirstOrDefault();

            if (target is null)
            {
                target = await CreateAsync(nameForFirstList, cancellationToken).ConfigureAwait(false);

                if (target is null)
                {
                    return false;
                }
            }

            await SetActiveListAsync(target.Id, cancellationToken).ConfigureAwait(false);
        }

        var listId = _activeListId!.Value;

        if (_saved.Contains(variableId))
        {
            if (await RemoveVariablesAsync(listId, [variableId], cancellationToken).ConfigureAwait(false))
            {
                _saved.Remove(variableId);
            }
        }
        else if (await AddVariablesAsync(listId, [variableId], cancellationToken).ConfigureAwait(false))
        {
            _saved.Add(variableId);
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

        while (true)
        {
            var result = await _client
                .GetMyListVariablesAsync(_activeListId.Value, page, pageSize, cancellationToken)
                .ConfigureAwait(false);

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
