using System.Text.Json;
using System.Text.Json.Serialization;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.State;

/// <summary>
/// A signed-out reader's one list, kept in their browser rather than in Munin (Fhi.Metadata-emsc0.3.12). Offered
/// only when the host turns <see cref="MuninExplorerFeatures.AnonymousLists"/> on.
/// </summary>
/// <remarks>
/// The rows carry the catalogue fields a share snapshot carries and nothing about the reader, stored in the
/// snapshot's own shape so <see cref="SharedSnapshot"/> reads them back as tolerantly. Storage can be refused; the
/// list then lasts for the circuit and <see cref="BrowserListKept"/> says so.
/// </remarks>
public sealed partial class VariableListState
{
    /// <summary>The anonymous export's ceiling, Munin's <c>MaxVariabelCount</c>.</summary>
    internal const int MaxBrowserListRows = 2000;

    private IBrowserListStore? _browserStore;
    private List<VariableListItem> _browserItems = [];
    private string? _browserName;
    private Task? _browserRead;

    /// <summary>What a press on the save button did to the browser list.</summary>
    internal enum BrowserListOutcome
    {
        Added,
        Removed,

        /// <summary>Refused: the list already holds <see cref="MaxBrowserListRows"/> rows.</summary>
        Full
    }

    /// <summary>Whether the host offers the browser list, which a component says once it has a JS runtime.</summary>
    internal bool OffersBrowserList => _browserStore is not null;

    /// <summary>Whether saves go to the browser list rather than to Munin: offered, and the reader is signed out.</summary>
    internal bool UsesBrowserList => !IsAuthenticated && OffersBrowserList;

    /// <summary>The browser list's rows, oldest first. Kept across a sign-in so the reader can carry them over.</summary>
    internal IReadOnlyList<VariableListItem> BrowserItems => _browserItems;

    /// <summary>The name the list was first saved under, or null before it has one.</summary>
    internal string? BrowserListName => _browserName;

    /// <summary>Whether the stored list has been read, so an empty one is an answer.</summary>
    internal bool BrowserListRead => _browserRead is { IsCompleted: true };

    /// <summary>False once the browser refused to store the list: it lasts only for this visit.</summary>
    internal bool BrowserListKept { get; private set; } = true;

    /// <summary>Turns the browser list on for this circuit. The first store wins: every component reaches the same browser.</summary>
    internal void OfferBrowserList(IBrowserListStore store) => _browserStore ??= store;

    /// <summary>
    /// Reads the stored list once per circuit, or joins the read in flight. Call from <c>OnAfterRenderAsync</c>:
    /// there is no browser during prerender. Never throws.
    /// </summary>
    internal Task ReadBrowserListAsync()
    {
        if (_browserStore is null)
        {
            return Task.CompletedTask;
        }

        return _browserRead ??= ReadBrowserListOnceAsync(_browserStore);
    }

    private async Task ReadBrowserListOnceAsync(IBrowserListStore store)
    {
        try
        {
            var stored = await store.ReadAsync().ConfigureAwait(false);

            if (stored is not { Kept: true })
            {
                BrowserListKept = false;
            }
            else if (stored.Json is { Length: > 0 } json)
            {
                using var document = JsonDocument.Parse(json);
                var list = SharedSnapshot.Read(document.RootElement);

                // A press made while the read was out is newer than the stored rows, so it is kept after them.
                var pressed = _browserItems.Select(VariableDatasamlingKey.Of).ToHashSet();
                _browserItems = [.. list.Items.Where(i => !pressed.Contains(VariableDatasamlingKey.Of(i))).Take(MaxBrowserListRows), .. _browserItems];
                _browserName ??= list.Name.Length > 0 ? list.Name : null;
            }
        }
        catch (Exception ex)
        {
            // A refused module, a broken script or an unreadable value: the visit keeps a list of its own.
            _logger?.LogWarning(ex, "could not read the browser's variable list");
            BrowserListKept = false;
        }

        RaiseChanged(listId: null, affectsRows: true);
    }

    /// <summary>Puts the row in the browser list, or takes it out if <paramref name="drawnAsSaved"/>.</summary>
    /// <param name="item">The row with the catalogue fields it is shown with; nothing else is stored.</param>
    /// <param name="drawnAsSaved">What the button said, read before any await, for the reason <c>ToggleAsync</c> gives.</param>
    /// <param name="nameForFirstList">The list's name the first time it is stored.</param>
    internal async Task<BrowserListOutcome> ToggleInBrowserListAsync(
        VariableListItem item, bool drawnAsSaved, string nameForFirstList)
    {
        await ReadBrowserListAsync().ConfigureAwait(false);

        var key = VariableDatasamlingKey.Of(item);
        BrowserListOutcome outcome;

        if (drawnAsSaved)
        {
            _browserItems.RemoveAll(i => VariableDatasamlingKey.Of(i) == key);
            outcome = BrowserListOutcome.Removed;
        }
        else if (_browserItems.Any(i => VariableDatasamlingKey.Of(i) == key))
        {
            return BrowserListOutcome.Added;
        }
        else if (_browserItems.Count >= MaxBrowserListRows)
        {
            return BrowserListOutcome.Full;
        }
        else
        {
            _browserItems.Add(StoredFields(item));
            _browserName ??= nameForFirstList;
            outcome = BrowserListOutcome.Added;
        }

        await WriteBrowserListAsync().ConfigureAwait(false);
        RaiseChanged(listId: null, affectsRows: true);

        return outcome;
    }

    /// <summary>Empties the browser list and forgets its key. Never throws.</summary>
    internal async Task ClearBrowserListAsync()
    {
        _browserItems = [];
        _browserName = null;
        await WriteBrowserListAsync().ConfigureAwait(false);
        RaiseChanged(listId: null, affectsRows: true);
    }

    /// <summary>
    /// Saves the browser list as a new list of the signed-in reader's, then empties it. Null when nothing was made;
    /// a failure leaves the browser list as it was, and throws what the API threw.
    /// </summary>
    internal async Task<VariableList?> CarryBrowserListOverAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated || _browserItems.Count == 0)
        {
            return null;
        }

        var rows = _browserItems.Select(VariableDatasamlingKey.Of).Distinct().ToList();
        var created = await CreateAsync(name, cancellationToken).ConfigureAwait(false);

        if (created is null)
        {
            return null;
        }

        bool added;

        try
        {
            added = true;

            foreach (var batch in rows.Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
            {
                added = added && await AddItemsAsync(created.Id, batch, kilder: null, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // An empty list the reader never asked for is not left behind by a half-done carry-over.
            await DeleteQuietlyAsync(created.Id).ConfigureAwait(false);
            throw;
        }

        if (!added)
        {
            await DeleteQuietlyAsync(created.Id).ConfigureAwait(false);
            return null;
        }

        await ClearBrowserListAsync().ConfigureAwait(false);
        return created;
    }

    private async Task DeleteQuietlyAsync(Guid id)
    {
        try
        {
            await DeleteAsync(id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "could not delete list {ListId} after a carry-over failed", id);
        }
    }

    // The catalogue fields and nothing the reader wrote, whatever the caller handed in.
    private static VariableListItem StoredFields(VariableListItem item) => new()
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

    private async Task WriteBrowserListAsync()
    {
        if (_browserStore is not { } store)
        {
            return;
        }

        try
        {
            var json = _browserItems.Count == 0
                ? null
                : JsonSerializer.Serialize(new StoredList(
                    1, _browserName ?? "", DateTimeOffset.UtcNow,
                    [.. _browserItems.Select(MuninExplorerClient.SharedItemBody.From)]));

            if (!await store.WriteAsync(json).ConfigureAwait(false))
            {
                BrowserListKept = false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "could not store the browser's variable list");
            BrowserListKept = false;
        }
    }

    /// <summary>What the browser stores: the share snapshot's shape, versioned.</summary>
    private sealed record StoredList(
        [property: JsonPropertyName("v")] int Version,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt,
        [property: JsonPropertyName("items")] IReadOnlyList<MuninExplorerClient.SharedItemBody> Items);
}

/// <summary>Where the browser list is kept: the reader's browser, through the package's script.</summary>
/// <remarks>Neither member throws for a refused storage; a fault in the script may, and the state logs it.</remarks>
internal interface IBrowserListStore
{
    /// <summary>The stored value, with <c>Kept</c> false where the browser refused storage; null when unreachable.</summary>
    Task<StoredBrowserList?> ReadAsync();

    /// <summary>Stores <paramref name="json"/>, or forgets the key for null; false where the browser did not keep it.</summary>
    Task<bool> WriteAsync(string? json);
}

/// <summary>What the browser holds under the anonymous list's key, and whether its storage answered at all.</summary>
internal sealed record StoredBrowserList(bool Kept, string? Json);
