using System.Text.Json;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A kilde's «Tilgangsnivå» on helsedata.no: Munin's kilde-scoped multi-select of four codes, curated apart from the
/// EU access-rights token (M9, Fhi.Metadata-d7vf4).
/// </summary>
internal static class AccessLevels
{
    /// <summary>The kilde property holding the codes, as a JSON array.</summary>
    internal const string Key = "TilgangsnivaaHelsedata";

    /// <summary>The kilde's codes, trimmed and once each; none when the property is absent, blank or not a list.</summary>
    internal static IReadOnlyList<string> Codes(IReadOnlyDictionary<string, string?>? properties)
    {
        var raw = properties?.FirstOrDefault(p => string.Equals(p.Key, Key, StringComparison.OrdinalIgnoreCase)).Value;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(raw);

            if (document.RootElement.ValueKind is JsonValueKind.String)
            {
                return [.. Present([document.RootElement.GetString()])];
            }

            return document.RootElement.ValueKind is JsonValueKind.Array
                ? [.. Present(document.RootElement.EnumerateArray()
                    .Where(element => element.ValueKind is JsonValueKind.String)
                    .Select(element => element.GetString()))]
                : [];
        }
        catch (JsonException)
        {
            // A plain value where a list belongs is still the one the catalogue holds.
            return [.. Present([raw])];
        }
    }

    /// <summary>
    /// The codes as words in the reader's language, in the vocabulary's order (helsedata.no's); a code the vocabulary
    /// does not word comes last, as itself and in no language.
    /// </summary>
    internal static IReadOnlyList<(string Code, string Label, string? Language)> Labels(
        IReadOnlyList<string> codes, PropertyMetadataEntry? vocabulary, string reader)
    {
        var options = vocabulary is { OptionsJson: { } json } ? CatalogueProperties.Options(json, reader) : [];
        var known = options
            .Where(option => codes.Contains(option.Value, StringComparer.OrdinalIgnoreCase))
            .Select(option => option.Curated
                ? (option.Value, option.Label, CatalogueProperties.Foreign(option.Language, reader))
                : (option.Value, option.Value, null));
        var unknown = codes
            .Where(code => !options.Any(option => string.Equals(option.Value, code, StringComparison.OrdinalIgnoreCase)))
            .Select(code => (code, code, (string?)null));

        return [.. known, .. unknown];
    }

    private static IEnumerable<string> Present(IEnumerable<string?> values) =>
        values.Select(value => value?.Trim() ?? "").Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
}
