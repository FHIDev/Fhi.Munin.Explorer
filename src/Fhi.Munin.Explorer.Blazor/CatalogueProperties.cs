using System.Globalization;
using System.Text.Json;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>One curated property, resolved into what a reader should see.</summary>
/// <param name="Label">The property's name in the reader's language, or the nearest available.</param>
/// <param name="LabelLanguage">Which language <paramref name="Label"/> actually ended up in.</param>
/// <param name="Value">The value as a word where the catalogue offers one, otherwise as stored.</param>
/// <param name="ValueLanguage">Which language <paramref name="Value"/> actually ended up in.</param>
internal readonly record struct PropertyRow(
    string Label,
    string LabelLanguage,
    string Value,
    string ValueLanguage);

/// <summary>A named group of properties, as the catalogue arranges them.</summary>
internal sealed record PropertyGroup(
    string Name,
    string NameLanguage,
    IReadOnlyList<PropertyRow> Rows);

/// <summary>
/// Resolving the catalogue's own properties: their names, their groups, and the words behind their
/// coded values.
/// </summary>
/// <remarks>
/// Nothing here knows which properties exist. Keys, labels, ordering, grouping and vocabularies all
/// arrive with the payload, because they are editable master data — a property added or renamed in
/// Munin shows up without this package being touched, and a copy of any of it would be stale the
/// first time someone edited a definition.
/// <para>
/// Shared rather than private to one explorer: the variabelutforsker and the kildeutforsker ship
/// from the same package and both draw properties this way. A second copy would drift from this one
/// the first time either was edited.
/// </para>
/// </remarks>
internal static class CatalogueProperties
{
    /// <summary>The reader's language as a tag: <c>en</c> or <c>no</c>.</summary>
    internal static string Reader(string? language)
        => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "no";

    /// <summary>The culture to format dates and numbers in for this reader.</summary>
    /// <remarks>
    /// <c>nb-NO</c> rather than a bare <c>no</c>: the neutral culture gives ISO-ish dates, and the
    /// point of formatting per reader is that a Norwegian one sees a Norwegian date.
    /// </remarks>
    internal static CultureInfo Culture(string? language)
        => CultureInfo.GetCultureInfo(string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "nb-NO");

    /// <summary>
    /// A <c>lang</c> for text that is not in the reader's language, or null when it is.
    /// </summary>
    /// <remarks>
    /// Curation is uneven, so an English page carries some Norwegian: labels nobody translated, and
    /// every free-text value, which the catalogue only ever stores in Norwegian. Marking those lets
    /// a screen reader switch voice rather than read Norwegian with English phonetics, which is the
    /// difference between an accent and being unintelligible. Text already in the reader's language
    /// is left unmarked so it inherits from the host.
    /// </remarks>
    internal static string? Foreign(string language, string reader)
        => string.Equals(language, reader, StringComparison.OrdinalIgnoreCase) ? null : language;

    /// <summary>
    /// The properties worth drawing, as label and value, in the catalogue's order.
    /// </summary>
    /// <remarks>
    /// A key with no metadata is skipped rather than drawn under its raw name: the bag can carry
    /// keys the catalogue no longer curates, and "FlerkodetFelt: 1" tells a reader nothing.
    /// </remarks>
    internal static List<PropertyRow> Rows(
        IEnumerable<PropertyMetadataEntry> metadata,
        IReadOnlyDictionary<string, string?> values,
        string reader)
    {
        var rows = new List<PropertyRow>();

        foreach (var entry in metadata.OrderBy(m => m.SortOrder).ThenBy(m => m.Key, StringComparer.Ordinal))
        {
            if (!values.TryGetValue(entry.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var (label, labelLanguage) = Localised(entry.DisplayNameTranslations, reader);

            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var (value, valueLanguage) = Value(entry, raw, reader);

            rows.Add(new PropertyRow(label, labelLanguage, value, valueLanguage));
        }

        return rows;
    }

    /// <summary>
    /// The properties gathered into the groups the catalogue puts them in.
    /// </summary>
    /// <remarks>
    /// Two rules, both measured against Runa rather than guessed at. A source there carries 73
    /// properties across 13 groups but shows 8.
    /// <list type="number">
    /// <item>
    /// A group whose every key is empty is dropped. Five go that way on a typical source, one of
    /// them holding eleven unset keys — drawing them would be eleven blank rows under a heading
    /// that promised something.
    /// </item>
    /// <item>
    /// Groups are ordered by the lowest sort order among the keys that <em>have</em> values, not
    /// among all their keys. This is not a detail: counting all keys puts two groups on the same
    /// number and leaves their order to however the dictionary enumerated, which is to say
    /// arbitrary. Counting only the populated ones separates them and matches what Runa shows.
    /// </item>
    /// </list>
    /// </remarks>
    internal static List<PropertyGroup> Groups(
        IReadOnlyList<PropertyMetadataEntry> metadata,
        IReadOnlyDictionary<string, string?> values,
        string reader)
    {
        var groups = new List<(string Name, string Language, int Order, List<PropertyMetadataEntry> Entries)>();

        foreach (var entry in metadata)
        {
            var (name, language) = Localised(entry.GroupTranslations, reader);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var existing = groups.FindIndex(g => string.Equals(g.Name, name, StringComparison.Ordinal));

            if (existing < 0)
            {
                groups.Add((name, language, int.MaxValue, [entry]));
            }
            else
            {
                groups[existing].Entries.Add(entry);
            }
        }

        var resolved = new List<(PropertyGroup Group, int Order)>();

        foreach (var (name, language, _, entries) in groups)
        {
            var rows = Rows(entries, values, reader);

            if (rows.Count == 0)
            {
                continue;
            }

            var order = entries
                .Where(e => values.TryGetValue(e.Key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                .Select(e => e.SortOrder)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            resolved.Add((new PropertyGroup(name, language, rows), order));
        }

        return [.. resolved.OrderBy(g => g.Order).Select(g => g.Group)];
    }

    /// <summary>
    /// A coded value as its label, or the value itself when it is not coded, with the language it
    /// ended up in.
    /// </summary>
    /// <remarks>
    /// Anything not drawn from a vocabulary is Norwegian: free text and identifiers are stored once,
    /// in the catalogue's own language, with no translated counterpart to fall back to.
    /// </remarks>
    internal static (string Value, string Language) Value(PropertyMetadataEntry entry, string raw, string reader)
    {
        if (string.IsNullOrWhiteSpace(entry.OptionsJson))
        {
            return (raw, "no");
        }

        foreach (var option in Options(entry.OptionsJson, reader))
        {
            if (string.Equals(option.Value, raw, StringComparison.OrdinalIgnoreCase))
            {
                return (option.Label, option.Language);
            }
        }

        // A code the vocabulary does not list. Showing it beats showing nothing: it is what the
        // catalogue holds, and a blank cell would hide that the two disagree.
        return (raw, "no");
    }

    /// <summary>
    /// The options in a vocabulary, as value and label in the reader's language.
    /// </summary>
    /// <remarks>
    /// Malformed JSON yields nothing rather than throwing. This is curated data arriving over the
    /// wire, and one bad definition should cost that one field its label, not take the page down.
    /// </remarks>
    internal static IReadOnlyList<(string Value, string Label, string Language)> Options(
        string optionsJson,
        string reader)
    {
        try
        {
            using var document = JsonDocument.Parse(optionsJson);

            if (document.RootElement.ValueKind is not JsonValueKind.Array)
            {
                return [];
            }

            var options = new List<(string, string, string)>();

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind is not JsonValueKind.Object
                    || !element.TryGetProperty("value", out var value))
                {
                    continue;
                }

                var code = value.ToString();

                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                var english = string.Equals(reader, "en", StringComparison.OrdinalIgnoreCase);
                var preferred = english ? "labelEn" : "label";

                var label = element.TryGetProperty(preferred, out var chosen) ? chosen.ToString() : null;
                var language = english ? "en" : "no";

                if (string.IsNullOrWhiteSpace(label) && element.TryGetProperty("label", out var fallback))
                {
                    // No English for this option. Norwegian beats the bare code, but it is Norwegian,
                    // and saying so is what lets it be read aloud correctly.
                    label = fallback.ToString();
                    language = "no";
                }

                options.Add((code, string.IsNullOrWhiteSpace(label) ? code : label, language));
            }

            return options;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// A translation bag's entry for the reader's language, falling back to Norwegian, with the
    /// language it ended up in.
    /// </summary>
    internal static (string? Text, string Language) Localised(
        IReadOnlyDictionary<string, string> translations,
        string reader)
    {
        var english = string.Equals(reader, "en", StringComparison.OrdinalIgnoreCase);

        if (english && translations.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en))
        {
            return (en, "en");
        }

        foreach (var key in new[] { "no", "nb" })
        {
            if (translations.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return (value, "no");
            }
        }

        // Some other language entirely. Nothing here can name it, so it is left unmarked rather than
        // asserted to be Norwegian on no evidence.
        return (translations.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)), reader);
    }
}
