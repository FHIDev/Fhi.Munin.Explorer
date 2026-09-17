using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>A runtime that refuses every call, the way a host without the module does.</summary>
internal sealed class RefusingJsRuntime(Exception thrown) : IJSRuntime
{
    private int _imports;

    /// <summary>How often an import was attempted — a refusal the caller retried is one more.</summary>
    /// <remarks>Counted atomically: written from a render continuation and read from the test.</remarks>
    internal int Imports => Volatile.Read(ref _imports);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier == "import")
        {
            Interlocked.Increment(ref _imports);
        }

        throw thrown;
    }
}

/// <summary>
/// A runtime whose first import fails the way a reconnecting circuit does, and whose next answers.
/// </summary>
/// <remarks>
/// The distinction the retry rests on: a <see cref="JSDisconnectedException"/> says "not yet" and a
/// <see cref="JSException"/> says "not there". Only the first is worth a later render asking again.
/// </remarks>
internal sealed class FlakyJsRuntime(IJSObjectReference module) : IJSRuntime
{
    private int _imports;

    /// <inheritdoc cref="RefusingJsRuntime.Imports" />
    internal int Imports => Volatile.Read(ref _imports);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier != "import")
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        return Interlocked.Increment(ref _imports) == 1
            ? throw new JSDisconnectedException("the circuit is reconnecting")
            : ValueTask.FromResult((TValue)(object)module);
    }
}

/// <summary>A runtime that hands out one module, counting how often it was asked for it.</summary>
/// <remarks>
/// Only <c>import</c> is answered with the module: a component under test reaches the browser for
/// <c>history.replaceState</c> as well, and handing that call a module fails the cast.
/// </remarks>
internal sealed class LendingJsRuntime(IJSObjectReference module) : IJSRuntime
{
    private int _imports;

    /// <inheritdoc cref="RefusingJsRuntime.Imports" />
    internal int Imports => Volatile.Read(ref _imports);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier != "import")
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        Interlocked.Increment(ref _imports);

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
/// <remarks>
/// Only <c>import</c> waits on the answer, and only it is answered with the module, for the reason
/// <see cref="LendingJsRuntime"/> gives: a component reaches the browser for other calls too.
/// </remarks>
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
        identifier == "import" ? (TValue)(object)await _answer.Task : default!;
}

/// <summary>A module whose disposal throws, as one on a dropped circuit does.</summary>
internal sealed class RefusingModule(Exception thrown) : IJSObjectReference
{
    public ValueTask DisposeAsync() => throw thrown;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw thrown;

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args) => throw thrown;
}

/// <summary>A module that remembers what it was called with.</summary>
/// <remarks>
/// The exports take ids rather than elements, so what a test has to be able to read back is the
/// arguments: a call that reached the module with the wrong instance's id would drive the wrong
/// bar, and counting calls alone cannot tell the two apart.
/// </remarks>
/// <param name="stalls">
/// An export that is recorded and then held until <see cref="RecordingModule.Answer"/>, for a test
/// staging a disposal that lands while that call is still out. Null holds nothing.
/// </param>
internal sealed class RecordingModule(string? stalls = null) : IJSObjectReference
{
    // Written from a render continuation and read from the test, which are not the same thread.
    private readonly Lock _gate = new();
    private readonly List<(string Identifier, object?[] Arguments)> _calls = [];

    private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets the held export return, as a browser that has caught up would.</summary>
    internal void Answer() => _held.TrySetResult();

    internal IReadOnlyList<(string Identifier, object?[] Arguments)> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>Every call to <paramref name="identifier"/>, in order, with its first argument.</summary>
    internal IReadOnlyList<string> ArgumentsOf(string identifier) =>
    [
        .. Calls.Where(call => call.Identifier == identifier)
            .Select(call => (call.Arguments.ElementAtOrDefault(0) as string) ?? "")
    ];

    /// <summary>
    /// Waits until <paramref name="identifier"/> has been called, for a continuation the test does
    /// not hold and so cannot await. False where it never arrived.
    /// </summary>
    internal async Task<bool> ReachedAsync(string identifier)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (Calls.Any(call => call.Identifier == identifier))
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    /// <summary>What a release is recorded under, so a test can wait for one it does not hold.</summary>
    /// <remarks>Bracketed, so it can never collide with an export the module really has.</remarks>
    internal const string Released = "[release]";

    public ValueTask DisposeAsync()
    {
        Record(Released, []);

        return ValueTask.CompletedTask;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        // Recorded before the hold, so a test waiting for the call to have STARTED can see it.
        Record(identifier, args ?? []);

        if (identifier == stalls)
        {
            await _held.Task;
        }

        return default!;
    }

    private void Record(string identifier, object?[] arguments)
    {
        lock (_gate)
        {
            _calls.Add((identifier, arguments));
        }
    }
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

/// <summary>A runtime whose imports are held and answered one at a time, in the order a test says.</summary>
/// <remarks>
/// <see cref="PendingJsRuntime"/> answers every import with the one reference and all of them at
/// once, which can neither tell a second arrival being released from a first never asked for, nor
/// stage the order two overlapping imports land in — and that order decides which one is kept.
/// </remarks>
internal sealed class StagingJsRuntime : IJSRuntime
{
    private readonly Lock _gate = new();
    private readonly List<TaskCompletionSource<IJSObjectReference>> _held = [];

    /// <summary>How many imports are waiting, so a test can see the overlap it staged.</summary>
    internal int Imports
    {
        get
        {
            lock (_gate)
            {
                return _held.Count;
            }
        }
    }

    /// <summary>
    /// Answers the <paramref name="import"/>th import, counted from one, with
    /// <paramref name="module"/> — or, where that is null, the way a reconnecting circuit does.
    /// </summary>
    internal void Answer(int import, IJSObjectReference? module)
    {
        TaskCompletionSource<IJSObjectReference> held;

        lock (_gate)
        {
            held = _held[import - 1];
        }

        if (module is null)
        {
            held.SetException(new JSDisconnectedException("the circuit is reconnecting"));
        }
        else
        {
            held.SetResult(module);
        }
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public async ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier != "import")
        {
            return default!;
        }

        var mine = new TaskCompletionSource<IJSObjectReference>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            _held.Add(mine);
        }

        return (TValue)(object)await mine.Task;
    }
}
