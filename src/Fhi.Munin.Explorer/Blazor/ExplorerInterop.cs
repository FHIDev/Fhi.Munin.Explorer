using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The package's one JavaScript module, imported once per component and disposed with it.
/// </summary>
/// <remarks>
/// Nothing rendered depends on it, and a refused import answers "not there" rather than throwing:
/// an unhandled rejection on a legacy Blazor Server circuit takes the circuit down, which costs the
/// reader the page (Fhi.Metadata-35w0p.14). The module is optional on those terms, and so is every
/// export where the module is absent. A <see cref="JSException"/> from an export that IS there
/// means something else — a defect inside the module — and each export below says whether it
/// answers for one or leaves it to its caller.
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

    // A 404 or a Content-Security-Policy is the host's answer for good, and the caller retries a
    // failed load on a later render: without this, a host serving no module pays an import a render.
    private bool _refused;

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
    /// Imports the module, keeping at most one however often it is asked, and answers whether it
    /// is there.
    /// </summary>
    /// <remarks>
    /// <b>Call from <c>OnAfterRenderAsync</c> and nowhere else.</b> There is no DOM and no JS
    /// runtime during prerender, so an import from any earlier lifecycle method fails for a reason
    /// that has nothing to do with whether the host serves the file.
    /// <para>
    /// A later render may call it again, and should where the first answered false: an import a
    /// reconnecting circuit could not carry is "not yet" rather than "not there". A host that
    /// actually serves no such file is remembered, so asking again is free after the first answer.
    /// </para>
    /// </remarks>
    internal async Task<bool> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        if (_module is null && !_disposed && !_refused)
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

    /// <summary>
    /// Shows the sticky fact bar <paramref name="barId"/> while the hero fact row
    /// <paramref name="factsId"/> is off the top of the viewport, and hides it again otherwise.
    /// </summary>
    /// <remarks>
    /// <b>Call it after <see cref="TryLoadAsync"/>, from the same render.</b> Where the module is
    /// not there it does nothing, which is the required behaviour: the bar repeats what is on the
    /// page already. Nothing about the scroll comes back to the server. Calling it again for the
    /// same <paramref name="barId"/> replaces that bar's observer rather than adding a second.
    /// <para>
    /// A <see cref="JSException"/> travels on, unlike <see cref="DisconnectHeroFactsAsync"/>'s: this
    /// one can only be raised by the export itself, which is a defect in the module and not a
    /// browser out of reach, and one nothing reports is a bar that never works with nothing in any
    /// host's log to say so. <b>The caller catches it</b> — see <see cref="DetailPage"/>, which
    /// calls this from a render continuation where an escape would take the circuit down.
    /// </para>
    /// </remarks>
    internal async Task ObserveHeroFactsAsync(string barId, string factsId)
    {
        if (_module is not { } module)
        {
            return;
        }

        await Tolerated(() => module.InvokeVoidAsync("observeHeroFacts", barId, factsId))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Stops watching for <paramref name="barId"/>, so the observer goes with the component rather
    /// than with the page.
    /// </summary>
    /// <remarks>
    /// Tolerates a <see cref="JSException"/> as well as the three <see cref="Tolerated{T}"/> covers,
    /// unlike <see cref="ObserveHeroFactsAsync"/>: this one is called from disposal, where a throw
    /// is unhandled in the renderer and takes the circuit down.
    /// </remarks>
    internal async Task DisconnectHeroFactsAsync(string barId)
    {
        if (_module is not { } module)
        {
            return;
        }

        try
        {
            await Tolerated(() => module.InvokeVoidAsync("disconnectHeroFacts", barId))
                .ConfigureAwait(false);
        }
        // The browser has moved on — a reconnect, or the page already gone. So has the observer.
        catch (JSException)
        {
        }
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
    /// Takes <paramref name="module"/> as this instance's, unless there is already one or disposal
    /// got here first. False means the caller still owns what it was handed.
    /// </summary>
    /// <remarks>
    /// The check and the assignment are one step because they are not on the same thread as
    /// <see cref="DisposeAsync"/>: read and assign apart and a disposal in between leaves the
    /// arriving reference on a dead instance, which is the leak this answers.
    /// <para>
    /// A module already here is refused too, since two imports can overlap: assigning anyway drops
    /// a live reference nothing releases, or — where the later one failed tolerably and so arrives
    /// as null — nulls a live one, leaving the observers it registered connected for good.
    /// </para>
    /// </remarks>
    private bool Kept(IJSObjectReference? module)
    {
        lock (_gate)
        {
            if (_disposed || _module is not null || module is null)
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
        // The host serves no such file, or a Content-Security-Policy refused it. Not a reconnect:
        // JSDisconnectedException derives from this one, and Tolerated answers it inside the try
        // above, so a dropped circuit is null without the latch and the caller may ask again.
        catch (JSException)
        {
            lock (_gate)
            {
                _refused = true;
            }

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
