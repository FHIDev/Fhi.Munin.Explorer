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
    /// <summary>Norwegian as a culture name, which is neither of the tags we render with.</summary>
    private const string NorwegianCulture = "nb-NO";

    /// <summary>The only two cultures this package ever formats in, resolved once.</summary>
    /// <remarks>
    /// Once rather than per call, because <see cref="Formatting"/> is a caught throw on exactly the
    /// host it was written for: with <c>InvariantGlobalization</c> every name fails. The date
    /// helpers run twice per period cell, so a page of results would construct, throw and catch a
    /// <see cref="CultureNotFoundException"/> some fifty times per render, and the degraded host
    /// would be the one paying for it. Resolved here it costs two throws at type load, once.
    /// </remarks>
    private static readonly CultureInfo NorwegianFormatting = Formatting(NorwegianCulture);

    /// <inheritdoc cref="NorwegianFormatting"/>
    private static readonly CultureInfo EnglishFormatting = Formatting(ReaderLanguage.English);

    /// <summary>The culture to format dates and numbers in for this reader.</summary>
    /// <remarks>
    /// <c>nb-NO</c> rather than a bare <c>no</c>: the neutral culture gives ISO-ish dates, and the
    /// point of formatting per reader is that a Norwegian one sees a Norwegian date. English is
    /// content with the neutral <c>en</c> because the reasoning does not carry over — the neutral
    /// English culture already formats the way an English reader expects, and picking a region for
    /// them would be picking one the host never asked for, with <c>en-GB</c> and <c>en-US</c>
    /// disagreeing about which way round a numeric date goes.
    /// </remarks>
    internal static CultureInfo Culture(string? language)
        => ReaderLanguage.IsEnglish(language) ? EnglishFormatting : NorwegianFormatting;

    /// <summary>A culture by name, or the invariant one where the host has none.</summary>
    /// <remarks>
    /// A host built with <c>InvariantGlobalization</c> has <c>PredefinedCulturesOnly</c> on, and
    /// there <see cref="CultureInfo.GetCultureInfo(string)"/> throws rather than returning
    /// anything. Thrown from the initialiser of <see cref="NorwegianFormatting"/> or
    /// <see cref="CatalogueOrder"/> it becomes a <c>TypeInitializationException</c> that takes
    /// every property row with it and cannot be retried once thrown. The same reasoning the type's
    /// own remarks give for not parsing tokens as cultures applies here: a host we cannot format
    /// for should cost us the formatting, not the page.
    /// <para>
    /// <see cref="CultureNotFoundException"/> alone is the whole surface, checked rather than
    /// assumed: with <c>PredefinedCulturesOnly</c> on, every name fails that way — including
    /// <c>nb-NO</c> and <c>en</c> — and with it off the only failures are names ICU will not
    /// fabricate a culture for, which fail the same way. Internal rather than private so a test can
    /// reach the branch, which no host running the suite can otherwise take.
    /// </para>
    /// </remarks>
    internal static CultureInfo Formatting(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>The order the catalogue's own names sort in.</summary>
    /// <remarks>
    /// Norwegian, always, and deliberately not the reader's language or the thread's. The strings
    /// being sorted are names the catalogue stores once in Norwegian, so æ, ø and å belong at the
    /// end of the alphabet whoever is reading. Sorting them by the reader's culture would put an
    /// English reader's list in a different order from a Norwegian colleague's for the same source,
    /// and sorting by the thread's would make the order depend on whatever the host happened to set.
    /// </remarks>
    internal static readonly StringComparer CatalogueOrder =
        StringComparer.Create(NorwegianFormatting, ignoreCase: false);

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
    /// <para>
    /// <paramref name="drawnElsewhere"/> names keys the caller renders itself, so the same fact does
    /// not appear twice on one page. A variable's <c>DataType</c> is the case this was written for:
    /// the sidebar shows it, and left in the groups it also appeared there — under the same label,
    /// with a different word, because the sidebar translates the code and the group resolves it
    /// through the catalogue's own vocabulary, whose Norwegian labels for this field are English.
    /// Dropping the key drops the group with it whenever nothing else in that group is filled in,
    /// which is exactly what Runa shows.
    /// </para>
    /// </remarks>
    internal static List<PropertyGroup> Groups(
        IReadOnlyList<PropertyMetadataEntry> metadata,
        IReadOnlyDictionary<string, string?> values,
        string reader,
        IReadOnlySet<string>? drawnElsewhere = null)
    {
        var groups = new List<(string Name, string Language, int Order, List<PropertyMetadataEntry> Entries)>();

        foreach (var entry in metadata)
        {
            if (drawnElsewhere is not null && drawnElsewhere.Contains(entry.Key))
            {
                continue;
            }

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
    internal static (string Value, string Language) Value(PropertyMetadataEntry entry, string raw, string reader) =>
        // A code the vocabulary does not list is shown as it arrived, and called Norwegian with the
        // rest of the catalogue's own text. Showing it beats showing nothing: it is what the
        // catalogue holds, and a blank cell would hide that the two disagree.
        Word(entry, raw, reader) ?? (raw, "no");

    /// <summary>
    /// The vocabulary's own word for a stored code, or nothing at all where it lists none.
    /// </summary>
    /// <remarks>
    /// The lookup <see cref="Value"/> is built on, separated out because the two callers want
    /// different things from a miss. A property row shows the code and calls it Norwegian, which is
    /// what the catalogue holds. A facet cannot: the codes it draws are CURIEs into EU and EHDS
    /// vocabularies — <c>eu-access:OP_DATPRO</c> — which are prose in no language at all, and a
    /// <c>lang="no"</c> over one hands it to a screen reader in a Norwegian voice (WCAG 3.1.2). So
    /// the miss is reported rather than papered over, and each caller says what it means by it.
    /// <para>
    /// Matching is on the whole stored value, never on the part after a colon. Two prefixes over
    /// one bare token are two values in the catalogue, so a prefix-blind lookup would answer
    /// <c>annet-vokabular:biobanks</c> with the word for <c>ehds-cat:biobanks</c> — a label naming
    /// a vocabulary entry the value is not in.
    /// </para>
    /// </remarks>
    internal static (string Label, string Language)? Word(PropertyMetadataEntry entry, string raw, string reader)
    {
        if (string.IsNullOrWhiteSpace(entry.OptionsJson))
        {
            return null;
        }

        foreach (var option in Options(entry.OptionsJson, reader))
        {
            if (string.Equals(option.Value, raw, StringComparison.OrdinalIgnoreCase))
            {
                return (option.Label, option.Language);
            }
        }

        return null;
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
