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

    /// <summary>The membership read walking pages right now, or <see langword="null"/> when none is.</summary>
    /// <remarks>
    /// The counterpart to <c>_loading</c> in <see cref="EnsureLoadedAsync"/>, and here for the same
    /// reason: several surfaces mount together and each one asks before any of them has finished. A
    /// task rather than a flag because the asker has to wait for the answer and not merely decline
    /// to ask again — a press that skipped a read still in flight would decide its direction from a
    /// set that is half filled.
    /// </remarks>
    private Task? _membershipRead;

    /// <summary>Which list <see cref="_membershipRead"/> is reading, so a switch is never joined.</summary>
    private Guid _membershipReadList;

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
    /// <para>
    /// This overload is the ask a render makes, and it never retries a read that has already been
    /// refused. <see cref="Blazor.VariableExplorer"/> calls it from <c>OnParametersSetAsync</c>,
    /// which runs on every parameter change and not only on the mount — so a retry here would send
    /// a fresh multi-page membership read alongside every search and every page turn, at the one
    /// moment the address is known to be over the per-address limit. The package deliberately does
    /// not retry on a shared <c>Retry-After</c>, and a repair path that did would rebuild the burst
    /// that caused the 429. The retry belongs to <see cref="ToggleSavedAsync"/>, which is a reader
    /// asking for something rather than a render happening.
    /// </para>
    /// </remarks>
    public Task EnsureActiveListAsync(CancellationToken cancellationToken = default) =>
        EnsureActiveListAsync(readerAsked: false, cancellationToken);

    /// <summary>Chooses which list saves go to, and reads what is already in it.</summary>
    public async Task SetActiveListAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        if (_activeListId == listId && _membershipLoaded)
        {
            return;
        }

        _activeListId = listId;
        _membershipLoaded = false;

        // Cleared here, where the switch is: leaving the previous list's members answering
        // IsSaved while the new list is being walked would draw rows against a list nobody chose.
        // The read itself does not clear — see the remarks on ReadMembershipAsync.
        _saved.Clear();
        await LoadMembershipAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Puts the variable in the active list, or takes it out if it is already there. Returns what
    /// the variable's state is afterwards, so a caller need not ask again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creates a list first when the reader has none — helsedata's 118497 is this same action for a
    /// reader with nothing saved yet, and refusing until they have made a list somewhere else would
    /// make the button lie about what it does.
    /// </para>
    /// <para>
    /// This is the reader-initiated ask, so it is where a membership read that was refused gets
    /// read again. Two things follow from the repair happening inside the press, and both are
    /// deliberate. The direction is decided from the set as the reader saw it, captured before
    /// anything is awaited: a repair arriving mid-press must not turn the save the button offered
    /// into a delete of a variable the reader was trying to keep. And a repair that is refused
    /// again does not take the write down with it — the write is what was asked for, the read was
    /// not.
    /// </para>
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

        // What the button said, read before the first await. Everything below acts on this and not
        // on _saved, because the membership read a line further down can fill the set in the middle
        // of the call: a variable already in the list, drawn as "save" because the mount's read was
        // refused, would otherwise be found saved here and removed — the press doing the exact
        // opposite of its label, which is the disagreement this whole path exists to prevent.
        var drawnAsSaved = _saved.Contains(variableId);

        try
        {
            await EnsureActiveListAsync(readerAsked: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (_activeListId is not null && e is not OperationCanceledException)
        {
            // The list is known, so the write can go out; only the membership read failed. Letting
            // that throw would cost the reader the save as well as the labels, and the limiter is
            // the likeliest reason it failed — the case where losing the press is least excusable,
            // since the reader is being told to wait and try again. The labels stay wrong until an
            // ask gets an answer.
            //
            // A cancellation is not swallowed: it means the component holding the token has gone
            // away, so there is nobody left who asked for the write either.
        }

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

        var accepted = drawnAsSaved
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
            // Both directions are safe against a set the repair filled: adding an id the list
            // already holds and removing one it does not are both no-ops on the API's side, and
            // both are no-ops here too.
            if (drawnAsSaved)
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

    private async Task EnsureActiveListAsync(bool readerAsked, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated || (_activeListId is not null && _membershipLoaded))
        {
            return;
        }

        if (_activeListId is not null)
        {
            // A list is already chosen and its membership is not here yet.
            if (InFlightMembershipRead() is { } inFlight)
            {
                // Someone else is already reading this same list. Wait for their answer rather than
                // sending a second walk of it — the mount has several surfaces asking at once, and
                // this is the burst the limiter counts.
                await inFlight.ConfigureAwait(false);

                return;
            }

            if (!readerAsked)
            {
                // Nothing is in flight, so an earlier read failed. Read it again only for a reader
                // who asked — see the remarks on the public overload for why a render must not.
                return;
            }

            // Read that list again rather than falling back to the first one, which is not
            // necessarily the one the reader — or another surface — chose.
            await LoadMembershipAsync(cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// The membership read still running, or <see langword="null"/> when there is none to join.
    /// </summary>
    /// <remarks>
    /// A finished read — one that filled the set and one that threw alike — is dropped here rather
    /// than in the read's own <c>finally</c>. An <c>async</c> method that throws before its first
    /// await has already run its <c>finally</c> by the time the faulted task is handed back and
    /// assigned, so clearing there would latch that task: every later ask would join a failure
    /// instead of reading again.
    /// </remarks>
    private Task? InFlightMembershipRead()
    {
        if (_membershipRead is { IsCompleted: false })
        {
            return _membershipRead;
        }

        _membershipRead = null;
        return null;
    }

    /// <summary>
    /// Reads every page of the active list, or joins the read already doing so.
    /// </summary>
    private Task LoadMembershipAsync(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated || _activeListId is null || _membershipLoaded)
        {
            return Task.CompletedTask;
        }

        var listId = _activeListId.Value;

        // Joined only when it is reading the list being asked about: a read left over from a list
        // the reader has since switched away from answers a question nobody is asking.
        if (InFlightMembershipRead() is { } inFlight && _membershipReadList == listId)
        {
            return inFlight;
        }

        _membershipReadList = listId;
        _membershipRead = ReadMembershipAsync(listId, cancellationToken);

        return _membershipRead;
    }

    /// <summary>
    /// Walks every page of one list, because the answer this feeds is "is this one variable in it",
    /// and a half-read list answers that wrongly for everything it did not reach.
    /// </summary>
    /// <remarks>
    /// The pages are collected into a set of this read's own and swapped into <see cref="_saved"/>
    /// only once the walk is complete. Writing into the shared set as the pages arrive would leave
    /// a read that threw partway through having published half a list, and would let two reads
    /// overlapping — ordinary, since every ask can start one — truncate each other: the loop stops
    /// on the count reaching <c>TotalCount</c>, so a set another read had emptied underneath would
    /// make this one walk past its real end and then latch a set with pages missing as loaded.
    /// </remarks>
    private async Task ReadMembershipAsync(Guid listId, CancellationToken cancellationToken)
    {
        const int pageSize = 1000; // The API's own ceiling — fewer round trips for a long list.
        var page = 1;
        var startedAt = _generation;
        var found = new HashSet<Guid>();

        while (true)
        {
            var result = await _client
                .GetMyListVariablesAsync(listId, page, pageSize, cancellationToken)
                .ConfigureAwait(false);

            // A sign-out between pages ends the read: the rest of this list is not this reader's.
            // So does a switch to another list, whose own read owns the set now.
            if (!StillCurrent(startedAt) || _activeListId != listId)
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
                found.Add(item.VariableId);
            }

            if (result.Items.Count == 0 || found.Count >= result.TotalCount)
            {
                break;
            }

            page++;
        }

        _saved.Clear();
        _saved.UnionWith(found);
        _membershipLoaded = true;
        Changed?.Invoke();
    }
}
