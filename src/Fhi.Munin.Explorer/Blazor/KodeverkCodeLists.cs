using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which kodeverk link a code list belongs to: the pair the codes endpoint is addressed by.</summary>
internal readonly record struct KodeverkKey(string Type, string Reference)
{
    public static KodeverkKey Of(KodeverkLink link) => new(link.KodeverkType, link.KodeverkReference);
}

internal enum CodesFailure
{
    Throttled,
    Failed
}

/// <summary>The code lists opened under one variable, and what was fetched for each.</summary>
/// <remarks>
/// One instance per open variable, replaced rather than emptied when another opens: a fetch still in
/// flight then writes into the instance nobody draws, so no generation counter is needed.
/// </remarks>
internal sealed class KodeverkCodeLists(Guid variableId, IMuninExplorerClient client, ILogger? log)
{
    private readonly HashSet<KodeverkKey> _open = [];
    private readonly Dictionary<KodeverkKey, IReadOnlyList<KodeverkCode>> _codes = [];
    private readonly HashSet<KodeverkKey> _loading = [];
    private readonly Dictionary<KodeverkKey, CodesFailure> _failures = [];

    public Guid VariableId => variableId;

    public bool IsOpen(KodeverkKey key) => _open.Contains(key);

    public bool IsLoading(KodeverkKey key) => _loading.Contains(key);

    public bool HasFailed(KodeverkKey key) => _failures.ContainsKey(key);

    public CodesFailure? FailureOf(KodeverkKey key) => _failures.TryGetValue(key, out var failure) ? failure : null;

    public IReadOnlyList<KodeverkCode>? CodesOf(KodeverkKey key) => _codes.GetValueOrDefault(key);

    /// <summary>Open this link's list, or close it. A failed list is fetched again: the press is the reader's only retry.</summary>
    public async Task ToggleAsync(KodeverkLink link, Action changed)
    {
        var key = KodeverkKey.Of(link);

        if (!_open.Add(key))
        {
            _open.Remove(key);

            return;
        }

        if (_codes.ContainsKey(key) || _loading.Contains(key))
        {
            return;
        }

        await LoadAsync(key, changed);
    }

    /// <summary>Fetch the codes that stand in for a name, for every link that has none, one at a time.</summary>
    public async Task LoadUnnamedAsync(VariableDetail detail, Action changed)
    {
        foreach (var link in detail.KodeverkLinks.Where(IsUnnamedKildekodeverk))
        {
            var key = KodeverkKey.Of(link);

            // A failure counts as asked, so a kodeverk the payload names twice is not retried back to back.
            if (_codes.ContainsKey(key) || _loading.Contains(key) || _failures.ContainsKey(key))
            {
                continue;
            }

            await LoadAsync(key, changed);
        }
    }

    /// <summary>Whether this link has no name worth drawing, so its codes are what identify it.</summary>
    public static bool IsUnnamedKildekodeverk(KodeverkLink link) =>
        string.Equals(link.KodeverkType, "Kildekodeverk", StringComparison.OrdinalIgnoreCase)
        && DisplayText.Trimmed(link.DisplayName) is null
        && link.HasCodeValues;

    private async Task LoadAsync(KodeverkKey key, Action changed)
    {
        _failures.Remove(key);
        _loading.Add(key);
        changed();

        try
        {
            // Null means the catalogue publishes no codes for this link, so it is cached as an empty list.
            var codes = await client.GetKodeverkCodesAsync(variableId, key.Type, key.Reference);

            _codes[key] = codes?.Codes ?? [];
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            log?.LogWarning(ex, "the rate limiter refused the kodeverk codes of variable {VariableId}", variableId);
            _failures[key] = CodesFailure.Throttled;
        }
        catch (Exception ex)
        {
            log?.LogError(ex, "could not load the kodeverk codes of variable {VariableId}", variableId);
            _failures[key] = CodesFailure.Failed;
        }
        finally
        {
            _loading.Remove(key);
        }
    }
}
