using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The package's one JavaScript module, imported once per component and disposed with it.
/// </summary>
/// <remarks>
/// Nothing reads it yet. It exists so a later enhancement has a module to put itself in, and a
/// refused import answers "not there" rather than throwing: an unhandled rejection on a legacy
/// Blazor Server circuit takes the circuit down, which costs the reader the page (Fhi.Metadata-35w0p.14).
/// </remarks>
internal sealed class ExplorerInterop : IAsyncDisposable
{
    /// <summary>The module's file name, as it sits in the RCL's <c>wwwroot</c>.</summary>
    internal const string ModuleFile = "explorer-interop.js";

    // Off the assembly rather than written down: the segment the SDK publishes these assets under
    // is the assembly's own name, so a literal here would survive a project rename as a 404.
    internal static string ModulePath { get; } =
        $"./_content/{typeof(ExplorerInterop).Assembly.GetName().Name}/{ModuleFile}";

    private readonly IJSRuntime _js;

    // The import's continuation resumes off the renderer's synchronization context and a disposal
    // runs on the dispatcher, so the two reach these at once: exactly one may keep the module.
    private readonly Lock _gate = new();

    private IJSObjectReference? _module;
    private bool _disposed;

    internal ExplorerInterop(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);

        _js = js;
    }

    /// <summary>
    /// Whether the module is there — and so, once it has one, whether an export can be called.
    /// </summary>
    internal bool IsLoaded => _module is not null;

    /// <summary>
    /// Imports the module at most once, and answers whether it is there.
    /// </summary>
    /// <remarks>
    /// <b>Call from <c>OnAfterRenderAsync(firstRender: true)</c> and nowhere else.</b> There is no
    /// DOM and no JS runtime during prerender, so an import from any earlier lifecycle method fails
    /// for a reason that has nothing to do with whether the host serves the file.
    /// </remarks>
    internal async Task<bool> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        if (_module is null && !_disposed)
        {
            var module = await Fetched(cancellationToken).ConfigureAwait(false);

            // Disposal can land while the import is in flight, and `??=` would assign anyway: the
            // answer would be a live reference on a dead instance that nothing releases again.
            if (!Kept(module))
            {
                await Released(module).ConfigureAwait(false);
            }
        }

        return _module is not null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IJSObjectReference? module;

        lock (_gate)
        {
            module = _module;
            _module = null;
            _disposed = true;
        }

        await Released(module).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes <paramref name="module"/> as this instance's, unless disposal got here first.
    /// </summary>
    /// <remarks>
    /// The check and the assignment are one step because they are not on the same thread as
    /// <see cref="DisposeAsync"/>: read and assign apart and a disposal in between leaves the
    /// arriving reference on a dead instance, which is the leak this answers.
    /// </remarks>
    private bool Kept(IJSObjectReference? module)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            _module = module;

            return true;
        }
    }

    /// <summary>
    /// The import, answering null where the host does not serve the file as well as where the
    /// browser is out of reach.
    /// </summary>
    private async Task<IJSObjectReference?> Fetched(CancellationToken cancellationToken)
    {
        try
        {
            return await Tolerated(
                () => _js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath))
                .ConfigureAwait(false);
        }
        // The host serves no such file, or a Content-Security-Policy refused it. Only an import is
        // ordinary this way: the same exception out of an export is a defect in the module itself.
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>Releases <paramref name="module"/> if there is one, tolerating a dead circuit.</summary>
    /// <remarks>
    /// A release is a round trip on Blazor Server, so a browser whose reference table has moved on
    /// answers it with a <see cref="JSException"/>. Unlike an export's, that one is not a defect to
    /// surface: a throw out of disposal is unhandled in the renderer and takes the circuit down.
    /// </remarks>
    private static async Task Released(IJSObjectReference? module)
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await Tolerated(module.DisposeAsync).ConfigureAwait(false);
        }
        // The reference is already gone from the browser's table. So is the thing it was holding.
        catch (JSException)
        {
        }
    }

    /// <summary>
    /// Runs <paramref name="call"/>, answering null wherever the browser is out of reach.
    /// </summary>
    /// <remarks>
    /// The three clauses are the whole list of what "out of reach" means, in one place. A
    /// <see cref="JSException"/> travels on: that is how a fault inside an export surfaces, and the
    /// two calls where it means something milder answer for it themselves.
    /// </remarks>
    private static async Task<T?> Tolerated<T>(Func<ValueTask<T>> call) where T : class
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        // The reader closed the tab. On disposal this is the normal case rather than an edge one.
        catch (JSDisconnectedException)
        {
            return null;
        }
        // The reader navigated away while the call was in flight.
        catch (OperationCanceledException)
        {
            return null;
        }
        // No JS runtime to call: a prerender, or a circuit already torn down — ObjectDisposedException
        // is one of these too.
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The same, for a call with nothing to answer.</summary>
    private static Task Tolerated(Func<ValueTask> call) =>
        Tolerated<object>(async () =>
        {
            await call().ConfigureAwait(false);

            return null!;
        });
}
