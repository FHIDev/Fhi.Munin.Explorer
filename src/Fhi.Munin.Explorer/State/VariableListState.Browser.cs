using System.Text.Json.Serialization;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.State;

/// <summary>
/// A signed-out reader's one list, kept in their browser rather than in Munin (Fhi.Metadata-emsc0.3.12). Offered
/// only when the host turns <see cref="MuninExplorerFeatures.CodePicker"/> on.
/// </summary>
/// <remarks>
/// The browser owns the list and changes it one row at a time; this holds only the rows' keys, read in chunks, since
/// a JS answer on Blazor Server is capped at 32 KB. Every call waits its turn, so they reach the browser in the
/// order they were made.
/// </remarks>
public sealed partial class VariableListState : IDisposable
{
    /// <summary>The anonymous export's ceiling, which the browser list therefore cannot pass.</summary>
    internal const int MaxBrowserListRows = MuninExplorerClient.MaxExportItems;

    // Each chunk well under the 32 KB a JS answer may carry: a key is about 75 bytes, a row about 550.
    private const int KeysPerCall = 100;
    internal const int RowsPerCall = 20;

    private IBrowserListStore? _browserStore;

    // The last call's turn; the next waits on it. A chain rather than a semaphore, which would need disposing mid-call.
    private Task _browserTail = Task.CompletedTask;

    // Swapped whole, never changed in place: a render may be enumerating the one it holds.
    private IReadOnlySet<VariableDatasamlingKey> _browserKeys = new HashSet<VariableDatasamlingKey>();
    private Task? _browserRead;

    // The lists carry-overs are filling, kept out of Lists until each add succeeds: a save into one would go with a discard.
    // Swapped whole, as two mounted views can press at once.
    private IReadOnlySet<Guid> _carryingInto = new HashSet<Guid>();

    // Lists this circuit knows Munin holds, made or left here: a lists read begun before one cannot speak for it.
    private IReadOnlyList<VariableList> _madeHere = [];

    // Lists deleted here, which a lists read begun before the delete still holds. Swapped whole, as _madeHere is.
    private IReadOnlySet<Guid> _deletedHere = new HashSet<Guid>();

    // Lists renamed here, as they stand after the rename, which a lists read begun before it answers under the old name.
    private IReadOnlyList<VariableList> _renamedHere = [];

    // The first read went unanswered, by a dropped circuit, a timeout or a fault in the module, and the host has not
    // refused the module for good: the next render asks again.
    private bool _browserReadAgain;

    /// <summary>What a press on the save button did to the browser list.</summary>
    internal enum BrowserListOutcome
    {
        Added,
        Removed,

        /// <summary>Refused: the list already holds <see cref="MaxBrowserListRows"/> rows.</summary>
        Full,

        /// <summary>The browser could not be asked: no module, or a fault in it.</summary>
        Unavailable
    }

    /// <summary>Whether the host offers the browser list, which a component says once it has a JS runtime.</summary>
    internal bool OffersBrowserList => _browserStore is not null;

    /// <summary>Whether saves go to the browser list rather than to Munin: offered, and the reader is signed out.</summary>
    internal bool UsesBrowserList => !IsAuthenticated && OffersBrowserList;

    /// <summary>How many rows the browser list holds, as the browser last said. Kept across a sign-in for the carry-over.</summary>
    internal int BrowserCount { get; private set; }

    /// <summary>Whether the browser has answered once, so an empty list is an answer.</summary>
    internal bool BrowserListRead { get; private set; }

    /// <summary>False when the browser could not be asked; the list then does not exist until a later read answers.</summary>
    internal bool BrowserListAvailable { get; private set; } = true;

    /// <summary>False while the browser holds the list for this page only: storage refused, or the key is not ours.</summary>
    internal bool BrowserListKept { get; private set; } = true;

    /// <summary>The key holds something this version cannot read, which is left alone; the list lives for this page.</summary>
    internal bool BrowserListForeign { get; private set; }

    /// <summary>
    /// True once a carry-over saved the rows to Munin but could not take them out of the browser: offering it again
    /// would save them twice.
    /// </summary>
    internal bool BrowserCarryOverStuck { get; private set; }

    /// <summary>A sign-in or sign-out is a new reader at the screen, who is offered the carry-over afresh.</summary>
    private void ForgetCarryOverStuck() => BrowserCarryOverStuck = false;

    /// <summary>Moves on every change to the browser list, so a view can tell its page is stale.</summary>
    internal int BrowserVersion { get; private set; }

    /// <summary>Turns the browser list on for this circuit. The first store wins: every component reaches the same browser.</summary>
    internal void OfferBrowserList(IBrowserListStore store) => _browserStore ??= store;

    /// <summary>Releases the browser list's hold on the reader's browser when the circuit ends.</summary>
    /// <remarks>The container calls it; a host never needs to.</remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _browserStore, null) is IAsyncDisposable store)
        {
            _ = store.DisposeAsync().AsTask();
        }
    }

    /// <summary>Runs <paramref name="call"/> after every call made before it has finished.</summary>
    private async Task<T> InTurnAsync<T>(Func<Task<T>> call)
    {
        var mine = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var previous = Interlocked.Exchange(ref _browserTail, mine.Task);

        try
        {
            await previous.ConfigureAwait(false);
            return await call().ConfigureAwait(false);
        }
        finally
        {
            mine.SetResult();
        }
    }

    /// <summary>
    /// Reads the list's keys once per circuit, again on the next call after a first read went unanswered without the
    /// host refusing the module, or joins the read in flight; listens for other tabs. Call from <c>OnAfterRenderAsync</c>:
    /// there is no browser during prerender. Never throws.
    /// </summary>
    internal Task ReadBrowserListAsync()
    {
        if (_browserStore is not { } store)
        {
            return Task.CompletedTask;
        }

        if (_browserReadAgain)
        {
            _browserReadAgain = false;
            _browserRead = null;
        }

        return _browserRead ??= FirstReadAsync(store);
    }

    private async Task FirstReadAsync(IBrowserListStore store)
    {
        try
        {
            await store.WatchAsync(SyncBrowserListAsync).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Without it another tab's change shows at the next read rather than at once; the list still works.
            _logger?.LogWarning(ex, "could not listen for the browser list changing in another tab");
        }

        await SyncBrowserListAsync().ConfigureAwait(false);
        _browserReadAgain = !BrowserListAvailable && !store.Refused;
    }

    /// <summary>Reads the keys again, as after another tab changed them. Never throws.</summary>
    internal async Task SyncBrowserListAsync()
    {
        if (_browserStore is not { } store)
        {
            return;
        }

        var wasAvailable = true;

        await InTurnAsync(async () =>
        {
            wasAvailable = BrowserListAvailable;

            try
            {
                await RereadKeysAsync(store, await store.CountAsync().ConfigureAwait(false)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "could not read the browser's variable list");
                BrowserListAvailable = false;
            }

            return true;
        }).ConfigureAwait(false);

        // A read failing again changed nothing; a render it set off would read again, and fail again, without end.
        if (wasAvailable || BrowserListAvailable)
        {
            BrowserChanged();
        }
    }

    // In its turn. A null anywhere means the browser could not be asked, and nothing is published.
    private async Task RereadKeysAsync(IBrowserListStore store, BrowserListStatus? status)
    {
        if (status is null)
        {
            BrowserListAvailable = false;
            return;
        }

        var keys = new HashSet<VariableDatasamlingKey>();

        for (var offset = 0; offset < status.Count; offset += KeysPerCall)
        {
            if (await store.KeysAsync(offset, KeysPerCall).ConfigureAwait(false) is not { } chunk)
            {
                BrowserListAvailable = false;
                return;
            }

            if (chunk.Count == 0)
            {
                break;
            }

            keys.UnionWith(chunk);
        }

        Publish(status, keys);
        BrowserListRead = true;
    }

    private void Publish(BrowserListStatus status, IReadOnlySet<VariableDatasamlingKey> keys)
    {
        _browserKeys = keys;
        BrowserCount = status.Count;

        // Nothing left to save twice.
        if (status.Count == 0)
        {
            BrowserCarryOverStuck = false;
        }

        BrowserListKept = status.Kept;
        BrowserListForeign = status.Foreign;
        BrowserListAvailable = true;
    }

    private void BrowserChanged()
    {
        BrowserVersion++;

        // No signed-in list's rows moved, so no view reloads one; the browser list's view follows BrowserVersion.
        RaiseChanged(listId: null, affectsRows: false);
    }

    /// <summary>Puts the row in the browser list, or takes it out if <paramref name="drawnAsSaved"/>.</summary>
    /// <param name="item">The row with the catalogue fields it is shown with; nothing else is stored.</param>
    /// <param name="drawnAsSaved">What the button said, read before any await, for the reason <c>ToggleAsync</c> gives.</param>
    internal async Task<BrowserListOutcome> ToggleInBrowserListAsync(VariableListItem item, bool drawnAsSaved)
    {
        if (_browserStore is not { } store)
        {
            return BrowserListOutcome.Unavailable;
        }

        await ReadBrowserListAsync().ConfigureAwait(false);
        var outcome = await InTurnAsync(() => ToggleInTurnAsync(store, item, drawnAsSaved)).ConfigureAwait(false);

        BrowserChanged();
        return outcome;
    }

    private async Task<BrowserListOutcome> ToggleInTurnAsync(IBrowserListStore store, VariableListItem item, bool drawnAsSaved)
    {
        try
        {
            var key = VariableDatasamlingKey.Of(item);
            var status = drawnAsSaved
                ? await store.RemoveAsync(key).ConfigureAwait(false)
                : await store.AddAsync(StoredFields(item), MaxBrowserListRows).ConfigureAwait(false);

            if (status is null)
            {
                BrowserListAvailable = false;
                return BrowserListOutcome.Unavailable;
            }

            var keys = new HashSet<VariableDatasamlingKey>(_browserKeys);
            var outcome = drawnAsSaved ? BrowserListOutcome.Removed : status.Full ? BrowserListOutcome.Full : BrowserListOutcome.Added;

            if (outcome == BrowserListOutcome.Removed)
            {
                keys.Remove(key);
            }
            else if (outcome == BrowserListOutcome.Added)
            {
                keys.Add(key);
            }

            // Another tab's change shows as a count the keys do not add up to: read them again rather than guess.
            if (keys.Count != status.Count)
            {
                await RereadKeysAsync(store, status).ConfigureAwait(false);
            }
            else
            {
                Publish(status, keys);
            }

            return outcome;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "could not change the browser's variable list");
            BrowserListAvailable = false;
            return BrowserListOutcome.Unavailable;
        }
    }

    /// <summary>Empties the browser list and forgets its key. False when the browser could not be asked. Never throws.</summary>
    internal async Task<bool> ClearBrowserListAsync()
    {
        if (_browserStore is not { } store)
        {
            return false;
        }

        var cleared = await InTurnAsync(async () =>
        {
            try
            {
                if (await store.ClearAsync().ConfigureAwait(false) is { } status)
                {
                    Publish(status, new HashSet<VariableDatasamlingKey>());
                    return true;
                }

                BrowserListAvailable = false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "could not empty the browser's variable list");
                BrowserListAvailable = false;
            }

            return false;
        }).ConfigureAwait(false);

        BrowserChanged();
        return cleared;
    }

    /// <summary>Every row's key, in the list's order, read from the browser now; null when it could not be asked.</summary>
    internal Task<IReadOnlyList<VariableDatasamlingKey>?> ReadBrowserKeysAsync() =>
        ReadInChunksAsync(KeysPerCall, (store, offset, count) => store.KeysAsync(offset, count), int.MaxValue);

    /// <summary>Rows from <paramref name="offset"/>, at most <paramref name="count"/>, with their catalogue fields.</summary>
    internal Task<IReadOnlyList<BrowserRow>?> ReadBrowserRowsAsync(int offset, int count) =>
        ReadInChunksAsync(RowsPerCall, (store, at, size) => store.PageAsync(at, size), count, offset);

    private async Task<IReadOnlyList<T>?> ReadInChunksAsync<T>(
        int chunk,
        Func<IBrowserListStore, int, int, Task<IReadOnlyList<T>?>> read,
        int count,
        int offset = 0)
    {
        if (_browserStore is not { } store)
        {
            return null;
        }

        return await InTurnAsync<IReadOnlyList<T>?>(async () =>
        {
            try
            {
                var all = new List<T>();

                while (all.Count < count)
                {
                    if (await read(store, offset + all.Count, Math.Min(chunk, count - all.Count)).ConfigureAwait(false) is not { } part)
                    {
                        return null;
                    }

                    if (part.Count == 0)
                    {
                        break;
                    }

                    all.AddRange(part);
                }

                return all;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "could not read rows of the browser's variable list");
                return null;
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// What a carry-over did: the list the rows went to and whether they also left the browser; when Munin refused the
    /// variables in <see cref="Refused"/>, no list and <see cref="RefusedRows"/> browser rows of them; or, with
    /// <see cref="AlreadyCarrying"/>, nothing touched, since another tab or view is carrying the rows.
    /// </summary>
    internal sealed record CarryOver(
        VariableList? List, bool LeftTheBrowser, IReadOnlySet<Guid>? Refused = null, int RefusedRows = 0, bool AlreadyCarrying = false);

    // The create and the add may each take the client's whole per-call limit; a third covers the reads and the removal.
    internal static readonly TimeSpan CarryClaimLifetime = RequestTimeoutHandler.Default * 3;

    // Two tabs writing a claim in the same moment: each reads back after this, and only the one whose claim stayed goes on.
    internal static readonly TimeSpan CarryClaimSettle = TimeSpan.FromMilliseconds(50);

    /// <summary>How many rows of the browser list belong to these variables, as this page last read it.</summary>
    internal int BrowserRowsOf(IReadOnlySet<Guid> variables) => _browserKeys.Count(k => variables.Contains(k.VariableId));

    /// <summary>How many of these variables still have a row in the browser list, as this page last read it.</summary>
    internal int BrowserVariablesOf(IReadOnlySet<Guid> variables) =>
        _browserKeys.Select(k => k.VariableId).Where(variables.Contains).Distinct().Count();

    /// <returns>A <see cref="CarryOver"/>; null when signed out, empty, unread, the list gone, or the reader changed before
    /// the add answered (a list made by then stays in Munin). Any other failure throws; when nothing is saved, the list
    /// is deleted.</returns>
    internal async Task<CarryOver?> CarryBrowserListOverAsync(string name, CancellationToken cancellationToken = default)
    {
        // Every await is a point at which the reader can sign out; the rows belong to the reader who pressed.
        var reader = Reader;

        if (!IsAuthenticated || _browserStore is not { } store)
        {
            return null;
        }

        BrowserCarryClaim? claim;

        try
        {
            // Unreachable, the module answers null here, and the read below says so.
            claim = await store.ClaimCarryOverAsync(CarryClaimLifetime, CarryClaimSettle).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "could not claim the browser's variable list for a carry-over");
            return null;
        }

        if (claim is { Taken: false })
        {
            return new CarryOver(null, LeftTheBrowser: false, AlreadyCarrying: true);
        }

        try
        {
            return Reader == reader ? await CarryClaimedAsync(name, reader, cancellationToken).ConfigureAwait(false) : null;
        }
        finally
        {
            if (claim?.Token is { } token)
            {
                await ReleaseCarryClaimAsync(store, token).ConfigureAwait(false);
            }
        }
    }

    // Never throws: the claim lapses by itself, and the press's own outcome is what the reader is told.
    private async Task ReleaseCarryClaimAsync(IBrowserListStore store, string token)
    {
        try
        {
            await store.ReleaseCarryOverAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "could not release the browser list's carry-over claim");
        }
    }

    private async Task<CarryOver?> CarryClaimedAsync(string name, int reader, CancellationToken cancellationToken)
    {
        var read = await ReadBrowserKeysAsync().ConfigureAwait(false);

        if (Reader != reader || read is null)
        {
            return null;
        }

        var keys = read.Distinct().ToList();

        if (keys.Count == 0)
        {
            return null;
        }

        var target = await _client.CreateMyListAsync(name, cancellationToken).ConfigureAwait(false);

        // Made for a reader who has left: Munin's list is theirs, and nothing here touches it again.
        if (!StillCurrent(reader))
        {
            return null;
        }

        _carryingInto = new HashSet<Guid>(_carryingInto) { target.Id };
        _lists = [.. _lists.Where(l => l.Id != target.Id)];

        try
        {
            return await FillCarriedListAsync(target, keys, reader, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            StopCarryingInto(target.Id);
        }
    }

    private void StopCarryingInto(Guid list) => _carryingInto = _carryingInto.Where(id => id != list).ToHashSet();

    private async Task<CarryOver?> FillCarriedListAsync(
        VariableList target, List<VariableDatasamlingKey> keys, int reader, CancellationToken cancellationToken)
    {
        bool accepted;

        try
        {
            // One request, which the browser list's ceiling keeps within a batch: Munin saves every row or none.
            accepted = await _client.AddItemsToMyListAsync(target.Id, keys, cancellationToken).ConfigureAwait(false);
        }
        catch (ListItemsRefusedException ex) when (Reader == reader && ex.VariableIds is { Count: > 0 } named)
        {
            _logger?.LogWarning(ex, "the API refused the browser list's rows of {Count} variables", named.Count);
            await DiscardAsync(target, reader).ConfigureAwait(false);
            var refused = keys.Select(k => k.VariableId).Where(named.Contains).ToHashSet();

            // Naming none of these rows, it says no more than a refusal that names nothing.
            if (refused.Count == 0)
            {
                throw;
            }

            return new CarryOver(null, LeftTheBrowser: false, refused, keys.Count(k => refused.Contains(k.VariableId)));
        }
        catch (Exception ex) when (Reader == reader)
        {
            _logger?.LogWarning(ex, "a carry-over to list {ListId} saved nothing", target.Id);
            await DiscardAsync(target, reader).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            // Another reader's lists are not this press's to tidy, nor are the rows in the browser.
            _logger?.LogWarning(ex, "a carry-over stopped because the reader changed");
            return null;
        }

        if (Reader != reader)
        {
            return null;
        }

        if (!accepted)
        {
            // The list was gone before the rows reached it.
            await DiscardAsync(target, reader).ConfigureAwait(false);
            return null;
        }

        StopCarryingInto(target.Id);
        _lists = [.. _lists, target];
        _madeHere = [.. _madeHere, target];
        RaiseChanged(target.Id, affectsRows: true);

        var left = await RemoveBrowserKeysAsync(keys).ConfigureAwait(false);

        // Rows left only where no other page reads them, or none left at all, cannot be saved twice.
        if (Reader == reader)
        {
            BrowserCarryOverStuck = !left && BrowserCount > 0;
        }

        return new CarryOver(target, left);
    }

    /// <summary>
    /// Takes every browser row of these variables out of the browser list, the rows Munin refused. False when any is
    /// still there. Never throws.
    /// </summary>
    internal async Task<bool> RemoveRefusedFromBrowserAsync(IReadOnlySet<Guid> variables)
    {
        var refused = _browserKeys.Where(k => variables.Contains(k.VariableId)).ToList();

        // Still in the stored value, the rows reach another page, which would offer them again.
        var left = refused.Count == 0 || await RemoveBrowserKeysAsync(refused).ConfigureAwait(false);

        return left && BrowserRowsOf(variables) == 0;
    }

    /// <summary>Takes these rows out of the browser list in one call, leaving any another tab added. Never throws.</summary>
    private async Task<bool> RemoveBrowserKeysAsync(IReadOnlyCollection<VariableDatasamlingKey> keys)
    {
        if (_browserStore is not { } store)
        {
            return false;
        }

        var removed = await InTurnAsync(async () =>
        {
            try
            {
                if (await store.RemoveKeysAsync(keys).ConfigureAwait(false) is { } status)
                {
                    await RereadKeysAsync(store, status).ConfigureAwait(false);

                    // Still in the stored value, the rows reach another page, which would offer them again.
                    return !status.StillStored;
                }

                BrowserListAvailable = false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "could not take the carried rows out of the browser's variable list");
                BrowserListAvailable = false;
            }

            return false;
        }).ConfigureAwait(false);

        BrowserChanged();
        return removed;
    }

    // Only the list this press made. The limiter that refused the add may refuse this too: the empty list then stays,
    // drawn among the reader's lists on the next visit or beside their next list, where they can delete it.
    private async Task DiscardAsync(VariableList list, int reader)
    {
        try
        {
            await DeleteAsync(list.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "could not delete list {ListId} after a carry-over failed", list.Id);

            if (StillCurrent(reader))
            {
                _lists = [.. _lists.Where(l => l.Id != list.Id), list];
                _madeHere = [.. _madeHere, list];
                RaiseChanged(list.Id, affectsRows: false);
            }
        }
    }

    // The catalogue fields and nothing the reader wrote, whatever the caller handed in. The README names each one.
    internal static VariableListItem StoredFields(VariableListItem item) => new()
    {
        VariableId = item.VariableId,
        VariableCode = item.VariableCode,
        VariableName = item.VariableName,
        KildeId = item.KildeId,
        KildeName = item.KildeName,
        KildeShortName = item.KildeShortName,
        DatasamlingId = item.DatasamlingId,
        DatasamlingName = item.DatasamlingName,
        VariabelgruppeName = item.VariabelgruppeName,
        DataType = item.DataType,
        DataFrom = item.DataFrom,
        DataTo = item.DataTo,
        VersionStatus = item.VersionStatus,
    };
}

/// <summary>Where the browser list is kept: the reader's browser, through the package's script.</summary>
/// <remarks>Null from any member means the browser could not be asked; a fault in the script may throw, and the state logs it.</remarks>
internal interface IBrowserListStore
{
    /// <summary>True once the host has refused the module for good, a 404 or a Content-Security-Policy.</summary>
    bool Refused { get; }

    Task<BrowserListStatus?> CountAsync();

    Task<BrowserListStatus?> AddAsync(VariableListItem row, int max);

    Task<BrowserListStatus?> RemoveAsync(VariableDatasamlingKey key);

    Task<BrowserListStatus?> ClearAsync();

    /// <summary>Takes out exactly these rows, in one call.</summary>
    Task<BrowserListStatus?> RemoveKeysAsync(IReadOnlyCollection<VariableDatasamlingKey> keys);

    /// <summary>Keys from <paramref name="offset"/>; an empty answer is the end of the list.</summary>
    Task<IReadOnlyList<VariableDatasamlingKey>?> KeysAsync(int offset, int count);

    /// <summary>Rows from <paramref name="offset"/>; an empty answer is the end of the list.</summary>
    Task<IReadOnlyList<BrowserRow>?> PageAsync(int offset, int count);

    /// <summary>Calls <paramref name="changedElsewhere"/> when another tab changes the list; false when it could not listen.</summary>
    Task<bool> WatchAsync(Func<Task> changedElsewhere);

    /// <summary>A carry-over's claim on the rows, which a press in another tab finds; it lapses after <paramref name="lifetime"/>.</summary>
    Task<BrowserCarryClaim?> ClaimCarryOverAsync(TimeSpan lifetime, TimeSpan settle);

    /// <summary>Gives up this press's claim, and no other tab's.</summary>
    Task ReleaseCarryOverAsync(string token);
}

/// <summary>Whether a carry-over may go on; a null token where storage gave no claim to take, or to release.</summary>
internal sealed record BrowserCarryClaim(
    [property: JsonPropertyName("taken")] bool Taken,
    [property: JsonPropertyName("token")] string? Token);

/// <summary>What the browser answers after any call: the row count, and whether the list is stored past this page.</summary>
/// <param name="Ok">False when the call changed nothing it was asked to: a full list.</param>
/// <param name="Count">Rows after the call.</param>
/// <param name="Kept">Whether storage holds the list; false while it lives in the page alone.</param>
/// <param name="Full">The add was refused at the ceiling.</param>
/// <param name="Foreign">The key holds a value this package did not write, which is neither read nor overwritten.</param>
/// <param name="StillStored">After a removal: some removed row is still in the stored value, where another page finds it.</param>
internal sealed record BrowserListStatus(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("kept")] bool Kept,
    [property: JsonPropertyName("full")] bool Full,
    [property: JsonPropertyName("foreign")] bool Foreign,
    [property: JsonPropertyName("stillStored")] bool StillStored = false);

/// <summary>One row as the browser handed it over: whole, or as its ids and code alone when it was too long to send.</summary>
internal sealed record BrowserRow(VariableListItem Item, bool Truncated);

/// <summary>The module's spelling of a row's key: <c>variabelId|datasamlingId</c>, the datasamling empty for none.</summary>
internal static class BrowserListKeys
{
    internal static string Spell(VariableDatasamlingKey key) => $"{key.VariableId}|{key.DatasamlingId}";

    internal static VariableDatasamlingKey? Parse(string? key)
    {
        var parts = key?.Split('|');

        if (parts is not { Length: 2 } || !Guid.TryParse(parts[0], out var variable))
        {
            return null;
        }

        return new VariableDatasamlingKey(variable, Guid.TryParse(parts[1], out var datasamling) ? datasamling : null);
    }
}
