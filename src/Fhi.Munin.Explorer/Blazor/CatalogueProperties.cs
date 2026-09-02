using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>One language's text out of a translation bag, and the language it is in.</summary>
/// <param name="Text">The text as that language holds it.</param>
/// <param name="Language">What to mark it with: <c>no</c>, <c>en</c>, or the catalogue's own tag.</param>
internal readonly record struct LocalisedText(string Text, string Language);

/// <summary>One curated property, resolved into what a reader should see.</summary>
/// <param name="Label">The property's name in the reader's language, or the nearest available.</param>
/// <param name="LabelLanguage">Which language <paramref name="Label"/> actually ended up in.</param>
/// <param name="Values">
/// Every language the value holds, the reader's first. One entry for all but the multilingual
/// types, whose bags the catalogue leaves open.
/// </param>
internal readonly record struct PropertyRow(
    string Label,
    string LabelLanguage,
    IReadOnlyList<LocalisedText> Values);

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
    /// <para>
    /// <paramref name="values"/> is nullable although every contract declares
    /// <c>AdditionalProperties</c> non-nullable — see
    /// <see cref="Contracts.KildeSummary.AdditionalProperties"/> for how a null gets in, and
    /// <c>NullAsEmptyCollections</c> for what stops it arriving from this package's own client. A
    /// host can substitute that client, so it is taken here as well: as the empty bag, which is
    /// what the payload means by it, and which is the same answer <c>KildeExplorer.Property</c>
    /// gives on the list side. Guarded here rather than at each call site because all three of them
    /// pass a field declared that way: <c>KildeView</c>, <c>VariableView</c> and the variable
    /// panel's own rows. Unguarded it throws while rendering, where the try/catch around the fetch
    /// is long since finished and cannot catch it.
    /// </para>
    /// </remarks>
    internal static List<PropertyRow> Rows(
        IEnumerable<PropertyMetadataEntry> metadata,
        IReadOnlyDictionary<string, string?>? values,
        string reader)
    {
        var rows = new List<PropertyRow>();
        var present = values ?? ReadOnlyDictionary<string, string?>.Empty;

        foreach (var entry in metadata.OrderBy(m => m.SortOrder).ThenBy(m => m.Key, StringComparer.Ordinal))
        {
            if (!present.TryGetValue(entry.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var (label, labelLanguage) = Localised(entry.DisplayNameTranslations, reader);

            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            // A key whose value the view has nothing honest to draw is dropped rather than drawn
            // empty. Only the structured types answer this way — see Value.
            if (Value(entry, raw, reader) is not { } resolved)
            {
                continue;
            }

            rows.Add(new PropertyRow(label, labelLanguage, resolved));
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
    /// <para>
    /// <paramref name="values"/> is nullable for the reason <see cref="Rows"/> gives, and taken the
    /// same way. Normalised here as well as there because the group ordering reads the bag directly
    /// rather than through <see cref="Rows"/>.
    /// </para>
    /// </remarks>
    internal static List<PropertyGroup> Groups(
        IReadOnlyList<PropertyMetadataEntry> metadata,
        IReadOnlyDictionary<string, string?>? values,
        string reader,
        IReadOnlySet<string>? drawnElsewhere = null)
    {
        var present = values ?? ReadOnlyDictionary<string, string?>.Empty;
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
            var rows = Rows(entries, present, reader);

            if (rows.Count == 0)
            {
                continue;
            }

            var order = entries
                .Where(e => present.TryGetValue(e.Key, out var raw) && !string.IsNullOrWhiteSpace(raw))
                .Select(e => e.SortOrder)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            resolved.Add((new PropertyGroup(name, language, rows), order));
        }

        return [.. resolved.OrderBy(g => g.Order).Select(g => g.Group)];
    }

    // The catalogue's names for the property types whose value is not prose. Matched
    // case-insensitively: Type is a string so a type added server side cannot break
    // deserialisation, and casing that shifts there must not put the envelope back on the page.
    private const string MultilingualTextType = "MultilingualText";
    private const string LangTaggedListType = "LangTaggedList";
    private const string MultiSelectType = "MultiSelect";
    private const string ObjectType = "Object";

    // The shape the view already draws the catalogue's own semicolon lists in, so unwrapping a
    // list into a row does not introduce a second way of showing one.
    private const string Separator = "; ";

    private static bool Typed(PropertyMetadataEntry entry, string type)
        => string.Equals(entry.Type, type, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A stored value as a reader should see it, in every language it holds, or nothing where
    /// there is nothing honest to draw.
    /// </summary>
    /// <remarks>
    /// Only the two multilingual types carry more than one slot. The rest answer with one, so a
    /// caller has a single shape to draw rather than a cell here and a list there.
    /// </remarks>
    internal static IReadOnlyList<LocalisedText>? Value(PropertyMetadataEntry entry, string raw, string reader)
    {
        // A curated label over uncurated parts: no honest single cell, so the row goes instead.
        if (Typed(entry, ObjectType))
        {
            return null;
        }

        var bag =
            Typed(entry, MultilingualTextType) ? Multilingual(raw, reader)
            : Typed(entry, LangTaggedListType) ? Tagged(raw, reader)
            : null;

        if (bag is not null)
        {
            return bag;
        }

        // An unwrapping declines on a value that is not the shape its type promises, which the
        // catalogue produces often enough to be the path rather than the net — and a value that
        // disagrees with its type is still a value, so it is shown as it arrived.
        var (text, language) =
            (Typed(entry, MultiSelectType) ? Chosen(entry, raw, reader) : null)
            ?? Word(entry, raw, reader)
            ?? (raw, "no");

        return [new LocalisedText(text, language)];
    }

    /// <summary>A value parsed as JSON, or nothing where it is not structured.</summary>
    /// <returns>A document the caller owns; <see cref="JsonElement"/> outlives no document.</returns>
    private static JsonDocument? Structured(string raw)
    {
        // Parsed only past a first character that could open one: values that are not envelopes are
        // the common case on some of these types, and each would otherwise cost a thrown
        // JsonException per row per render, on the same reasoning NorwegianFormatting gives.
        var trimmed = raw.AsSpan().TrimStart();

        if (trimmed.IsEmpty || (trimmed[0] is not '{' and not '['))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Every language a multilingual envelope holds, or nothing where it is not one.</summary>
    private static IReadOnlyList<LocalisedText>? Multilingual(string raw, string reader)
    {
        // The envelope's keys are language tags, so it is a translation bag and resolved by
        // AllLocalised rather than by a second copy of the fallback.
        using var document = Structured(raw);

        if (document?.RootElement.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String
                && property.Value.GetString() is { } text
                && !string.IsNullOrWhiteSpace(text))
            {
                translations[property.Name] = text;
            }
        }

        var slots = AllLocalised(translations, reader);

        return slots.Count == 0 ? null : slots;
    }

    /// <summary>
    /// Every language a list whose entries carry their own tags holds, or nothing where the value
    /// is not such a list.
    /// </summary>
    private static IReadOnlyList<LocalisedText>? Tagged(string raw, string reader)
    {
        // Gathered per language and joined per language, so a reader gets one language's list whole
        // rather than another's spliced through it.
        using var document = Structured(raw);

        if (document?.RootElement.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        var byLanguage = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in document.RootElement.EnumerateArray())
        {
            var (text, language) = Entry(element);

            if (text is not { } present || string.IsNullOrWhiteSpace(present))
            {
                continue;
            }

            if (!byLanguage.TryGetValue(language, out var texts))
            {
                byLanguage[language] = texts = [];
            }

            texts.Add(present);
        }

        if (byLanguage.Count == 0)
        {
            return null;
        }

        var joined = byLanguage.ToDictionary(
            l => l.Key,
            l => string.Join(Separator, l.Value),
            StringComparer.OrdinalIgnoreCase);

        var slots = AllLocalised(joined, reader);

        return slots.Count == 0 ? null : slots;
    }

    /// <summary>One entry of a language-tagged list, as its text and the language it claims.</summary>
    private static (string? Text, string Language) Entry(JsonElement element) => element.ValueKind switch
    {
        // Untagged text is Norwegian, which is where the catalogue's own always sits.
        JsonValueKind.String => (element.GetString(), "no"),
        JsonValueKind.Object => (
            element.TryGetProperty("value", out var value) && value.ValueKind is JsonValueKind.String
                ? value.GetString()
                : null,
            element.TryGetProperty("language", out var language)
            && language.ValueKind is JsonValueKind.String
            && language.GetString() is { Length: > 0 } tag
                ? tag
                : "no"),
        _ => (null, "no"),
    };

    /// <summary>
    /// The vocabulary's words for every code a multi-valued property holds, or nothing where the
    /// value is not a list of codes.
    /// </summary>
    private static (string Value, string Language)? Chosen(PropertyMetadataEntry entry, string raw, string reader)
    {
        // Word matches on the whole stored value, so handed ["a","b"] it compares the array's own
        // text against the vocabulary and misses. Looked up one code at a time instead.
        using var document = Structured(raw);

        if (document?.RootElement.ValueKind is not JsonValueKind.Array)
        {
            return null;
        }

        var labels = new List<string>();
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind is not JsonValueKind.String
                || element.GetString() is not { } code
                || string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var (label, language) = Word(entry, code, reader) ?? (code, "no");

            labels.Add(label);
            languages.Add(language);
        }

        if (labels.Count == 0)
        {
            return null;
        }

        // One lang covers the whole cell, so it is honest only where every part agrees; mixed, it
        // falls to Norwegian with the rest of the catalogue's own text.
        return (string.Join(Separator, labels), languages.Count == 1 ? languages.First() : "no");
    }

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
    /// A translation bag's entry for the reader's language, falling back to Norwegian and then to
    /// whatever the bag does hold, with the language it ended up in.
    /// </summary>
    /// <remarks>
    /// The first of <see cref="AllLocalised"/> rather than a resolution of its own: two orderings
    /// of the same bag would answer differently the first time either was edited, and the one a
    /// label is drawn from must be the one its value is drawn from.
    /// </remarks>
    internal static (string? Text, string Language) Localised(
        IReadOnlyDictionary<string, string> translations,
        string reader)
    {
        var slots = AllLocalised(translations, reader);

        return slots.Count == 0 ? (null, reader) : (slots[0].Text, slots[0].Language);
    }

    /// <summary>
    /// Every language a translation bag holds, the reader's first and the rest behind it.
    /// </summary>
    /// <remarks>
    /// The bag is open while the page offers two languages, so resolving to the reader's alone
    /// drops slots nothing here could reach; and a tag this package cannot name keeps the
    /// catalogue's own, since the reader's is the one <see cref="Foreign"/> drops (Fhi.Metadata-l9d5r).
    /// </remarks>
    internal static List<LocalisedText> AllLocalised(
        IReadOnlyDictionary<string, string> translations,
        string reader)
    {
        var slots = new List<LocalisedText>();

        foreach (var (tag, text) in translations
                     .Where(t => !string.IsNullOrWhiteSpace(t.Value))
                     .OrderBy(t => Rank(t.Key))
                     .ThenBy(t => t.Key, StringComparer.OrdinalIgnoreCase))
        {
            var language = Rank(tag) switch
            {
                0 or 1 => ReaderLanguage.Norwegian,
                2 => ReaderLanguage.English,
                _ => tag,
            };

            if (!slots.Any(s => string.Equals(s.Language, language, StringComparison.OrdinalIgnoreCase)))
            {
                slots.Add(new LocalisedText(text, language));
            }
        }

        var readers = slots.FindIndex(s => string.Equals(s.Language, reader, StringComparison.OrdinalIgnoreCase));

        if (readers > 0)
        {
            slots.Insert(0, slots[readers]);
            slots.RemoveAt(readers + 1);
        }

        return slots;
    }

    /// <summary>Where a bag's tag sorts, and which tags are one slot.</summary>
    /// <remarks>
    /// <c>no</c> and <c>nb</c> rank apart so a bag holding both resolves to <c>no</c>'s text
    /// whichever language the reader arrived in, rather than to whichever the dictionary happened
    /// to enumerate first.
    /// </remarks>
    private static int Rank(string tag) =>
        tag.Equals(ReaderLanguage.Norwegian, StringComparison.OrdinalIgnoreCase) ? 0
        : tag.Equals(ReaderLanguage.ApiNorwegian, StringComparison.OrdinalIgnoreCase) ? 1
        : tag.Equals(ReaderLanguage.English, StringComparison.OrdinalIgnoreCase) ? 2
        : 3;
}
