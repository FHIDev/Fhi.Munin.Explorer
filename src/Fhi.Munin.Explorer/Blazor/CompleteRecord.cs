using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The two halves of the catch-all section that the payload cannot supply: the view's own lead
/// paragraph, and the facts no property definition carries.
/// </summary>
/// <remarks>
/// Appended by the view because only the view has them — a count of variables or of datasamlinger
/// is not a curated property and no placement can ever name one (Fhi.Metadata-35w0p.21).
/// </remarks>
internal readonly record struct CompleteRecordExtras(
    string Lead,
    IReadOnlyList<(string Label, string? Value, bool Norwegian)> Facts);

/// <summary>
/// The values the catch-all section is assembled from: everything a named section reads, plus the
/// columns that identify the thing itself.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="CatalogueColumns.Values(KildeDetail, string?)"/> on purpose. A named
/// section must not start drawing the name and the code that the page's own header already draws;
/// the catch-all must, because the sentence over it says nothing is left out.
/// </remarks>
internal static class CompleteRecord
{
    /// <summary>The catalogue's keys for the columns a detail page draws in its name block.</summary>
    /// <remarks>
    /// Spelled as Munin spells them, for the reason <see cref="CatalogueColumns"/> gives: these are
    /// keys in someone else's data, and one that disagrees with the payload never matches.
    /// </remarks>
    internal const string Name = "PreferredTerm";

    /// <inheritdoc cref="Name"/>
    internal const string Code = "Code";

    /// <inheritdoc cref="Name"/>
    internal const string ShortName = "KortNavn";

    /// <inheritdoc cref="Name"/>
    internal const string Kildetype = "Kildetype";

    /// <summary>A source's own identity, over the values its sections already read.</summary>
    internal static IReadOnlyDictionary<string, string?> Values(
        KildeDetail kilde, IReadOnlyDictionary<string, string?> placed) =>
        With(placed,
             (Name, kilde.PreferredTerm),
             (Code, kilde.Code),
             (ShortName, kilde.ShortName),
             (Kildetype, kilde.Kildetype));

    /// <inheritdoc cref="Values(KildeDetail, IReadOnlyDictionary{string, string?})"/>
    internal static IReadOnlyDictionary<string, string?> Values(
        DatasamlingDetail datasamling, IReadOnlyDictionary<string, string?> placed) =>
        With(placed,
             (Name, datasamling.PreferredTerm),
             (Code, datasamling.Code),
             (ShortName, datasamling.ShortName));

    /// <inheritdoc cref="Values(KildeDetail, IReadOnlyDictionary{string, string?})"/>
    internal static IReadOnlyDictionary<string, string?> Values(
        VariableDetail variable, IReadOnlyDictionary<string, string?> placed) =>
        With(placed, (Name, variable.PreferredTerm), (Code, variable.Code));

    /// <summary>The bag with every named column that has a value added to it, the bag winning.</summary>
    /// <remarks>
    /// The rule <see cref="CatalogueColumns"/> merges by, and for its reasons: an empty column adds
    /// no key at all, so a property nobody filled in stays absent rather than drawing a blank row.
    /// </remarks>
    private static IReadOnlyDictionary<string, string?> With(
        IReadOnlyDictionary<string, string?> placed,
        params (string Key, string? Value)[] columns)
    {
        var values = new Dictionary<string, string?>(placed, StringComparer.Ordinal);

        foreach (var (key, value) in columns)
        {
            if (string.IsNullOrWhiteSpace(value)
                || (values.TryGetValue(key, out var curated) && !string.IsNullOrWhiteSpace(curated)))
            {
                continue;
            }

            values[key] = value;
        }

        return values;
    }
}
