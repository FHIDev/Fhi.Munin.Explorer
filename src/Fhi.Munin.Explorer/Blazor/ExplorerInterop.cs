using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The package's one JavaScript module, imported once per component and disposed with it.
/// </summary>
/// <remarks>
/// Nothing reads it yet. It exists so a later enhancement has a module to put itself in, and every
/// call here answers "not there" rather than throwing: an import rejection on a legacy Blazor Server
/// circuit takes the circuit down, which costs the reader the page (Fhi.Metadata-35w0p.14).
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
    private IJSObjectReference? _module;

    internal ExplorerInterop(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);

        _js = js;
    }

    /// <summary>Whether the module is loaded, and so whether its exports can be called.</summary>
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
        _module ??= await Tolerated(
            () => _js.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath))
            .ConfigureAwait(false);

        return _module is not null;
    }

    /// <summary>
    /// The package version the page itself reports — and null whenever the module is not there to
    /// be asked, which is every caller's ordinary case rather than an error.
    /// </summary>
    internal async Task<string?> TryReadPageVersionAsync(CancellationToken cancellationToken = default)
    {
        var module = _module;

        return module is null
            ? null
            : await Tolerated(() => module.InvokeAsync<string>("packageVersion", cancellationToken))
                .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var module = _module;
        _module = null;

        if (module is not null)
        {
            await Tolerated(module.DisposeAsync).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs <paramref name="call"/>, answering null wherever the browser is out of reach.
    /// </summary>
    /// <remarks>
    /// The four clauses are the whole list of what "out of reach" means, in one place. Anything
    /// else travels on: a fault inside the module is a defect to read in a host's log, and only
    /// these four are the ordinary shape of a reader whose browser was never going to answer.
    /// </remarks>
    private static async Task<T?> Tolerated<T>(Func<ValueTask<T>> call) where T : class
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        // The host serves no such file, or a Content-Security-Policy refused it.
        catch (JSException)
        {
            return null;
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
