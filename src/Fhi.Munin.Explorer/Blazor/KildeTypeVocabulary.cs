using Fhi.Munin.Explorer.Contracts;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The API's kildetype words for a component holding no filters payload, asked once per language.</summary>
/// <remarks>The client is optional: with none, or a failed call, the shipped table answers (Fhi.Metadata-vcxoc).</remarks>
internal sealed class KildeTypeVocabulary
{
    private readonly Dictionary<string, Task<IReadOnlyList<KildetypeFacet>>> _byLanguage =
        new(StringComparer.Ordinal);

    /// <summary>The fetch for this language, or null when one was already started.</summary>
    internal Task? Load(IMuninExplorerClient? client, string? language, ILogger? log)
    {
        var apiLanguage = ReaderLanguage.ForApi(language);

        if (_byLanguage.ContainsKey(apiLanguage))
        {
            return null;
        }

        var fetch = FetchAsync(client, apiLanguage, log);

        _byLanguage[apiLanguage] = fetch;

        return fetch;
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
