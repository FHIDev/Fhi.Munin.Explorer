using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Every URL written to the address bar, in call order, over bUnit's own runtime. bUnit groups its
/// recorded invocations by identifier, so a pushState and a later replaceState cannot be ordered there.
/// </summary>
internal sealed class HistoryRecorder(IJSRuntime inner) : IJSRuntime
{
    public List<string?> Written { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Record(identifier, args);

        return inner.InvokeAsync<TValue>(identifier, args);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Record(identifier, args);

        return inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
    }

    private void Record(string identifier, object?[]? args)
    {
        if (identifier is "history.pushState" or "history.replaceState" && args is [_, _, var url])
        {
            Written.Add(url as string);
        }
    }
}
