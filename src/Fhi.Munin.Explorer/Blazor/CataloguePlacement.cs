using System.Collections.ObjectModel;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One payload's placement questions, asked over a single snapshot of the values and the
/// suppressions its sections are drawn from.
/// </summary>
/// <remarks>
/// A snapshot rather than a set of expressions per view. <see cref="Placed"/> and
/// <see cref="Curated"/> are read one after the other about the same fact, and answered over two
/// separate resolutions they can disagree about which payload they were answering for — the hero
/// would report the curated word for a source whose placement was decided against other metadata.
/// <para>
/// Shared by the detail views because the rule is shared, not because the code is short: the three
/// views' private copies of the catalogue's date handling had already drifted apart before
/// <see cref="CatalogueDate"/> gathered them, and <see cref="ValidityRows"/> is subtler than any of
/// those were (Fhi.Metadata-bct95).
/// </para>
/// </remarks>
internal sealed record CataloguePlacement(
    IReadOnlyList<PropertyMetadataEntry> Metadata,
    IReadOnlyDictionary<string, string?> Values,
    string Reader,
    IReadOnlySet<string> DrawnElsewhere)
{
    /// <summary>A view with no payload yet: nothing is placed, and nothing is curated.</summary>
    internal static CataloguePlacement None { get; } =
        new([], ReadOnlyDictionary<string, string?>.Empty, ReaderLanguage.Norwegian,
            new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Whether the catalogue's own sections draw this key, so the view must not.</summary>
    /// <remarks>
    /// The merged values and the view's own suppressions both go in, so the question is the one
    /// <see cref="CatalogueProperties.Groups"/> answers rather than a weaker one about the placement
    /// alone — the rule <see cref="CatalogueProperties.Placed"/> states in full.
    /// </remarks>
    internal bool Placed(string key) =>
        CatalogueProperties.Placed(Metadata, Values, Reader, key, DrawnElsewhere);

    /// <summary>One curated property's first value, resolved exactly as its section resolves it.</summary>
    internal string? Curated(string key) =>
        CatalogueProperties.Row(Metadata, Values, Reader, key) is { Values: [var first, ..] }
            ? first.Text
            : null;

    /// <summary>
    /// A fact box's row, or no row where the catalogue has placed the key in a section of its own
    /// and that section is drawing it.
    /// </summary>
    /// <remarks>
    /// The same fact in a section and in a fact box is two rows under one label in two different
    /// words, which reads as two legitimate rows; dropped outright it would be a blank field on a
    /// public page wherever Munin's placements have not arrived (Fhi.Metadata-bct95).
    /// <para>The row goes, not its value: a row with no value reads "Ingen" (Fhi.Metadata-35w0p.24).</para>
    /// </remarks>
    internal IReadOnlyList<TRow> UnlessPlaced<TRow>(string key, TRow row) => Placed(key) ? [] : [row];

    /// <summary>
    /// The validity as a fact box shows it: the period, the one end no section has taken, or no row
    /// at all once the catalogue draws both ends itself.
    /// </summary>
    /// <remarks>
    /// Per end, because the catalogue places GyldigFra and GyldigTil separately: yielding the whole
    /// period to either placement leaves the other date drawn nowhere, and keeping the period beside
    /// a placed end either repeats that date or, with the closing one placed, reads as ongoing.
    /// <para>
    /// Which row to draw is decided here; how each is worded stays the caller's, resolved once on
    /// the member its hero row reads too — this formats no dates, so a box and a hero cannot spell
    /// one period two ways. Both ends are asked of one snapshot, so the pair cannot be answered
    /// against two payloads.
    /// </para>
    /// </remarks>
    internal IReadOnlyList<(string Label, string? Value, bool Norwegian)> ValidityRows(
        string? period, string? from, string? to, Texts texts)
    {
        var fromPlaced = Placed(CatalogueColumns.ValidFrom);
        var toPlaced = Placed(CatalogueColumns.ValidTo);

        if (!fromPlaced && !toPlaced)
        {
            return [(texts.FieldValidity, period, false)];
        }

        if (!fromPlaced)
        {
            return [(texts.FieldValidFrom, from, false)];
        }

        // Here a section draws the start, so an open end is ongoing, as the period says, not absent.
        if (!toPlaced)
        {
            return [(texts.FieldValidTo, to ?? texts.Ongoing, false)];
        }

        return [];
    }
}
