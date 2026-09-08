using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.State;

/// <summary>
/// The one place the signed-in user's variable lists live for the lifetime of a circuit. Every
/// surface that reads or writes them — the save action in the result list, the list view, the
/// download — resolves this same instance and is told when the others change it.
/// </summary>
/// <remarks>
/// <para>
/// Scoped, which in Blazor Server means one per circuit: two components on the same page share an
/// instance, two browser tabs do not. That is the boundary the data already has, since every
/// <c>my/lists</c> endpoint answers for the user the token names.
/// </para>
/// <para>
/// Whether the user is signed in is told to this service by the host, through the root component's
/// <c>IsAuthenticated</c> parameter. It is deliberately not discovered by calling the API and
/// reading 401 as "signed out": that spends a failed request on every render for every signed-out
/// reader, and it folds three different situations — no session, expired token, Munin down — into
/// one answer. <see cref="IsAuthenticated"/> defaults to <see langword="false"/>, so a host that
/// forgets the parameter gets no lists rather than unauthorised calls.
/// </para>
/// </remarks>
public sealed partial class VariableListState(
    IMuninExplorerClient client,
    ILogger<VariableListState>? logger = null)
{
    private readonly IMuninExplorerClient _client = client;

    // Defaulted for the tests, which new this up directly; AddMuninExplorer's AddLogging is what
    // makes it resolvable through the container. Guarded so a host's failing sink costs a log line
    // rather than the circuit — see ExplorerLog.
    private readonly ILogger? _logger = ExplorerLog.Guard(logger);

    private IReadOnlyList<VariableList> _lists = [];
    private bool _loaded;
    private bool _loading;
    private int _generation;

    /// <summary>Raised after any change, so every surface can re-render without refetching. What
    /// changed is the argument rather than a property beside it: these methods await with
    /// ConfigureAwait(false), so a shared one could be the next raise's before a handler read it.</summary>
    public event Action<ListChange?>? Changed;

    /// <summary>Which list a <see cref="Changed"/> named, and whether it could have altered its rows.</summary>
    /// <param name="ListId">The list, or <see langword="null"/> when the change names none in particular.</param>
    /// <param name="AffectsRows">False for a rename or a brand-new list — neither touches a row.</param>
    public readonly record struct ListChange(Guid? ListId, bool AffectsRows);

    /// <summary>Raises <see cref="Changed"/> with what it was raised for.</summary>
    private void RaiseChanged(Guid? listId, bool affectsRows) =>
        Changed?.Invoke(new ListChange(listId, affectsRows));

    /// <summary>
    /// Whether the host says the reader is signed in. False until the host says otherwise — see the
    /// remarks on the class for why this is told rather than discovered.
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>The lists as last read. Empty for a signed-out reader, always.</summary>
    public IReadOnlyList<VariableList> Lists => _lists;

    /// <summary>Set by the root component from its parameter. Signing out drops what was loaded.</summary>
    public void SetAuthenticated(bool isAuthenticated)
    {
        if (IsAuthenticated == isAuthenticated)
        {
            return;
        }

        IsAuthenticated = isAuthenticated;

        // Anything already in flight belongs to the reader who was here a moment ago. Bumping the
        // generation is what lets those calls recognise, when they come back, that their answer is
        // no longer anybody's to see.
        _generation++;

        // Leaving the previous user's list names on screen after a sign-out would be a disclosure,
        // not just a stale view.
        if (!isAuthenticated)
        {
            _lists = [];
            _saved.Clear();
            _activeListId = null;
            _membershipLoaded = false;
            ForgetKilder();

            // Dropped rather than awaited: a read still walking pages belongs to the reader who
            // has just left, and the generation guard already stops it writing anything. Keeping
            // the task would let the next reader join it and take its silence for an answer.
            _membershipRead = null;
        }

        _loaded = false;
        RaiseChanged(listId: null, affectsRows: true);
    }

    /// <summary>
    /// True when the answer to a call started at <paramref name="startedAt"/> may still be used.
    /// </summary>
    /// <remarks>
    /// Every await here is a point at which the reader can sign out. Without this check the
    /// continuation would write the previous reader's lists back over the empty ones the sign-out
    /// just installed — the disclosure the sign-out exists to prevent, arriving a few milliseconds
    /// late.
    /// </remarks>
    private bool StillCurrent(int startedAt) => IsAuthenticated && _generation == startedAt;

    /// <summary>
    /// Reads the lists once per circuit. Signed out, this returns without calling anything — the
    /// guard is here rather than at each call site so a new surface cannot forget it.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        // _loading as well as _loaded: three surfaces mounting together all reach this before any of
        // them has finished, and without it each would send its own request for the same lists.
        if (!IsAuthenticated || _loaded || _loading)
        {
            return;
        }

        var startedAt = _generation;
        _loading = true;

        try
        {
            var lists = await _client.GetMyListsAsync(cancellationToken).ConfigureAwait(false);

            if (!StillCurrent(startedAt))
            {
                return;
            }

            _lists = lists;
            _loaded = true;
        }
        finally
        {
            _loading = false;
        }

        RaiseChanged(listId: null, affectsRows: true);
    }

    /// <summary>Forces the next <see cref="EnsureLoadedAsync"/> to read again.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _loaded = false;
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a list and returns it, or <see langword="null"/> when signed out.</summary>
    public async Task<VariableList?> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return null;
        }

        var startedAt = _generation;
        var created = await _client.CreateMyListAsync(name, cancellationToken).ConfigureAwait(false);

        // The list was made — it is the reader's, on the server. It is only this circuit's copy that
        // must not keep it, because the reader who owns it is no longer the one at the screen.
        if (!StillCurrent(startedAt))
        {
            return null;
        }

        _lists = [.. _lists, created];

        // A list this fresh has never held a row for anybody to have shown - the identity below is
        // what lets a subscriber skip the read on it, not this flag, but it is honestly false too.
        RaiseChanged(created.Id, affectsRows: false);
        return created;
    }

    /// <summary>Renames a list. Returns whether the API accepted it.</summary>
    public async Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        var startedAt = _generation;

        if (!await _client.RenameMyListAsync(id, name, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!StillCurrent(startedAt))
        {
            return false;
        }

        // Patched in place rather than refetched: the other surfaces are told below, and a round
        // trip here would make a rename look slower than it is. The timestamp goes with the name,
        // for the reason TouchedNow gives.
        _lists = [.. _lists.Select(l => l.Id == id ? l with { Name = name, UpdatedAt = TouchedNow() } : l)];

        // A rename touches the list's own name and stamp, never a row in it.
        RaiseChanged(id, affectsRows: false);
        return true;
    }

    /// <summary>Deletes a list. Returns whether the API accepted it.</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        var startedAt = _generation;

        if (!await _client.DeleteMyListAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!StillCurrent(startedAt))
        {
            return false;
        }

        _lists = [.. _lists.Where(l => l.Id != id)];

        // Left pointing at the list just deleted, the next ask would read the membership of a list
        // the API no longer has, and the save button would offer to remove variables from it.
        // Cleared rather than repointed: which list becomes active is the surface's call.
        if (_activeListId == id)
        {
            _activeListId = null;
            _membershipLoaded = false;
            _saved.Clear();
            ForgetKilder();
        }

        // A subscriber still showing this id has to notice it is gone, not merely that it changed.
        RaiseChanged(id, affectsRows: true);
        return true;
    }

    /// <summary>Puts variables in a list. Returns whether the API accepted it.</summary>
    public async Task<bool> AddVariablesAsync(
        Guid id,
        IReadOnlyCollection<Guid> variableIds,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        var startedAt = _generation;

        // An empty collection is passed through, not short-circuited: the client documents it as a
        // legitimate call whose answer says whether the list exists.
        var accepted = await _client
            .AddVariablesToMyListAsync(id, variableIds, cancellationToken)
            .ConfigureAwait(false);

        if (accepted)
        {
            var delta = RecordMembership(id, variableIds, saved: true, startedAt);

            RecordCountChange(id, delta, startedAt);
            RaiseChanged(id, affectsRows: true);
        }

        return accepted;
    }

    /// <summary>Takes variables out of a list. Returns whether the API accepted it.</summary>
    public async Task<bool> RemoveVariablesAsync(
        Guid id,
        IReadOnlyCollection<Guid> variableIds,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        var startedAt = _generation;

        var accepted = await _client
            .RemoveVariablesFromMyListAsync(id, variableIds, cancellationToken)
            .ConfigureAwait(false);

        if (accepted)
        {
            var delta = RecordMembership(id, variableIds, saved: false, startedAt);

            RecordCountChange(id, delta, startedAt);
            RaiseChanged(id, affectsRows: true);
        }

        return accepted;
    }

    /// <summary>
    /// Moves one list's count — never its stamp, which Munin moves on a rename only
    /// (Fhi.Metadata-l9l2n.45). <c>countDelta</c> is what <see cref="RecordMembership"/> measured,
    /// never the size of the batch.
    /// </summary>
    private void RecordCountChange(Guid id, int countDelta, int startedAt)
    {
        if (!StillCurrent(startedAt))
        {
            return;
        }

        // A floor, because the count and the membership walk are two reads with a write's worth of
        // time between them, and "-1 variabler" is a sentence no reader should be shown.
        _lists =
        [
            .. _lists.Select(l => l.Id == id
                ? l with { VariableCount = Math.Max(0, l.VariableCount + countDelta) }
                : l)
        ];
    }

    /// <summary>
    /// What an accepted rename sets <c>updatedAt</c> to. This clock because the endpoint does not
    /// answer with the new value, and the alternative is not a truer one but a stale one.
    /// </summary>
    private static DateTimeOffset TouchedNow() => DateTimeOffset.UtcNow;
}
