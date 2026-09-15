using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>A runtime that refuses every call, the way a host without the module does.</summary>
internal sealed class RefusingJsRuntime(Exception thrown) : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw thrown;

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) => throw thrown;
}

/// <summary>A runtime that hands out one module, counting how often it was asked for it.</summary>
/// <remarks>
/// Only <c>import</c> is answered with the module: a component under test reaches the browser for
/// <c>history.replaceState</c> as well, and handing that call a module fails the cast.
/// </remarks>
internal sealed class LendingJsRuntime(IJSObjectReference module) : IJSRuntime
{
    internal int Imports { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier != "import")
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        Imports++;

        return ValueTask.FromResult((TValue)(object)module);
    }
}

/// <summary>
/// A runtime that lends a module and holds every other call until the test answers it.
/// </summary>
/// <remarks>
/// The other call a mounted explorer makes is <c>history.replaceState</c>, and holding it suspends
/// <c>OnAfterRenderAsync</c> before the import — which is where a disposal has to be survived.
/// </remarks>
internal sealed class HoldingJsRuntime(IJSObjectReference module) : IJSRuntime
{
    private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the held call answer, as a browser that has caught up would.</summary>
    internal void Release() => _held.SetResult();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier == "import")
        {
            return (TValue)(object)module;
        }

        await _held.Task;

        return default!;
    }
}

/// <summary>A runtime whose import stays in flight until the test answers it.</summary>
internal sealed class PendingJsRuntime(IJSObjectReference module) : IJSRuntime
{
    private readonly TaskCompletionSource<IJSObjectReference> _answer =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the import that is waiting resolve, as a browser answering it would.</summary>
    internal void Answer() => _answer.SetResult(module);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) =>
        (TValue)(object)await _answer.Task;
}

/// <summary>A module whose disposal throws, as one on a dropped circuit does.</summary>
internal sealed class RefusingModule(Exception thrown) : IJSObjectReference
{
    public ValueTask DisposeAsync() => throw thrown;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw thrown;

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) => throw thrown;
}

/// <summary>A module that counts its own disposals.</summary>
internal sealed class CountingModule : IJSObjectReference
{
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal int Disposals { get; private set; }

    /// <summary>Completes on the first disposal, for a test whose release is somebody else's.</summary>
    internal Task Released => _released.Task;

    public ValueTask DisposeAsync()
    {
        Disposals++;
        _released.TrySetResult();

        return ValueTask.CompletedTask;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        ValueTask.FromResult(default(TValue)!);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) =>
        ValueTask.FromResult(default(TValue)!);
}
