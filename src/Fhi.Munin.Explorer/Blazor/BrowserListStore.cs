using Fhi.Munin.Explorer.State;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The browser list kept in local storage through <see cref="ExplorerInterop"/>, one import per call.</summary>
/// <remarks>A host that serves no module keeps nothing, which reads as storage refused rather than as an error.</remarks>
internal sealed class BrowserListStore(IJSRuntime js) : IBrowserListStore
{
    public Task<StoredBrowserList?> ReadAsync() => ExplorerInterop.ReadAnonymousListAsync(js);

    public Task<bool> WriteAsync(string? json) => ExplorerInterop.WriteAnonymousListAsync(js, json);
}
