using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The API's kildetype words for a component holding no filters payload, asked once per language.</summary>
/// <remarks>The client is optional: with none, or a failed call, the shipped table answers (Fhi.Metadata-vcxoc).</remarks>
internal sealed class KildeTypeVocabulary
{
    private readonly Dictionary<string, Task<IReadOnlyList<KildetypeFacet>>> _byLanguage =
        new(StringComparer.Ordinal);

    /// <summary>The fetch for this language still to be awaited, or null when its answer is already here.</summary>
    internal Task? Load(IMuninExplorerClient? client, string? language, ILogger? log)
    {
        var apiLanguage = ReaderLanguage.ForApi(language);

        if (_byLanguage.TryGetValue(apiLanguage, out var started))
        {
            return started.IsCompleted ? null : started;
        }

        var fetch = FetchAsync(client, apiLanguage, log);

        _byLanguage[apiLanguage] = fetch;

        return fetch;
    }

    /// <summary>
    /// Takes the caller's own filters request for <paramref name="language"/> while it is still in flight, so a view
    /// it mounts awaits that one instead of asking again. The language is the request's, not the reader's current one.
    /// </summary>
    internal void Share(string? language, Task<FilterOptions> request) =>
        _byLanguage[ReaderLanguage.ForApi(language)] = KildeTyperOf(request);

    /// <summary>Takes a payload the caller already holds, so a view it mounts names the kildetype from the same one.</summary>
    internal void Seed(string? language, FilterOptions? facets)
    {
        if (facets is not null)
        {
            _byLanguage[ReaderLanguage.ForApi(language)] = Task.FromResult(facets.KildeTyper ?? []);
        }
    }

    private static async Task<IReadOnlyList<KildetypeFacet>> KildeTyperOf(Task<FilterOptions> request)
    {
        try
        {
            return (await request).KildeTyper ?? [];
        }
        catch
        {
            // The caller that owns the request logs its failure; here it only means the shipped table answers.
            return [];
        }
    }

    /// <summary><see cref="Texts.KildeTypeNameFromApi"/> over whatever this language has fetched so far.</summary>
    internal string Name(Texts texts, string? language, string? value) =>
        texts.KildeTypeNameFromApi(value, ApiName(language, value));

    /// <summary>The facet's <c>displayName</c> as the API sent it, for a caller that resolves it itself.</summary>
    internal string? ApiName(string? language, string? value) =>
        _byLanguage.TryGetValue(ReaderLanguage.ForApi(language), out var fetch) && fetch.IsCompletedSuccessfully
            ? fetch.Result
                .FirstOrDefault(type => string.Equals(type.Value, value, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName
            : null;

    // Munin's fixed order (innmeldt Munin#6832), by code like its KildetypeOrder. Not read from the filters answer:
    // that lists only kildetyper with variables, and the kilde explorer also lists kilder without any (Fhi.Metadata-yk2ur).
    private static readonly string[] Ranked =
        ["sentraltHelseregister", "befolkningsbasertHelseundersokelse", "nasjonaltMedisinskKvalitetsregister", "annenDatakilde"];

    /// <summary>A kildetype's place in the fixed order; every other type ranks after it, so the label decides.</summary>
    internal static int Rank(string? value)
    {
        var index = Array.FindIndex(Ranked, code => string.Equals(code, value, StringComparison.OrdinalIgnoreCase));

        return index < 0 ? Ranked.Length : index;
    }

    private static async Task<IReadOnlyList<KildetypeFacet>> FetchAsync(
        IMuninExplorerClient? client, string apiLanguage, ILogger? log)
    {
        if (client is null)
        {
            return [];
        }

        try
        {
            return (await client.GetFiltersAsync(language: apiLanguage)).KildeTyper ?? [];
        }
        catch (Exception ex)
        {
            // Warning, not Error: the reader gets the shipped word rather than nothing.
            log?.LogWarning(ex, "could not load the kildetype names for {Language}", apiLanguage);

            return [];
        }
    }
}
