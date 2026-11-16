using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The browser list, kept by <see cref="ExplorerInterop"/>'s module through one instance for the circuit.</summary>
/// <remarks>A host that serves no module has no list: every call answers null, which the state reads as unavailable.</remarks>
internal sealed class BrowserListStore(IJSRuntime js) : IBrowserListStore, IAsyncDisposable
{
    private readonly ExplorerInterop _interop = new(js);
    private DotNetObjectReference<ElsewhereListener>? _listener;

    public Task<BrowserListStatus?> CountAsync() => _interop.AnonymousListCountAsync();

    public Task<BrowserListStatus?> AddAsync(VariableListItem row, int max) =>
        _interop.AnonymousListAddAsync(MuninExplorerClient.SharedItemBody.From(row), max);

    public Task<BrowserListStatus?> RemoveAsync(VariableDatasamlingKey key) =>
        _interop.AnonymousListRemoveAsync(key.VariableId, key.DatasamlingId);

    public Task<BrowserListStatus?> RemoveKeysAsync(IReadOnlyCollection<VariableDatasamlingKey> keys) =>
        _interop.AnonymousListRemoveKeysAsync([.. keys.Select(BrowserListKeys.Spell)]);

    public Task<BrowserListStatus?> ClearAsync() => _interop.AnonymousListClearAsync();

    public async Task<IReadOnlyList<VariableDatasamlingKey>?> KeysAsync(int offset, int count) =>
        await _interop.AnonymousListKeysAsync(offset, count).ConfigureAwait(false) is { } keys
            ? [.. keys.Select(BrowserListKeys.Parse).OfType<VariableDatasamlingKey>()]
            : null;

    public async Task<IReadOnlyList<VariableListItem>?> PageAsync(int offset, int count)
    {
        using var page = await _interop.AnonymousListPageAsync(offset, count).ConfigureAwait(false);

        return page is null ? null : SharedSnapshot.Read(page.RootElement).Items;
    }

    public Task<bool> WatchAsync(Func<Task> changedElsewhere)
    {
        _listener ??= DotNetObjectReference.Create(new ElsewhereListener(changedElsewhere));

        return _interop.WatchAnonymousListAsync(_listener);
    }

    public async ValueTask DisposeAsync()
    {
        _listener?.Dispose();
        await _interop.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>What the module calls when another tab writes the key.</summary>
    internal sealed class ElsewhereListener(Func<Task> changed)
    {
        [JSInvokable]
        public Task ChangedElsewhere() => changed();
    }
}
